using MotionMatching;
using Unity.Mathematics;
using UnityEngine;

using Random = UnityEngine.Random;

public class CrowdDemoRandomController : MonoBehaviour
{
    public GameObject Prefab;
    public int Count = 10;
    public float MaxRadius = 5.0f;
    public float MaxTimer = 5.0f;
    public float MaxTimerVar = 2.0f;
    public float MaxSpeed = 1.0f;
    public float MaxSpeedVar = 0.25f;

    private CrowdCharacterController[] Controllers;
    private float[] Timers;

    private void Awake()
    {
        Controllers = new CrowdCharacterController[Count];
        Timers = new float[Count];

        for (int i = 0; i < Count; i++)
        {
            GameObject go = Instantiate(Prefab, transform.position, Quaternion.identity);
            go.transform.parent = transform;
            go.transform.localPosition = new Vector3(Random.Range(-MaxRadius, MaxRadius), 0.0f, Random.Range(-MaxRadius, MaxRadius));
            go.SetActive(true);
            Controllers[i] = go.GetComponentInChildren<CrowdCharacterController>();
            Timers[i] = 0.0f;
        }
    }

    private void Update()
    {
        for (int i = 0; i < Count; i++)
        {
            Timers[i] -= Time.deltaTime;

            if (Timers[i] <= 0.0f)
            {
                CrowdCharacterController controller = Controllers[i];

                float magnitude = Random.Range(0.3f, 1.2f);
                float2 direction = new(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
                float distanceToCenter = math.length(new float2(controller.transform.position.x, controller.transform.position.z));
                direction -= math.normalize(new float2(controller.transform.position.x, controller.transform.position.z)) * (distanceToCenter / MaxRadius);
                direction = math.normalize(direction);
                controller.SetMovementDirection(direction * magnitude);

                float speed = Random.Range(MaxSpeed - MaxSpeedVar, MaxSpeed + MaxSpeedVar);
                controller.MaxSpeed = speed;

                Timers[i] = Random.Range(MaxTimer - MaxTimerVar, MaxTimer + MaxTimerVar);
            }
        }
    }
}
