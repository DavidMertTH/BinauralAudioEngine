using UnityEngine;

public class FillGraph : MonoBehaviour
{
    public Material graphMaterial;
    public float[] data;

    ComputeBuffer buffer;

    void Start()
    {
        data = new float[graphMaterial.mainTexture.width];
        for (int i = 0; i < graphMaterial.mainTexture.width; i++)
        {
            data[i] = Random.Range(-1.0f, 1.0f);
        }
        if (data == null || data.Length == 0) return;

        
        buffer = new ComputeBuffer(data.Length, sizeof(float));
        buffer.SetData(data);

        graphMaterial.SetBuffer("_Samples", buffer);
        graphMaterial.SetInt("_SampleCount", data.Length);
    }
    
    void OnDestroy()
    {
        if (buffer != null) buffer.Release();
    }
}