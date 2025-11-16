using UnityEngine;
using UnityEngine.Serialization;

public class MaterialLookUp : MonoBehaviour
{
    public static MaterialLookUp instance;
    
    public Material primary;
    public Material secondary;
    public Material dark;
    public Material light;
    public Material highlight;
    void Awake()
    {
        if(instance == null)instance = this;
        else Destroy(gameObject);
    }
}
