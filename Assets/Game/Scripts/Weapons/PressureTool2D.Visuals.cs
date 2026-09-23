using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class PressureTool2D
{
    [Header("Flame Color Over Lifetime")]
    [Tooltip("Hot yellow through orange/red to a transparent ember. Evaluated smoothly over each flame probe's lifetime.")]
    [SerializeField] private Gradient flameColorOverLifetime = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(new Color(1f, 0.9f, 0.22f), 0f),
            new GradientColorKey(new Color(1f, 0.72f, 0.10f), 0.25f),
            new GradientColorKey(new Color(1f, 0.44f, 0.04f), 0.5f),
            new GradientColorKey(new Color(1f, 0.18f, 0.025f), 0.75f),
            new GradientColorKey(new Color(0.55f, 0.045f, 0.01f), 1f)
        },
        alphaKeys = new[]
        {
            new GradientAlphaKey(0.95f, 0f),
            new GradientAlphaKey(0.70f, 0.4f),
            new GradientAlphaKey(0.25f, 0.8f),
            new GradientAlphaKey(0f, 1f)
        }
    };

    private GameObject sprayVisualObject;
    private Mesh sprayMesh;
    private MeshRenderer sprayRenderer;
    private Vector3[] sprayVertices;
    private Color[] sprayColors;
    private Vector2[] sprayUVs;
    private int[] sprayTriangles;

    private void InitializeSprayVisuals()
    {
        Material material = Resources.Load<Material>("Effects/PressureSpray");
        if (material == null)
        {
            Debug.LogWarning("PressureTool2D: missing Resources/Effects/PressureSpray material. Simulation will still run.", this);
            return;
        }
        sprayVisualObject = new GameObject("Pressure spray (runtime)");
        sprayVisualObject.layer = gameObject.layer;
        sprayMesh = new Mesh { name = "Pressure spray mesh" };
        sprayMesh.MarkDynamic();
        sprayVisualObject.AddComponent<MeshFilter>().sharedMesh = sprayMesh;
        sprayRenderer = sprayVisualObject.AddComponent<MeshRenderer>();
        sprayRenderer.sharedMaterial = material;
        sprayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        sprayRenderer.receiveShadows = false;
        int count = probes.Length;
        sprayVertices = new Vector3[count * 4];
        sprayColors = new Color[count * 4];
        sprayUVs = new Vector2[count * 4];
        sprayTriangles = new int[count * 6];
        for (int i = 0; i < count; i++)
        {
            int v = i * 4, t = i * 6;
            sprayUVs[v] = new Vector2(0, 0); sprayUVs[v + 1] = new Vector2(1, 0);
            sprayUVs[v + 2] = new Vector2(1, 1); sprayUVs[v + 3] = new Vector2(0, 1);
            sprayTriangles[t] = v; sprayTriangles[t + 1] = v + 1; sprayTriangles[t + 2] = v + 2;
            sprayTriangles[t + 3] = v; sprayTriangles[t + 4] = v + 2; sprayTriangles[t + 5] = v + 3;
        }
        sprayMesh.vertices = sprayVertices;
        sprayMesh.uv = sprayUVs;
        sprayMesh.colors = sprayColors;
        sprayMesh.triangles = sprayTriangles;
        sprayRenderer.enabled = false;
    }

    private void UpdateSprayVisuals()
    {
        ActiveProbeCount = 0;
        for (int i = 0; i < probes.Length; i++) if (probes[i].active) ActiveProbeCount++;
        if (sprayMesh == null) return;
        SpriteRenderer worldRenderer = cellWorld.GetComponent<SpriteRenderer>();
        if (worldRenderer != null)
        {
            sprayRenderer.sortingLayerID = worldRenderer.sortingLayerID;
            sprayRenderer.sortingOrder = worldRenderer.sortingOrder + spraySortingOffset;
        }
        for (int i = 0; i < probes.Length; i++)
        {
            int v = i * 4;
            SprayProbe probe = probes[i];
            if (!probe.active)
            {
                for (int j = 0; j < 4; j++) { sprayVertices[v + j] = Vector3.zero; sprayColors[v + j] = Color.clear; }
                continue;
            }
            bool water = probe.material == ToolMaterial.Water;
            float age = 1f - Mathf.Clamp01(probe.remainingLife / probe.initialLife);
            float width = water ? waterDropWidth : flameWidth * Mathf.Lerp(0.65f, 1.7f, age);
            float length = water ? Mathf.Clamp(probe.velocity.magnitude * 0.022f, 0.2f, 0.9f) : width * 2.3f;
            Vector2 direction = probe.velocity.sqrMagnitude > 0.001f ? probe.velocity.normalized : Vector2.up;
            if (!water) direction = Vector2.Lerp(direction, Vector2.up, age * 0.65f).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x) * width * 0.5f;
            Vector2 center = probe.position;
            Vector2 bottom = center - direction * length * 0.5f, top = center + direction * length * 0.5f;
            float z = cellWorld.transform.position.z;
            sprayVertices[v] = new Vector3(bottom.x - side.x, bottom.y - side.y, z);
            sprayVertices[v + 1] = new Vector3(bottom.x + side.x, bottom.y + side.y, z);
            sprayVertices[v + 2] = new Vector3(top.x + side.x, top.y + side.y, z);
            sprayVertices[v + 3] = new Vector3(top.x - side.x, top.y - side.y, z);
            Color color = water ? new Color(0.2f, 0.72f, 1f, 0.88f)
                : EvaluateFlameColor(age);
            for (int j = 0; j < 4; j++) sprayColors[v + j] = color;
        }
        sprayMesh.vertices = sprayVertices;
        sprayMesh.colors = sprayColors;
        sprayMesh.RecalculateBounds();
        sprayRenderer.enabled = ActiveProbeCount > 0;
    }

    private Color EvaluateFlameColor(float age) =>
        flameColorOverLifetime.Evaluate(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age)));

    private void ClearSprayVisuals()
    {
        ActiveProbeCount = 0;
        if (sprayRenderer != null) sprayRenderer.enabled = false;
    }

    private void DisposeSprayVisuals()
    {
        if (sprayMesh != null) Destroy(sprayMesh);
        if (sprayVisualObject != null) Destroy(sprayVisualObject);
    }
}
