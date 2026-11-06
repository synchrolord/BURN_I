using UnityEngine;
using UnityEngine.Rendering.Universal;


public sealed class RandomLightFlickerGroup2D : MonoBehaviour
{
    [SerializeField, Min(0f)] private float intensity = 1f;
    private float appliedIntensity = -1f;

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
        ApplyIntensity();
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
        if (intensity != appliedIntensity) ApplyIntensity();

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
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
        ApplyIntensity();
    }

    private void ApplyIntensity()
    {
        //include switched-off lights so they use the same brightness when they flicker on
        Light2D[] children = GetComponentsInChildren<Light2D>(true);
        foreach (Light2D light in children)
            light.intensity = intensity;
        appliedIntensity = intensity;
    }
}
