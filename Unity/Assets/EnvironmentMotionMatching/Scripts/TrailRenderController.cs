using Unity.Mathematics;
using UnityEngine;

public class TrailRenderController : MonoBehaviour
{
    private Material Material;
    private void Start()
    {
        Material = GetComponent<TrailRenderer>().material;
        float3 hsv = new(UnityEngine.Random.Range(0f, 1f), 1.0f, 1.0f);
        Color randomColor = Color.HSVToRGB(hsv.x, hsv.y, hsv.z);
        Material.color = randomColor;
    }

    private void OnDestroy()
    {
        if (Material != null)
        {
            Destroy(Material);
        }
    }
}
