using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using MotionMatching;
using static MotionMatching.MotionMatchingData;

[CreateAssetMenu(fileName = "HeightMotionMatchingSearch", menuName = "MotionMatching/Search/Height Motion Matching Search")]
public class HeightMotionMatchingSearch : MotionMatchingSearch
{
    // Settings
    public float ObstacleDistanceThreshold = 0.6f;
    public float CrowdSecondTrajectoryWeight = 0.4f;
    public float CrowdThirdTrajectoryWeight = 0.1f;
    public EnvironmentAccelerationConsts EnvironmentAccelerationConsts;
    public bool IsNPC = false; // Adds some optimizations at the expense of less responsiveness.
    [Range(0.0f, 1.0f)] public float Evasion = 0.01f;
    [Tooltip("Scale the interaction features by the target speed of the character controller multiplied by this factor")] public float Anticipation = 2.0f;

    // Acceleration Structures
    private NativeArray<float> LargeBoundingBoxMin;
    private NativeArray<float> LargeBoundingBoxMax;
    private NativeArray<float> SmallBoundingBoxMin;
    private NativeArray<float> SmallBoundingBoxMax;
    private NativeArray<int> AdaptativeFeaturesIndices;

    // Debugging
    private NativeArray<float3> PointsOnEllipse;
    private NativeArray<float3> PointsOnObstacle;
    private NativeArray<float> ObstacleDistances;
    private NativeArray<float> ObstaclePenalization;
    private NativeArray<int> VisualDebugElements;

    // Other
    private IObstacleAwareCharacterControler CharacterController;
    private NativeArray<int> SearchResult;
    private NativeArray<float> Means;
    private NativeArray<float> Stds;
    private int HeightFeatureIndex;
    private int DirectionFeatureIndex;
    private float StartDirectionWeight;
    private int NPCid;
    private int MaxNPCs;
    private bool IsNPCFirst;
    private static int NPCCounter = 0;
    private bool IsDisposed = false;

    public override void Initialize(MotionMatchingController controller)
    {
        controller.FeatureSet.GetBVHBuffers(out LargeBoundingBoxMin,
                                            out LargeBoundingBoxMax,
                                            out SmallBoundingBoxMin,
                                            out SmallBoundingBoxMax);
        controller.FeatureSet.GetEnvironmentAccelerationStructures(EnvironmentAccelerationConsts, out AdaptativeFeaturesIndices);

        PointsOnEllipse = new NativeArray<float3>(1000, Allocator.Persistent); // some large number
        PointsOnObstacle = new NativeArray<float3>(1000, Allocator.Persistent); // some large number
        ObstacleDistances = new NativeArray<float>(1000, Allocator.Persistent); // some large number
        ObstaclePenalization = new NativeArray<float>(1000, Allocator.Persistent); // some large number
        VisualDebugElements = new NativeArray<int>(1, Allocator.Persistent);

        Means = new(controller.FeatureSet.GetMeans(), Allocator.Persistent);
        Stds = new(controller.FeatureSet.GetStandardDeviations(), Allocator.Persistent);

        DirectionFeatureIndex = -1;
        for (int i = 0; i < controller.MMData.TrajectoryFeatures.Count; i++)
        {
            if (controller.MMData.TrajectoryFeatures[i].Name == "FutureDirection")
            {
                DirectionFeatureIndex = i;
                break;
            }
        }
        if (DirectionFeatureIndex == -1)
        {
            Debug.LogError("HeightMotionMatchingSearch requires a 'FutureDirection' trajectory feature.");
        }
        StartDirectionWeight = controller.FeatureWeights[DirectionFeatureIndex];

        HeightFeatureIndex = -1;
        for (int i = 0; i < controller.MMData.EnvironmentFeatures.Count; i++)
        {
            if (controller.MMData.EnvironmentFeatures[i].Name == "FutureHeight")
            {
                HeightFeatureIndex = i;
                break;
            }
        }
        if (HeightFeatureIndex == -1)
        {
            Debug.LogError("HeightMotionMatchingSearch requires a 'FutureHeight' dynamic feature.");
        }

        if (IsNPC)
        {
            MaxNPCs = (int)math.round(controller.SearchTime * controller.DatabaseFrameRate);
            NPCid = NPCCounter;
            NPCCounter = (NPCCounter + 1) % MaxNPCs;
        }

        SearchResult = new NativeArray<int>(2, Allocator.Persistent);
        SearchResult[0] = 0;
        SearchResult[1] = 0;

        if (controller.CharacterController is not IObstacleAwareCharacterControler)
        {
            Debug.LogError("EnvironmentMotionMatchingSearch requires a character controller that implements IObstacleAwareCharacterControler.");
            return;
        }
        CharacterController = controller.CharacterController as IObstacleAwareCharacterControler;

        IsDisposed = false;
    }

    public override void OnEnabled()
    {
        if (IsNPC)
        {
            IsNPCFirst = true;
        }
    }

    public override void OnDisabled() { }

    public override bool ShouldSearch(MotionMatchingController controller)
    {
        return (!IsNPC && controller.SearchTimeLeft <= 0) || (IsNPC && Time.frameCount % MaxNPCs == NPCid) || IsNPCFirst;
    }

    public override int FindBestFrame(MotionMatchingController controller, float currentDistance)
    {
        if (IsDisposed) return controller.CurrentFrame;

        IsNPCFirst = false;

        (
            NativeArray<(float2, float, float2)> obstaclesCircles,
            NativeArray<int> obstaclesCirclesCount,
            NativeArray<(float2, float2, float2)> obstaclesEllipses,
            NativeArray<int> obstaclesEllipsesCount
        ) = CharacterController.GetNearbyObstacles(controller.SkeletonTransforms[0], ObstacleDistanceThreshold);

        if (obstaclesCircles.Length == 0 && obstaclesEllipses.Length == 0)
        {
            var job = new BVHMotionMatchingSearchBurst
            {
                Valid = controller.FeatureSet.GetValid(),
                TagMask = controller.TagMask,
                Features = controller.FeatureSet.GetFeatures(),
                QueryFeature = controller.QueryFeature,
                FeatureWeights = controller.FeaturesWeightsNativeArray,
                FeatureSize = controller.FeatureSet.FeatureSize,
                FeatureStaticSize = controller.FeatureSet.FeatureStaticSize,
                PoseOffset = controller.FeatureSet.PoseOffset,
                CurrentDistance = currentDistance,
                LargeBoundingBoxMin = LargeBoundingBoxMin,
                LargeBoundingBoxMax = LargeBoundingBoxMax,
                SmallBoundingBoxMin = SmallBoundingBoxMin,
                SmallBoundingBoxMax = SmallBoundingBoxMax,
                BestIndex = SearchResult
            };
            job.Schedule().Complete();
        }
        else
        {
            var jobCrowd = new CrowdHeightMotionMatchingSearchBurst
            {
                Valid = controller.FeatureSet.GetValid(),
                TagMask = controller.TagMask,
                Features = controller.FeatureSet.GetFeatures(),
                FeatureWeights = controller.FeaturesWeightsNativeArray,
                QueryFeature = controller.QueryFeature,
                AdaptativeFeaturesIndices = AdaptativeFeaturesIndices,
                ObstacleDistanceThreshold = ObstacleDistanceThreshold,
                CrowdSecondTrajectoryWeight = CrowdSecondTrajectoryWeight,
                CrowdThirdTrajectoryWeight = CrowdThirdTrajectoryWeight,
                Mean = Means,
                Std = Stds,
                ObstaclesCircles = obstaclesCircles,
                ObstaclesCirclesCount = obstaclesCirclesCount,
                ObstaclesEllipses = obstaclesEllipses,
                ObstaclesEllipsesCount = obstaclesEllipsesCount,
                FeatureSize = controller.FeatureSet.FeatureSize,
                FeatureStaticSize = controller.FeatureSet.FeatureStaticSize,
                BestIndex = SearchResult,
                PointsOnEllipse = PointsOnEllipse,
                PointsOnObstacle = PointsOnObstacle,
                ObstacleDistance = ObstacleDistances,
                ObstaclePenalization = ObstaclePenalization,
                NumberDebugPoints = VisualDebugElements,
                IsDebug = false,
                DynamicAccelerationConsts = EnvironmentAccelerationConsts,
            };
            jobCrowd.Schedule().Complete();
        }

        return SearchResult[0];
    }

    public override void OnSearchCompleted(MotionMatchingController controller)
    {
        if (IsDisposed) return;

        // At the end of the frame update, recompute features to display debug information
        (
            NativeArray<(float2, float, float2)> obstaclesCircles,
            NativeArray<int> obstaclesCirclesCount,
            NativeArray<(float2, float2, float2)> obstaclesEllipses,
            NativeArray<int> obstaclesEllipsesCount
        ) = CharacterController.GetNearbyObstacles(controller.SkeletonTransforms[0], ObstacleDistanceThreshold);

        if (obstaclesCircles.Length > 0 || obstaclesEllipses.Length > 0)
        {
            controller.FillQueryVector(); // Force to set obstacles local to the current character position
            var jobCrowd = new CrowdHeightMotionMatchingSearchBurst
            {
                Valid = controller.FeatureSet.GetValid(),
                TagMask = controller.TagMask,
                Features = controller.FeatureSet.GetFeatures(),
                FeatureWeights = controller.FeaturesWeightsNativeArray,
                QueryFeature = controller.QueryFeature,
                AdaptativeFeaturesIndices = AdaptativeFeaturesIndices,
                ObstacleDistanceThreshold = ObstacleDistanceThreshold,
                CrowdSecondTrajectoryWeight = CrowdSecondTrajectoryWeight,
                CrowdThirdTrajectoryWeight = CrowdThirdTrajectoryWeight,
                Mean = Means,
                Std = Stds,
                ObstaclesCircles = obstaclesCircles,
                ObstaclesCirclesCount = obstaclesCirclesCount,
                ObstaclesEllipses = obstaclesEllipses,
                ObstaclesEllipsesCount = obstaclesEllipsesCount,
                FeatureSize = controller.FeatureSet.FeatureSize,
                FeatureStaticSize = controller.FeatureSet.FeatureStaticSize,
                BestIndex = SearchResult,
                PointsOnEllipse = PointsOnEllipse,
                PointsOnObstacle = PointsOnObstacle,
                ObstacleDistance = ObstacleDistances,
                ObstaclePenalization = ObstaclePenalization,
                NumberDebugPoints = VisualDebugElements,
                IsDebug = true,
                DebugIndex = controller.CurrentFrame,
                DynamicAccelerationConsts = EnvironmentAccelerationConsts,
            };
            jobCrowd.Schedule().Complete();
        }
        else
        {
            VisualDebugElements[0] = 0;
        }

        // Adapt the direction weight based on the closest obstacle
        if (VisualDebugElements[0] > 0)
        {
            float closestObstacleDistance = float.MaxValue;
            for (int i = 0; i < VisualDebugElements[0]; i++)
            {
                float distance = ObstacleDistances[i];
                if (distance < closestObstacleDistance)
                {
                    closestObstacleDistance = distance;
                }
            }
            float distanceFactor = closestObstacleDistance / ObstacleDistanceThreshold; // 1.0f is the max distance, 0.0f is touching
            Debug.Assert(distanceFactor <= 1.0f, "Distance factor should be between 0.0f and 1.0f. If it is not, the obstacle is too close to the character."); ;
            distanceFactor = math.log10(distanceFactor) + 1.0f;
            controller.FeatureWeights[DirectionFeatureIndex] = math.lerp(controller.FeatureWeights[DirectionFeatureIndex],
                                                                         StartDirectionWeight * math.max(Evasion, distanceFactor),
                                                                         math.clamp(Time.deltaTime * 10.0f, 0.0f, 1.0f));
        }
        else
        {
            controller.FeatureWeights[DirectionFeatureIndex] = math.lerp(controller.FeatureWeights[DirectionFeatureIndex],
                                                                         StartDirectionWeight,
                                                                         math.clamp(Time.deltaTime * 100.0f, 0.0f, 1.0f));
        }

        for (int i = 0; i < VisualDebugElements[0]; i++)
        {
            // character space to world space
            PointsOnEllipse[i] = controller.SkeletonTransforms[0].TransformPoint(PointsOnEllipse[i]);
            PointsOnObstacle[i] = controller.SkeletonTransforms[0].TransformPoint(PointsOnObstacle[i]);
        }
    }

    public override float OnUpdateEnvironmentFeatureWeight(MotionMatchingController controller, TrajectoryFeature dynamicFeature, float defaultWeight)
    {
        return defaultWeight * Anticipation * math.max(0.5f, controller.CharacterController.GetTargetSpeed());
    }

    public override void Dispose()
    {
        if (SearchResult != null && SearchResult.IsCreated) SearchResult.Dispose();
        if (PointsOnEllipse != null && PointsOnEllipse.IsCreated) PointsOnEllipse.Dispose();
        if (PointsOnObstacle != null && PointsOnObstacle.IsCreated) PointsOnObstacle.Dispose();
        if (ObstacleDistances != null && ObstacleDistances.IsCreated) ObstacleDistances.Dispose();
        if (ObstaclePenalization != null && ObstaclePenalization.IsCreated) ObstaclePenalization.Dispose();
        if (VisualDebugElements != null && VisualDebugElements.IsCreated) VisualDebugElements.Dispose();
        if (Means != null && Means.IsCreated) Means.Dispose();
        if (Stds != null && Stds.IsCreated) Stds.Dispose();
        IsDisposed = true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (EnvironmentAccelerationConsts.PercentageThreshold < 0.001f)
        {
            EnvironmentAccelerationConsts.PercentageThreshold = 0.05f;
        }
        if (EnvironmentAccelerationConsts.MinimumStepSize < 1)
        {
            EnvironmentAccelerationConsts.MinimumStepSize = 8;
        }
        if (EnvironmentAccelerationConsts.VarianceFactor < 1.0f)
        {
            EnvironmentAccelerationConsts.VarianceFactor = 1.0f;
        }
    }

    public override void DrawGizmos(MotionMatchingController controller, float radius)
    {
        for (int i = 0; i < VisualDebugElements[0]; i++)
        {
            float3 dir = math.normalize(PointsOnObstacle[i] - PointsOnEllipse[i]);
            float3 pointOnDisk = PointsOnEllipse[i] + dir * ObstacleDistances[i];
            Gizmos.color = new Color(1.0f, 0.5f, 0.0f);
            Gizmos.DrawSphere(PointsOnEllipse[i], radius);
            Gizmos.DrawLine(PointsOnEllipse[i], pointOnDisk);
            Gizmos.DrawSphere(pointOnDisk, radius);
            //GUI.color = Color.red;
            //Handles.Label(PointsOnEllipse[i] + math.up() * radius * 2.0f, ObstaclePenalization[i].ToString("0.00"));
            //GUI.color = new Color(1.0f, 0.5f, 0.0f);
            //Handles.Label(PointsOnEllipse[i] + math.up() * radius * 3.0f, ObstacleDistances[i].ToString("0.0000"));
        }
    }
#endif
}