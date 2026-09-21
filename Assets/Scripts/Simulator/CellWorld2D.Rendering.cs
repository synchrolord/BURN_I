using System;
using UnityEngine;

public sealed partial class CellWorld2D
{
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

            if (solid[i]) water[i] = fuel[i] = fire[i] = smoke[i] = 0;
        }
    }

    private void RenderNow()
    {
        if (runtimeTexture == null) return;

        for (int i = 0; i < pixels.Length; i++)
        {
            if (showSolidCells && solid[i]) pixels[i] = solidDebugColor;
            else if (fire[i] == 3) pixels[i] = hotFireColor;
            else if (fire[i] == 2) pixels[i] = mediumFireColor;
            else if (fire[i] == 1) pixels[i] = weakFireColor;
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
