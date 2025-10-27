using MotionMatching;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CrowdDemoCorridorController : MonoBehaviour
{
    public List<GameObject> Prefab;
    public float Speed = 1.0f;
    public float SpeedVar = 0.25f;
    public float WalkDistance = 20.0f; // distance to walk before destroying the agent
    public Vector2 SpawnSize = new Vector2(5.0f, 2.0f);
    public float SpawnRate = 3.0f; // spawn every SpawnRate seconds

    private List<CrowdCharacterController> Controllers;
    private float LastSpawnTime;

    private void Awake()
    {
        Controllers = new List<CrowdCharacterController>();
        LastSpawnTime = -SpawnRate;
    }

    private void Update()
    {
        if (Time.time - LastSpawnTime > SpawnRate)
        {
            GameObject prefab = Prefab[UnityEngine.Random.Range(0, Prefab.Count)];
            GameObject go = Instantiate(prefab, transform.position, Quaternion.identity);
            go.transform.parent = transform;
            go.transform.localPosition = new Vector3(UnityEngine.Random.Range(-SpawnSize.x, SpawnSize.x), 0.0f, UnityEngine.Random.Range(-SpawnSize.y, SpawnSize.y));
            go.transform.rotation = transform.rotation;
            go.SetActive(true);
            CrowdCharacterController controller = go.GetComponentInChildren<CrowdCharacterController>();
            float speed = UnityEngine.Random.Range(Speed - SpeedVar, Speed + SpeedVar);
            controller.MaxSpeed = speed;
            controller.SetMovementDirection(new float2(go.transform.forward.x, go.transform.forward.z) * speed);
            Controllers.Add(controller);
            LastSpawnTime = Time.time;
        }

        for (int i = 0; i < Controllers.Count; i++)
        {
            CrowdCharacterController controller = Controllers[i];
            if (math.distance(new float2(controller.transform.position.x, controller.transform.position.z),
                                             new float2(transform.position.x, transform.position.z)) > WalkDistance)
            {
                Destroy(controller.transform.parent.gameObject);
                Controllers.RemoveAt(i--);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(SpawnSize.x * 2.0f, 0.0f, SpawnSize.y * 2.0f));
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * WalkDistance);
    }
}
