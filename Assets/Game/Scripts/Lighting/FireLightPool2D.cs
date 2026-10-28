using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class FireLightPool2D : MonoBehaviour
{
    [SerializeField]
    private Light2D[] lights;

    private void Awake()
    {
        //setup lights and disable all of them
        int count = lights == null ? 0 : lights.Length;

        for (int i = 0; i < count; i++)
        {
            if (lights[i] != null)
            {
                lights[i].enabled = false;
            }
        }

        //kept so old scenes do not get a missing script
        //cellworld lighting now handles all fire light
        enabled = false;
    }
}
