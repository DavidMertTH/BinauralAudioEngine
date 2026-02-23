using UnityEngine;

public class DemoGraph : MonoBehaviour
{
    public GraphRenderer graph;
    public int sampleCount = 4096;

    private float[] data;
    private float   time;

    void Start()
    {
        data = new float[sampleCount];
    }

    void Update()
    {
        time += Time.deltaTime;

        // Beispieldaten: überlagerte Sinuswellen
        for (int i = 0; i < sampleCount; i++)
        {
            float x = i / (float)sampleCount;
            data[i] = Mathf.Sin(x * 20f + time)
                      + Mathf.Sin(x * 73f + time * 0.5f) * 0.3f;
        }

        graph.SetData (data);
    }
}