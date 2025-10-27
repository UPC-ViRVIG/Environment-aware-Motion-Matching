//#define USE_FAST_DISTANCE

using Unity.Burst;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System.Collections.Generic;
using MotionMatching;

[BurstCompile]
public struct CrowdHeightMotionMatchingSearchBurst : IJob
{
    [ReadOnly] public NativeArray<bool> Valid;
    [ReadOnly] public NativeArray<bool> TagMask;
    [ReadOnly] public NativeArray<float> Features;
    [ReadOnly] public NativeArray<float> FeatureWeights;
    [ReadOnly] public NativeArray<float> QueryFeature;
    [ReadOnly] public NativeArray<int> AdaptativeFeaturesIndices;
    [ReadOnly] public float ObstacleDistanceThreshold;
    [ReadOnly] public float CrowdSecondTrajectoryWeight;
    [ReadOnly] public float CrowdThirdTrajectoryWeight;
    [ReadOnly] public NativeArray<float> Mean;
    [ReadOnly] public NativeArray<float> Std;
    [ReadOnly] public NativeArray<(float2, float, float2)> ObstaclesCircles; // (position, radius, height)
    [ReadOnly] public NativeArray<(float2, float2, float2)> ObstaclesEllipses;  // (position, primaryAxis, secondaryAxis)
    [ReadOnly] public NativeArray<int> ObstaclesCirclesCount;
    [ReadOnly] public NativeArray<int> ObstaclesEllipsesCount;
    [ReadOnly] public int NumberOfFeatures;
    [ReadOnly] public int FeatureSize;
    [ReadOnly] public int FeatureStaticSize;
    [ReadOnly] public EnvironmentAccelerationConsts DynamicAccelerationConsts;

    public NativeArray<int> BestIndex;

    // Debug Visuals
    [WriteOnly] public NativeArray<float3> PointsOnEllipse;
    [WriteOnly] public NativeArray<float3> PointsOnObstacle;
    [WriteOnly] public NativeArray<float> ObstacleDistance;
    [WriteOnly] public NativeArray<float> ObstaclePenalization;
    public NativeArray<int> NumberDebugPoints;
    [ReadOnly] public bool IsDebug;
    [ReadOnly] public int DebugIndex;

    private float StaticSqrDistance(int i)
    {
        float sqrDistance = 0.0f;
        int featureIndex = i * FeatureSize;

        for (int j = 0; j < FeatureStaticSize; ++j)
        {
            float diff = Features[featureIndex + j] - QueryFeature[j];
            sqrDistance += diff * diff * FeatureWeights[j];
        }

        return sqrDistance;
    }

    private float DistanceFunction(float distance, float threshold)
    {
        if (distance > threshold)
        {
            return 0.0f;
        }
        return -math.pow((threshold - distance), 4.0f) * math.log(math.max(distance / threshold, UtilitiesBurst.MAX_INSIDE_ELLIPSE));
    }

    private float ComputePenalizationCircles(float2 centerEllipse, float2 primaryAxisUnit, float2 secondaryAxisUnit, float2 ellipse, int obstacle, float penalizationFactor, bool saveDebug)
    {
        const int maxIterationsRootFinder = 5;

#if USE_FAST_DISTANCE
        const float fastDistanceAngle = 30.0f;
        float distance = UtilitiesBurst.FastDistancePointToEllipse(centerEllipse, primaryAxisUnit, secondaryAxisUnit, ellipse, ObstaclesCircles[obstacle].Item1, out float2 closest, fastDistanceAngle);
#else
        float distance = UtilitiesBurst.DistancePointToEllipse(centerEllipse, primaryAxisUnit, secondaryAxisUnit, ellipse, ObstaclesCircles[obstacle].Item1, out float2 closest, maxIterationsRootFinder);
#endif
        distance = math.max(distance - ObstaclesCircles[obstacle].Item2, UtilitiesBurst.MAX_INSIDE_ELLIPSE);
        float penalization = DistanceFunction(distance, ObstacleDistanceThreshold) * penalizationFactor;
        
        if (saveDebug && distance < ObstacleDistanceThreshold)
        {
            PointsOnEllipse[NumberDebugPoints[0]] = new float3(closest.x, 0.0f, closest.y);
            PointsOnObstacle[NumberDebugPoints[0]] = new float3(ObstaclesCircles[obstacle].Item1.x, 0.0f, ObstaclesCircles[obstacle].Item1.y);
            ObstacleDistance[NumberDebugPoints[0]] = distance;
            ObstaclePenalization[NumberDebugPoints[0]] = penalization;
            NumberDebugPoints[0] += 1;
        }
        return penalization;
    }

    private float ComputePenalizationEllipse(float2 centerEllipse, float2 primaryAxisUnit, float2 secondaryAxisUnit, float2 ellipse, int obstacle, float penalizationFactor, bool saveDebug)
    {
        const int maxIterationsRootFinder = 5;
        const float fastDistanceAngle = 30.0f;

        float2 centerEllipse2 = ObstaclesEllipses[obstacle].Item1;
        float2 primaryAxis2 = ObstaclesEllipses[obstacle].Item2;
        float2 secondaryAxis2 = ObstaclesEllipses[obstacle].Item3;
        float2 ellipse2 = new(math.length(primaryAxis2), math.length(secondaryAxis2));

#if USE_FAST_DISTANCE
            float distance = UtilitiesBurst.FastDistanceEllipseToEllipse(centerEllipse, primaryAxisUnit, secondaryAxisUnit, ellipse,
                                                                         centerEllipse2, primaryAxis2 / ellipse2.x, secondaryAxis2 / ellipse2.y, ellipse2,
                                                                         out float2 closest1, out float2 closest2, fastDistanceAngle);
#else
        float distance = UtilitiesBurst.DistanceEllipseToEllipse(centerEllipse, primaryAxisUnit, secondaryAxisUnit, ellipse,
                                                                 centerEllipse2, primaryAxis2 / ellipse2.x, secondaryAxis2 / ellipse2.y, ellipse2,
                                                                 out float2 closest1, out float2 closest2, fastDistanceAngle, maxIterationsRootFinder);
#endif
        float penalization = DistanceFunction(distance, ObstacleDistanceThreshold) * penalizationFactor;
        
        if (saveDebug && distance < ObstacleDistanceThreshold)
        {
            PointsOnEllipse[NumberDebugPoints[0]] = new float3(closest1.x, 0.0f, closest1.y);
            PointsOnObstacle[NumberDebugPoints[0]] = new float3(closest2.x, 0.0f, closest2.y);
            ObstacleDistance[NumberDebugPoints[0]] = distance;
            ObstaclePenalization[NumberDebugPoints[0]] = penalization;
            NumberDebugPoints[0] += 1;
        }
        return penalization;
    }

    private bool HeightOverlap(float2 agentHeight, float2 obstacleHeight)
    {
        return !(agentHeight.x > obstacleHeight.y || agentHeight.y < obstacleHeight.x);
    }

    private float FeatureCheck(int i, float minDistance, float sqrDistance, bool saveDebug)
    {
        int featureIndex = i * FeatureSize;

        float2 pos1 = new(Features[featureIndex + 0] * Std[0] + Mean[0],
                          Features[featureIndex + 1] * Std[1] + Mean[1]);
        float2 pos2 = new(Features[featureIndex + 2] * Std[2] + Mean[2],
                          Features[featureIndex + 3] * Std[3] + Mean[3]);
        float2 pos3 = new(Features[featureIndex + 4] * Std[4] + Mean[4],
                          Features[featureIndex + 5] * Std[5] + Mean[5]);

        int featureStaticIndex = featureIndex + FeatureStaticSize;
        float3 ellipseFeatures1 = new(Features[featureStaticIndex + 0], Features[featureStaticIndex + 1], Features[featureStaticIndex + 2]);
        float3 ellipseFeatures2 = new(Features[featureStaticIndex + 3], Features[featureStaticIndex + 4], Features[featureStaticIndex + 5]);
        float3 ellipseFeatures3 = new(Features[featureStaticIndex + 6], Features[featureStaticIndex + 7], Features[featureStaticIndex + 8]);

        float2 primaryAxisUnit1 = math.normalize(ellipseFeatures1.xy);
        float2 primaryAxisUnit2 = math.normalize(ellipseFeatures2.xy);
        float2 primaryAxisUnit3 = math.normalize(ellipseFeatures3.xy);

        float2 secondaryAxisUnit1 = new(-primaryAxisUnit1.y, primaryAxisUnit1.x);
        float2 secondaryAxisUnit2 = new(-primaryAxisUnit2.y, primaryAxisUnit2.x);
        float2 secondaryAxisUnit3 = new(-primaryAxisUnit3.y, primaryAxisUnit3.x);

        float2 ellipse1 = new(math.length(ellipseFeatures1.xy), ellipseFeatures1.z);
        float2 ellipse2 = new(math.length(ellipseFeatures2.xy), ellipseFeatures2.z);
        float2 ellipse3 = new(math.length(ellipseFeatures3.xy), ellipseFeatures3.z);

        float2 height1 = new(Features[featureStaticIndex + 9], Features[featureStaticIndex + 10]);
        float2 height2 = new(Features[featureStaticIndex + 11], Features[featureStaticIndex + 12]);
        float2 height3 = new(Features[featureStaticIndex + 13], Features[featureStaticIndex + 14]);

        float debugTotalCrowdDistance = 0.0f;
        int obstacleCircleIt = 0;
        int obstacleEllipseIt = 0;
        for (int p = 0; p < ObstaclesCirclesCount[0]; p++)
        {
            if (HeightOverlap(height1, ObstaclesCircles[obstacleCircleIt].Item3))
            {
                float penalization1 = ComputePenalizationCircles(pos1, primaryAxisUnit1, secondaryAxisUnit1, ellipse1, obstacleCircleIt, 1.0f, saveDebug);
                debugTotalCrowdDistance += penalization1;
                sqrDistance += penalization1 * FeatureWeights[FeatureStaticSize];
                if (!saveDebug && sqrDistance > minDistance)
                {
                    return minDistance;
                }
            }
            obstacleCircleIt += 1;
        }
        for (int p = 0; p < ObstaclesEllipsesCount[0]; p++)
        {
            float penalization1 = ComputePenalizationEllipse(pos1, primaryAxisUnit1, secondaryAxisUnit1, ellipse1, obstacleEllipseIt, 1.0f, saveDebug);
            debugTotalCrowdDistance += penalization1;
            sqrDistance += penalization1 * FeatureWeights[FeatureStaticSize];
            if (!saveDebug && sqrDistance > minDistance)
            {
                return minDistance;
            }
            obstacleEllipseIt += 1;
        }

        for (int p = 0; p < ObstaclesCirclesCount[1]; p++)
        {
            if (HeightOverlap(height2, ObstaclesCircles[obstacleCircleIt].Item3))
            {
                float penalization2 = ComputePenalizationCircles(pos2, primaryAxisUnit2, secondaryAxisUnit2, ellipse2, obstacleCircleIt, CrowdSecondTrajectoryWeight, saveDebug);
                debugTotalCrowdDistance += penalization2;
                sqrDistance += penalization2 * FeatureWeights[FeatureStaticSize];
                if (!saveDebug && sqrDistance > minDistance)
                {
                    return minDistance;
                }
            }
            obstacleCircleIt += 1;
        }
        for (int p = 0; p < ObstaclesEllipsesCount[1]; p++)
        {
            float penalization2 = ComputePenalizationEllipse(pos2, primaryAxisUnit2, secondaryAxisUnit2, ellipse2, obstacleEllipseIt, CrowdSecondTrajectoryWeight, saveDebug);
            debugTotalCrowdDistance += penalization2;
            sqrDistance += penalization2 * FeatureWeights[FeatureStaticSize];
            if (!saveDebug && sqrDistance > minDistance)
            {
                return minDistance;
            }
            obstacleEllipseIt += 1;
        }

        for (int p = 0; p < ObstaclesCirclesCount[2]; p++)
        {
            if (HeightOverlap(height3, ObstaclesCircles[obstacleCircleIt].Item3))
            {
                float penalization3 = ComputePenalizationCircles(pos3, primaryAxisUnit3, secondaryAxisUnit3, ellipse3, obstacleCircleIt, CrowdThirdTrajectoryWeight, saveDebug);
                debugTotalCrowdDistance += penalization3;
                sqrDistance += penalization3 * FeatureWeights[FeatureStaticSize];
                if (!saveDebug && sqrDistance > minDistance)
                {
                    return minDistance;
                }
            }
            obstacleCircleIt += 1;
        }
        for (int p = 0; p < ObstaclesEllipsesCount[2]; p++)
        {
            float penalization3 = ComputePenalizationEllipse(pos3, primaryAxisUnit3, secondaryAxisUnit3, ellipse3, obstacleEllipseIt, CrowdThirdTrajectoryWeight, saveDebug);
            debugTotalCrowdDistance += penalization3;
            sqrDistance += penalization3 * FeatureWeights[FeatureStaticSize];
            if (!saveDebug && sqrDistance > minDistance)
            {
                return minDistance;
            }
            obstacleEllipseIt += 1;
        }

        if (sqrDistance < minDistance)
        {
            minDistance = sqrDistance;
            BestIndex[0] = i;
        }

        return minDistance;
    }

    public float Search(int aStart, int aEnd, float minDistance)
    {
        for (int a = aStart; a < aEnd; a++)
        {
            int i = AdaptativeFeaturesIndices[a];
            float staticSqrDistance = StaticSqrDistance(i);
            if (staticSqrDistance < minDistance)
            {
                float newMinDistance = FeatureCheck(i, minDistance, staticSqrDistance, false);
                if (newMinDistance < minDistance)
                {
                    BestIndex[1] = a;
                    minDistance = newMinDistance;
                    int jStart = (AdaptativeFeaturesIndices[math.max(0, a - 1)] + i) / 2;
                    int jEnd = (AdaptativeFeaturesIndices[math.min(AdaptativeFeaturesIndices.Length - 1, a + 1)] + i) / 2;
                    for (int j = jStart; j < jEnd; j++)
                    {
                        if (j != i && Valid[j] && TagMask[j])
                        {
                            staticSqrDistance = StaticSqrDistance(j);
                            minDistance = FeatureCheck(j, minDistance, staticSqrDistance, false);
                        }
                    }
                }
            }
        }

        return minDistance;
    }

    public void Execute()
    {
        if (IsDebug)
        {
            NumberDebugPoints[0] = 0;
            float staticSqrDistance = StaticSqrDistance(DebugIndex);
            FeatureCheck(DebugIndex, float.MinValue, staticSqrDistance, true);
        }
        else
        {
            float minDistance = float.MaxValue;
            BestIndex[0] = -1;

            int aStart = math.max(0, BestIndex[1] - (int)math.floor(AdaptativeFeaturesIndices.Length * 0.01f));
            minDistance = Search(aStart, AdaptativeFeaturesIndices.Length, minDistance);
            minDistance = Search(0, math.max(0, aStart), minDistance);
        }
    }
}