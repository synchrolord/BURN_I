using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class FireLightPool2D : MonoBehaviour
{
    [SerializeField]
    private CellWorld2D cellWorld;

    [Tooltip("Create a few reusable Spot Light 2D objects and assign them here.")]
    [SerializeField]
    private Light2D[] lights;

    [SerializeField, Min(0.02f)]
    private float updateInterval = 0.10f;

    [SerializeField, Range(1, 3)]
    private int minimumFireEnergy = 2;

    [SerializeField]
    private Color fireLightColor =
        new Color(1f, 0.42f, 0.10f, 1f);

    [SerializeField, Min(0f)]
    private float minimumIntensity = 0.35f;

    [SerializeField, Min(0f)]
    private float maximumIntensity = 1.35f;

    [Tooltip("Larger values make each burning chunk brighter.")]
    [SerializeField, Min(0.001f)]
    private float intensityPerFireStrength = 0.04f;

    [SerializeField, Range(0f, 0.5f)]
    private float flickerAmount = 0.18f;

    private Vector2[] clusterPositions;
    private float[] clusterStrengths;
    private float timer;

    private void Awake()
    {
        int count = lights == null ? 0 : lights.Length;

        clusterPositions = new Vector2[count];
        clusterStrengths = new float[count];

        for (int i = 0; i < count; i++)
        {
            if (lights[i] != null)
            {
                lights[i].enabled = false;
            }
        }
    }

    private void Update()
    {
        if (cellWorld == null || !cellWorld.IsInitialized ||
            lights == null ||
            lights.Length == 0)
        {
            return;
        }

        timer -= Time.deltaTime;

        if (timer > 0f)
        {
            return;
        }

        timer = updateInterval;
        RefreshLights();
    }

    private void RefreshLights()
    {
        int clusterCount = cellWorld.GetBrightestFireClusters(
            clusterPositions,
            clusterStrengths,
            (byte)minimumFireEnergy
        );

        for (int i = 0; i < lights.Length; i++)
        {
            Light2D light2D = lights[i];

            if (light2D == null)
            {
                continue;
            }

            if (i >= clusterCount)
            {
                light2D.enabled = false;
                continue;
            }

            Vector2 position = clusterPositions[i];

            light2D.transform.position = new Vector3(
                position.x,
                position.y,
                light2D.transform.position.z
            );

            light2D.color = fireLightColor;
            light2D.intensity = Mathf.Clamp(
                clusterStrengths[i] * intensityPerFireStrength,
                minimumIntensity,
                maximumIntensity
            );
            light2D.intensity *= Mathf.Lerp(1f - flickerAmount, 1f + flickerAmount,
                Mathf.PerlinNoise(i * 3.7f, Time.time * 8f));

            light2D.enabled = true;
        }
    }
}
