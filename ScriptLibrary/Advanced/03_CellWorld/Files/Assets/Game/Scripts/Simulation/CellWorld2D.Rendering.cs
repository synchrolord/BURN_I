using System;
using UnityEngine;

public partial class CellWorld2D
{
    [Header("Fire Appearance")]
    [SerializeField, Range(5f, 30f)] private float fireVisualFramesPerSecond = 20f;
    [SerializeField, Range(0f, 1f)] private float fireFlickerAmount = 0.4f;
    [SerializeField, Min(0.1f)] private float fireFlickerSpeed = 7f;
    private bool hasVisibleFire;
    private float nextFireVisualUpdate;

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
            bool outsideRoom = !ContainsCell(x, y);
            solid[i] = outsideRoom || Physics2D.OverlapBox(CellToWorld(x, y), sampleSize, 0f, solidMask.value) != null;

            if (solid[i])
            {
                water[i] = fuel[i] = smoke[i] = 0;
                if (outsideRoom || !HasPlantFuel(i)) fire[i] = 0;
            }
        }
    }

    private void RenderNow()
    {
        if (runtimeTexture == null) return;

        hasVisibleFire = false;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (!ContainsCell(i % width, i / width)) pixels[i] = new Color32(0, 0, 0, 0);
            else if (showSolidCells && solid[i]) pixels[i] = solidDebugColor;
            else if (fire[i] > 0)
            {
                pixels[i] = FireColor(i);
                hasVisibleFire = true;
            }
            else if (smoke[i] > 0) pixels[i] = SmokeColor(smoke[i]);
            else if (water[i] > 0 && fuel[i] > 0)
            {
                float waterShare = water[i] / (float)(water[i] + fuel[i]);
                pixels[i] = Color.Lerp(FuelColor(fuel[i]), WaterColor(water[i]), waterShare);
            }
            else if (water[i] > 0) pixels[i] = WaterColor(water[i]);
            else if (fuel[i] > 0) pixels[i] = FuelColor(fuel[i]);
            else pixels[i] = new Color32(0, 0, 0, 0);
        }

        runtimeTexture.SetPixels32(pixels);
        runtimeTexture.Apply(false, false);
        textureNeedsRefresh = false;
    }

    private Color FireColor(int index)
    {
        float x = index % width, y = index / width;
        float noise = Mathf.PerlinNoise(x * 0.32f + Time.time * 0.7f,
            y * 0.3f - Time.time * fireFlickerSpeed);
        float heat = Mathf.Clamp01((fire[index] - 1f) * 0.5f + (noise - 0.5f) * fireFlickerAmount);
        Color color = heat < 0.5f ? Color.Lerp(weakFireColor, mediumFireColor, heat * 2f)
            : Color.Lerp(mediumFireColor, hotFireColor, (heat - 0.5f) * 2f);
        color.a *= Mathf.Lerp(0.72f, 1f, noise);
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

    protected void OnDrawGizmosSelected()
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
