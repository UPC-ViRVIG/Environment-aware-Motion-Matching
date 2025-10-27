using MotionMatching;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class CrowdDemoCircleController : MonoBehaviour
{
    public List<GameObject> Prefab;
    public float Speed = 1.0f;
    public float Radius = 5.0f;
    public int Count = 10;

    private CrowdCharacterController[] Controllers;
    private float2[] Directions;

    private void Awake()
    {
        Controllers = new CrowdCharacterController[Count];
        Directions = new float2[Count];

        for (int i = 0; i < Count; i++)
        {
            GameObject prefab = Prefab[UnityEngine.Random.Range(0, Prefab.Count)];
            GameObject go = Instantiate(prefab, transform.position, Quaternion.identity);
            go.transform.parent = transform;
            go.transform.localPosition = new Vector3(Radius * Mathf.Cos(i * 2.0f * Mathf.PI / Count), 0.0f, Radius * Mathf.Sin(i * 2.0f * Mathf.PI / Count));
            go.transform.LookAt(transform.position);
            go.SetActive(true);
            Controllers[i] = go.GetComponentInChildren<CrowdCharacterController>();
            Controllers[i].MaxSpeed = Speed;
            Directions[i] = math.normalize(new float2(go.transform.forward.x, go.transform.forward.z));
        }
    }

    private void Update()
    {
        for (int i = 0; i < Count; i++)
        {
            Controllers[i].SetMovementDirection(Directions[i] * Speed);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}
