using UnityEngine;
using UnityEngine.Rendering.Universal;


public sealed class RandomLightFlickerGroup2D : MonoBehaviour
{
    [Header("Random time ranges")]
    [SerializeField, Min(0.01f)] private float minimumOnTime = 0.08f;
    [SerializeField, Min(0.01f)] private float maximumOnTime = 0.35f;
    [SerializeField, Min(0.01f)] private float minimumOffTime = 0.03f;
    [SerializeField, Min(0.01f)] private float maximumOffTime = 0.18f;

    private Light2D[] lights;
    private float[] timers;
    private bool[] lightIsOn;

    private void Awake()
    {
        lights = GetComponentsInChildren<Light2D>(true);
        timers = new float[lights.Length];
        lightIsOn = new bool[lights.Length];

        for (int i = 0; i < lights.Length; i++)
        {
            lightIsOn[i] = Random.value >= 0.5f;
            lights[i].enabled = lightIsOn[i];
            timers[i] = GetRandomDuration(lightIsOn[i]);
        }
    }

    private void Update()
    {
        for (int i = 0; i < lights.Length; i++)
        {
            timers[i] -= Time.deltaTime;

            if (timers[i] > 0f)
                continue;

            lightIsOn[i] = !lightIsOn[i];
            lights[i].enabled = lightIsOn[i];
            timers[i] = GetRandomDuration(lightIsOn[i]);
        }
    }

    private float GetRandomDuration(bool isOn)
    {
        return isOn
            ? Random.Range(minimumOnTime, maximumOnTime)
            : Random.Range(minimumOffTime, maximumOffTime);
    }

    private void OnValidate()
    {
        maximumOnTime = Mathf.Max(minimumOnTime, maximumOnTime);
        maximumOffTime = Mathf.Max(minimumOffTime, maximumOffTime);
    }
}
