using UnityEngine;

public class SinusoidalMovingTransform : MonoBehaviour
{
    public float Speed = 1.0f;
    public float Amplitude = 1.0f;
    public float Frequency = 1.0f;
    public Vector3 Direction = Vector3.forward;

    private Vector3 StartPosition;

    private void Start()
    {
        StartPosition = transform.position;
    }

    private void Update()
    {
        float time = Time.time * Speed;
        float offset = Amplitude * Mathf.Sin(time * Frequency);
        Vector3 newPosition = StartPosition + offset * Direction.normalized;
        transform.position = newPosition;
    }
}
