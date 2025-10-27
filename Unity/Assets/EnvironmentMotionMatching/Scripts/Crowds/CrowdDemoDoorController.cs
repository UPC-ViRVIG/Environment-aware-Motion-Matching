using MotionMatching;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

[DefaultExecutionOrder(-100)]
public class CrowdDemoDoorController : MonoBehaviour
{
    public List<GameObject> Prefab;
    public float Speed = 1.0f;
    public int Rows = 3;
    public int Columns = 3;
    [Header("Spline")]
    public float RowSize = 3.0f;
    public float ColumnSize = 3.0f;


    private static int NextAgentID = 0;
    [ContextMenu("Prepare Scene")]
    private void PrepareScene()
    {
        // Delete all children except for the prefab
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (i != 0)
            {
                DestroyImmediate(child.gameObject);
            }
        }

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                GameObject prefab = Prefab[NextAgentID];
                NextAgentID = (NextAgentID + 1) % Prefab.Count;
                GameObject go = Instantiate(prefab, transform.position, Quaternion.identity);
                go.transform.parent = transform;
                go.SetActive(true);
                CrowdSplineCharacterController controller = go.GetComponentInChildren<CrowdSplineCharacterController>();
                controller.Speed = Speed;
                float3 startOffset = new(-r * RowSize, 0.0f, (c - Columns / 2) * ColumnSize);
                float3 endOffset = new((Rows - r) * RowSize, 0.0f, (c - Columns / 2) * ColumnSize);
                Spline spline = controller.SplineContainer.Spline;
                BezierKnot[] knots = spline.ToArray();
                knots[0].Position += (float3)controller.SplineContainer.transform.InverseTransformDirection(startOffset);
                knots[^1].Position += (float3)controller.SplineContainer.transform.InverseTransformDirection(endOffset);
                spline.Knots = knots;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
