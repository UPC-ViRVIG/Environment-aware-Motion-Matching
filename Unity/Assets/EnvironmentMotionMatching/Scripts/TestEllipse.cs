using MotionMatching;
using Unity.Burst;
using Unity.Mathematics;
using UnityEditor.Search;
using UnityEngine;

public class TestEllipse : MonoBehaviour
{
    public float3 Center;
    public float3 Center2;
    public float2 PrimaryAxis;
    public float2 PrimaryAxis2;
    public float SecondaryAxisMagnitude;
    public float SecondaryAxisMagnitude2;
    public bool Normalize;

    public float Distance;
    public float FastDistance;

    void OnDrawGizmos()
    {
        const float pointRadius = 0.05f;
        Gizmos.color = Color.red;

        float2 primaryAxis = math.normalize(PrimaryAxis);
        float2 primaryAxis2 = math.normalize(PrimaryAxis2);

        float2 secondaryAxis = new(-primaryAxis.y, primaryAxis.x);
        GizmosExtensions.DrawWireEllipse(Center, PrimaryAxis, secondaryAxis * SecondaryAxisMagnitude, Quaternion.identity);

        float2 secondaryAxis2 = new(-primaryAxis2.y, primaryAxis2.x);
        GizmosExtensions.DrawWireEllipse(Center2, PrimaryAxis2, secondaryAxis2 * SecondaryAxisMagnitude2, Quaternion.identity);

        float distance = UtilitiesBurst.DistanceEllipseToEllipse(new float2(Center.x, Center.z), primaryAxis, secondaryAxis, new float2(math.length(PrimaryAxis), SecondaryAxisMagnitude),
                                                  new float2(Center2.x, Center2.z), primaryAxis2, secondaryAxis2, new float2(math.length(PrimaryAxis2), SecondaryAxisMagnitude2),
                                                  out float2 closest1, out float2 closest2);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(new float3(closest1.x, 0.0f, closest1.y), pointRadius);
        Gizmos.DrawSphere(new float3(closest2.x, 0.0f, closest2.y), pointRadius);

        float fastDistance = UtilitiesBurst.FastDistanceEllipseToEllipse(new float2(Center.x, Center.z), primaryAxis, secondaryAxis, new float2(math.length(PrimaryAxis), SecondaryAxisMagnitude),
                                                          new float2(Center2.x, Center2.z), primaryAxis2, secondaryAxis2, new float2(math.length(PrimaryAxis2), SecondaryAxisMagnitude2),
                                                          out float2 closestFast1, out float2 closestFast2);

        Gizmos.color = new Color(0.7f, 0.7f, 0.0f);
        Gizmos.DrawSphere(new float3(closestFast1.x, 0.0f, closestFast1.y), pointRadius);
        Gizmos.DrawSphere(new float3(closestFast2.x, 0.0f, closestFast2.y), pointRadius);

        Distance = distance;
        FastDistance = fastDistance;
    }
}
