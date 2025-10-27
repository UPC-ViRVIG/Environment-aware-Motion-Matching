using MotionMatching;
using Unity.Mathematics;
using UnityEngine;
using Joint = MotionMatching.Skeleton.Joint;

[CreateAssetMenu(fileName = "EllipseFeatureExtractor", menuName = "Custom/EllipseFeatureExtractor")]
public class EllipseFeatureExtractor : Feature3DExtractor
{
    private Joint HipsJoint;

    public override void StartExtracting(Skeleton skeleton)
    {
        bool found = skeleton.Find(HumanBodyBones.Hips, out HipsJoint);
        Debug.Assert(found, "Hips Joint could not be found");
    }

    public override float3 ExtractFeature(PoseVector pose, int poseIndex, PoseVector nextPose, int animationClip, Skeleton skeleton, float3 characterOrigin, float3 characterForward)
    {
        float3 worldHipsPos = FeatureSet.GetWorldPosition(skeleton, pose, HipsJoint);
        float2 worldProjHipsPos = new(worldHipsPos.x, worldHipsPos.z);
        float3 nextWorldHipsPos = FeatureSet.GetWorldPosition(skeleton, nextPose, HipsJoint);
        float2 nextWorldProjHipsPos = new(nextWorldHipsPos.x, nextWorldHipsPos.z);

        float2 displVector = nextWorldProjHipsPos - worldProjHipsPos;
        if (math.length(displVector) < 1e-2)
        {
            displVector = new(characterForward.x, characterForward.z);
        }
        float2 primaryAxis = math.normalize(displVector);
        float2 secondaryAxis = new(-primaryAxis.y, primaryAxis.x);

        float maxPrimaryDistance = 0.0f;
        float maxSecondaryDistance = 0.0f;
        foreach (Joint joint in skeleton.Joints)
        {
            float3 worldPos = FeatureSet.GetWorldPosition(skeleton, pose, joint);
            float2 projWorldPos = new(worldPos.x, worldPos.z);

            // Calculate the vector from the hips to the joint in the projected plane
            float2 jointVector = projWorldPos - worldProjHipsPos;

            // Project the joint vector onto the primary axis
            float primaryProjection = math.dot(jointVector, primaryAxis);
            // Project the joint vector onto the secondary axis
            float secondaryProjection = math.dot(jointVector, secondaryAxis);

            maxPrimaryDistance = math.max(maxPrimaryDistance, math.abs(primaryProjection));
            maxSecondaryDistance = math.max(maxSecondaryDistance, math.abs(secondaryProjection));
        }

        float3 primaryAxisCharacterSpace = FeatureSet.GetLocalDirectionFromCharacter(new float3(primaryAxis.x, 0.0f, primaryAxis.y), characterForward);

        return new float3(maxPrimaryDistance * math.normalize(new float2(primaryAxisCharacterSpace.x, primaryAxisCharacterSpace.z)), maxSecondaryDistance);
    }

    public override void DrawGizmos(float3 feature, float radius, float3 characterOrigin, float3 characterForward, Transform[] joints, Skeleton skeleton, float3 posOffset)
    {
        float ellipsePrimaryMagnitude = math.length(new float2(feature.x, feature.z));
        float3 primaryAxisCharacterSpace = math.normalize(new float3(feature.x, 0.0f, feature.y));
        float3 primaryAxis3D = FeatureSet.GetWorldDirectionFromCharacter(primaryAxisCharacterSpace, characterForward);
        float2 primaryAxis = new(primaryAxis3D.x, primaryAxis3D.z);
        float2 secondaryAxis = new(-primaryAxis.y, primaryAxis.x);

        GizmosExtensions.DrawWireEllipse(posOffset, primaryAxis * ellipsePrimaryMagnitude, secondaryAxis * feature.z, Quaternion.identity, segments: 40, thickness: 3.0f);
    }
}
