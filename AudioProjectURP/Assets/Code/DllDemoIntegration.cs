using System.Runtime.InteropServices;
using UnityEngine;

public class DllDemoIntegration : MonoBehaviour
{
    [DllImport("MyLibrary")]
    private static extern int add(int a, int b);

    void Start()
    {
        int result = add(3, 4);
        Debug.Log("3 + 4 = " + result);
    }
}