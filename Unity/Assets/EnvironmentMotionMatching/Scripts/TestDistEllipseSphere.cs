using MotionMatching;
using Unity.Mathematics;
using UnityEngine;

public class TestDistEllipseSphere : MonoBehaviour
{
    public float2 CenterEllipse;
    public float2 CenterPoint;
    public float2 PrimaryAxis;
    public float Radius;
    public float SecondaryAxisMagnitude;
    public bool Normalize;
    public float FastAngle = 90.0f;
    public float Distance;
    public float FastDistance;

    private void Update()
    {
        if (Normalize)
        {
            PrimaryAxis = math.normalize(PrimaryAxis);
            Normalize = false;
        }
    }

    void OnDrawGizmos()
    {
        const float pointRadius = 0.05f;
        Gizmos.color = Color.red;

        float2 primaryAxisUnit = math.normalize(PrimaryAxis);
        float2 secondaryAxisUnit = new(-primaryAxisUnit.y, primaryAxisUnit.x);
        GizmosExtensions.DrawWireEllipse(new float3(CenterEllipse.x, 0.0f, CenterEllipse.y), PrimaryAxis, secondaryAxisUnit * SecondaryAxisMagnitude, Quaternion.identity);
        Gizmos.DrawLine(new float3(CenterEllipse.x, 0.0f, CenterEllipse.y), new float3(CenterEllipse.x + PrimaryAxis.x, 0.0f, CenterEllipse.y + PrimaryAxis.y));
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(new float3(CenterEllipse.x, 0.0f, CenterEllipse.y), new float3(CenterEllipse.x + secondaryAxisUnit.x * SecondaryAxisMagnitude, 0.0f, CenterEllipse.y + secondaryAxisUnit.y * SecondaryAxisMagnitude));
        Gizmos.color = Color.red;
        GizmosExtensions.DrawWireCircle(new float3(CenterPoint.x, 0.0f, CenterPoint.y), Radius, Quaternion.identity);
        Gizmos.DrawSphere(new float3(CenterPoint.x, 0.0f, CenterPoint.y), pointRadius);

        float2 ellipse = new(math.length(PrimaryAxis), SecondaryAxisMagnitude);
        float distance = UtilitiesBurst.DistancePointToEllipse(CenterEllipse, primaryAxisUnit, secondaryAxisUnit, ellipse, CenterPoint, out float2 closest);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(new float3(closest.x, 0.0f, closest.y), pointRadius);
        float fastDistance = UtilitiesBurst.FastDistancePointToEllipse(CenterEllipse, primaryAxisUnit, secondaryAxisUnit, ellipse, CenterPoint, out float2 closestFast, FastAngle);
        Gizmos.color = new Color(0.7f, 0.7f, 0.0f);
        Gizmos.DrawSphere(new float3(closestFast.x, 0.0f, closestFast.y), pointRadius);

        Distance = distance;
        FastDistance = fastDistance;

        //Debug.Log("Distance: " + distance + " - " + math.distance(closest, CenterPoint));
    }
}
