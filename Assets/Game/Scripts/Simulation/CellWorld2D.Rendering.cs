using System;
using UnityEngine;

public sealed partial class CellWorld2D
{
    [Header("Fire Appearance")]
    [Tooltip("Animate visible fire every rendered frame. Disable to use the capped visual refresh rate below.")]
    [SerializeField] private bool smoothFireEveryFrame = true;
    [SerializeField, Range(5f, 30f)] private float fireVisualFramesPerSecond = 20f;
    [Tooltip("Time for displayed heat to move across its full range. Does not change simulated fire energy.")]
    [SerializeField, Min(0.01f)] private float fireColorBlendSeconds = 0.18f;
    [Tooltip("Time for opacity to fade across its full range after cooling or extinction.")]
    [SerializeField, Min(0.01f)] private float fireFadeSeconds = 0.22f;
    [SerializeField] private Color warmFireColor = new Color(0.98f, 0.30f, 0.035f, 1f);
    [SerializeField] private Color brightFireColor = new Color(1f, 0.72f, 0.12f, 1f);
    [SerializeField, Range(0f, 1f)] private float fireFlickerAmount = 0.4f;
    [SerializeField, Min(0.1f)] private float fireFlickerSpeed = 7f;
    private bool hasVisibleFire;
    private float nextFireVisualUpdate;
    // Presentation only: x = displayed energy, y = opacity. Gameplay still uses fire[].
    private Vector2[] fireVisuals;
    private float fireAnimationTime;
    private float lastFireRenderTime;

    private void CreateRuntimeTexture()
    {
        runtimeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "CellWorld Runtime Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        runtimeSprite = Sprite.Create(
            runtimeTexture,
            new Rect(0f, 0f, width, height),
            Vector2.zero,
            1f / cellSize,
            0,
            SpriteMeshType.FullRect
        );

        runtimeSprite.name = "CellWorld Runtime Sprite";
        spriteRenderer.sprite = runtimeSprite;
        spriteRenderer.color = Color.white;
    }

    private void BakeSolidsFromPhysics()
    {
        Physics2D.SyncTransforms();
        Array.Clear(solid, 0, solid.Length);

        for (int x = 0; x < width; x++)
        {
            solid[Index(x, 0)] = true;
            solid[Index(x, height - 1)] = true;
        }

        for (int y = 0; y < height; y++)
        {
            solid[Index(0, y)] = true;
            solid[Index(width - 1, y)] = true;
        }

        Vector2 sampleSize = Vector2.one * (cellSize * 0.85f);

        for (int y = 1; y < height - 1; y++)
        for (int x = 1; x < width - 1; x++)
        {
            int i = Index(x, y);
            solid[i] = Physics2D.OverlapBox(CellToWorld(x, y), sampleSize, 0f, solidMask.value) != null;

            if (solid[i])
            {
                water[i] = fuel[i] = smoke[i] = 0;
                if (!HasPlantFuel(i)) fire[i] = 0;
            }
        }
        if (fireVisuals != null)
            for (int i = 0; i < solid.Length; i++)
                if (solid[i]) fireVisuals[i] = Vector2.zero;
    }

    private void RenderNow()
    {
        if (runtimeTexture == null) return;

        if (fireVisuals == null || fireVisuals.Length != pixels.Length)
            fireVisuals = new Vector2[pixels.Length];
        float visualDelta = Mathf.Max(0f, fireAnimationTime - lastFireRenderTime);
        lastFireRenderTime = fireAnimationTime;

        hasVisibleFire = false;
        for (int i = 0; i < pixels.Length; i++)
        {
            Vector2 visual = fireVisuals[i];
            if (solid[i]) visual = Vector2.zero;
            else if (fire[i] > 0 && visual.y <= 0f)
            {
                // Ignition appears immediately; subsequent heat changes blend.
                visual = new Vector2(fire[i], FireOpacity(fire[i]));
            }
            else if (paused && textureNeedsRefresh)
            {
                // Painting and single-stepping while paused should show the requested state.
                visual = new Vector2(fire[i], FireOpacity(fire[i]));
            }
            else if (!paused && (fire[i] > 0 || visual.y > 0f))
            {
                visual.x = Mathf.MoveTowards(visual.x, fire[i], visualDelta * 3f / Mathf.Max(0.01f, fireColorBlendSeconds));
                visual.y = Mathf.MoveTowards(visual.y, FireOpacity(fire[i]), visualDelta / Mathf.Max(0.01f, fireFadeSeconds));
            }
            fireVisuals[i] = visual;
            Color underneath = MaterialColorWithoutFire(i);
            if (visual.y > 0f)
            {
                hasVisibleFire = true; // Continue refreshing the tail after simulated fire reaches zero.
                pixels[i] = CompositeFire(FireColor(i, visual), underneath);
            }
            else pixels[i] = underneath;
        }

        runtimeTexture.SetPixels32(pixels);
        runtimeTexture.Apply(false, false);
        textureNeedsRefresh = false;
    }

    private static float FireOpacity(byte energy) => energy == 0 ? 0f : Mathf.Lerp(0.45f, 1f, (energy - 1f) * 0.5f);

    private Color MaterialColorWithoutFire(int index)
    {
        if (showSolidCells && solid[index]) return solidDebugColor;
        if (smoke[index] > 0) return SmokeColor(smoke[index]);
        if (water[index] > 0 && fuel[index] > 0)
            return Color.Lerp(FuelColor(fuel[index]), WaterColor(water[index]),
                water[index] / (float)(water[index] + fuel[index]));
        if (fuel[index] > 0) return FuelColor(fuel[index]);
        if (water[index] > 0) return WaterColor(water[index]);
        return Color.clear;
    }

    private static Color CompositeFire(Color flame, Color underneath)
    {
        float belowAlpha = underneath.a * (1f - flame.a);
        float alpha = flame.a + belowAlpha;
        if (alpha <= 0f) return Color.clear;
        Color result = (flame * flame.a + underneath * belowAlpha) / alpha;
        result.a = alpha;
        return result;
    }

    private Color FireColor(int index, Vector2 visual)
    {
        float x = index % width, y = index / width;
        float noise = Mathf.PerlinNoise(x * 0.32f + fireAnimationTime * 0.7f,
            y * 0.3f - fireAnimationTime * fireFlickerSpeed);
        float heat = Mathf.Clamp01((visual.x - 1f) * 0.5f + (noise - 0.5f) * fireFlickerAmount);
        float segment = heat * 4f;
        Color color;
        if (segment < 1f) color = Color.Lerp(weakFireColor, warmFireColor, segment);
        else if (segment < 2f) color = Color.Lerp(warmFireColor, mediumFireColor, segment - 1f);
        else if (segment < 3f) color = Color.Lerp(mediumFireColor, brightFireColor, segment - 2f);
        else color = Color.Lerp(brightFireColor, hotFireColor, segment - 3f);
        color.a *= visual.y * Mathf.Lerp(0.72f, 1f, noise);
        return color;
    }

    private Color WaterColor(byte amount) =>
        Color.Lerp(shallowWaterColor, deepWaterColor, amount / (float)liquidCapacity);

    private Color FuelColor(byte amount) =>
        Color.Lerp(shallowFuelColor, deepFuelColor, amount / (float)liquidCapacity);

    private Color SmokeColor(byte amount)
    {
        Color color = smokeColor;
        color.a *= Mathf.Lerp(0.25f, 1f, amount / (float)smokeCapacity);
        return color;
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(
            new Vector3(width * cellSize * 0.5f, height * cellSize * 0.5f, 0f),
            new Vector3(width * cellSize, height * cellSize, 0f)
        );
        Gizmos.matrix = oldMatrix;
    }
}
