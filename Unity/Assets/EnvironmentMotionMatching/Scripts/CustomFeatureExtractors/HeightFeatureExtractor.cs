using MotionMatching;
using Unity.Mathematics;
using UnityEngine;
using Joint = MotionMatching.Skeleton.Joint;

[CreateAssetMenu(fileName = "HeightFeatureExtractor", menuName = "Custom/HeightFeatureExtractor")]
public class HeightFeatureExtractor : Feature2DExtractor
{
    private Joint HipsJoint;

    public override void StartExtracting(Skeleton skeleton)
    {
        bool found = skeleton.Find(HumanBodyBones.Hips, out HipsJoint);
        Debug.Assert(found, "Hips Joint could not be found");
    }

    public override float2 ExtractFeature(PoseVector pose, int poseIndex, PoseVector nextPose, int animationClip, Skeleton skeleton, float3 characterOrigin, float3 characterForward)
    {
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        foreach (Joint joint in skeleton.Joints)
        {
            if (joint.Name == "SimulationBone")
            {
                continue;
            }

            float3 worldPos = FeatureSet.GetWorldPosition(skeleton, pose, joint);
            
            float relativeHeight = worldPos.y - characterOrigin.y;

            minHeight = math.min(minHeight, relativeHeight);
            maxHeight = math.max(maxHeight, relativeHeight);
        }

        float2 feature = new(minHeight, maxHeight);
        return feature;
    }

    public override void DrawGizmos(float2 feature, float radius, float3 characterOrigin, float3 characterForward, Transform[] joints, Skeleton skeleton, float3 posOffset)
    {
        // index 1 is the hips joint
        float3 minHeight = posOffset + new float3(0, feature.x, 0);
        float3 maxHeight = posOffset + new float3(0, feature.y, 0);
        Gizmos.DrawLine(minHeight, maxHeight);
    }
}
