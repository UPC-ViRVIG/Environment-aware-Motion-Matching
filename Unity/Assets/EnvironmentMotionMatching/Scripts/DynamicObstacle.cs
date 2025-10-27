using MotionMatching;
using Unity.Mathematics;
using UnityEngine;

public class DynamicObstacle : MonoBehaviour
{
    public Obstacle Obstacle;
    public Obstacle FutureObstacle1;
    public Obstacle FutureObstacle2;
    public Obstacle FutureObstacle3;

    private float3 Velocity;
    private float3 LastPosition;

    private void Awake()
    {
        LastPosition = Obstacle.transform.position;
    }

    private void LateUpdate()
    {
        // Calculate the velocity of the dynamic obstacle
        Velocity = Velocity * 0.9f + ((float3)Obstacle.transform.position - LastPosition) * 0.1f;

        // Update the last position
        LastPosition = Obstacle.transform.position;

        FutureObstacle1.transform.position = Obstacle.transform.position + (Vector3)Velocity * 20.0f;
        FutureObstacle2.transform.position = Obstacle.transform.position + (Vector3)Velocity * 40.0f;
        FutureObstacle3.transform.position = Obstacle.transform.position + (Vector3)Velocity * 60.0f;
    }
}
