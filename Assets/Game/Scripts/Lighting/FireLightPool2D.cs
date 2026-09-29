using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class FireLightPool2D : MonoBehaviour
{
    [SerializeField]
    private CellWorld2D cellWorld;

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


    [SerializeField, Min(0.001f)]
    private float intensityPerFireStrength = 0.04f;

    [SerializeField, Range(0f, 0.5f)]
    private float flickerAmount = 0.18f;

    private Vector2[] clusterPositions;
    private float[] clusterStrengths;
    private float timer;

    private void Awake()
    {
        //setup lights and disable all of them
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
        if (cellWorld == null || !cellWorld.IsInitialized || lights == null || lights.Length == 0)
        { return;  }

        //timer to refresh lighting
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
        //get cells with highest fire powers
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

            //disable unused lights when there are fewer fire clusters
            if (i >= clusterCount)
            {
                light2D.enabled = false;
                continue;
            }

            //move this light to its cluster while keeping the same z position
            Vector2 position = clusterPositions[i];

            light2D.transform.position = new Vector3(
                position.x,
                position.y,
                light2D.transform.position.z
            );

            //set the fire color and limit brightness based on cluster strength
            light2D.color = fireLightColor;
            light2D.intensity = Mathf.Clamp(
                clusterStrengths[i] * intensityPerFireStrength,
                minimumIntensity,
                maximumIntensity
            );
            //vary brightness over time with a different flicker pattern for each light
            light2D.intensity *= Mathf.Lerp(1f - flickerAmount, 1f + flickerAmount,
                Mathf.PerlinNoise(i * 3.7f, Time.time * 8f));

            light2D.enabled = true;
        }
    }
}
