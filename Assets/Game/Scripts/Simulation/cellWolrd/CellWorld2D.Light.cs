using System.Collections.Generic;
using UnityEngine;

public partial class CellWorld2D
{
    private readonly Queue<int> lightQueue = new Queue<int>();
    private readonly List<int> placedLightCells = new List<int>();
    private bool[] lightSource;
    private ushort[] lightPower;

    private SpriteRenderer darknessRenderer;
    private Texture2D darknessTexture;
    private Sprite darknessSprite;
    private Color32[] darknessPixels;

    private float nextLightRefresh;
    private bool lightNeedsRefresh = true;
    private bool lightHadFire;


    private void CreateDarknessTexture()
    {
        int count = width * height;

        lightSource = new bool[count];
        lightPower = new ushort[count];
        darknessPixels = new Color32[count];

        darknessTexture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false,
            false)
        {
            name = "CellWorld Darkness Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        darknessSprite = Sprite.Create(
            darknessTexture,
            new Rect(0f, 0f, width, height),
            Vector2.zero,
            1f / cellSize,
            0,
            SpriteMeshType.FullRect);

        darknessSprite.name = "CellWorld Darkness Sprite";

        GameObject darknessObject =
            new GameObject("Pixel Darkness");

        darknessObject.transform.SetParent(transform, false);

        darknessRenderer =
            darknessObject.AddComponent<SpriteRenderer>();

        darknessRenderer.sprite = darknessSprite;
        darknessRenderer.sortingLayerID =
            spriteRenderer.sortingLayerID;
        darknessRenderer.sortingOrder = darknessOrder;

        Material material = Resources.Load<Material>(
            "Materials/Pixel Darkness");

        if (material != null)
        {
            darknessRenderer.sharedMaterial = material;

            MaterialPropertyBlock properties =
                new MaterialPropertyBlock();

            properties.SetFloat("_CellSize", cellSize);
            properties.SetFloat("_BaseLight", unityLightBase);
            properties.SetFloat("_LightEffect", unityLightEffect);
            properties.SetFloat("_MaximumLight", unityLightMaximum);
            darknessRenderer.SetPropertyBlock(properties);
        }

        SetDarknessVisible(roomActive);
    }


    private bool UpdatePixelLighting()
    {
        if (!usePixelDarkness || darknessTexture == null)
            return false;

        if (!lightNeedsRefresh)
        {
            bool lightCanChange =
                hasVisibleFire ||
                lightHadFire ||
                HasCellLight();

            if (!lightCanChange || Time.time < nextLightRefresh)
                return false;
        }

        RefreshPixelLight();
        return true;
    }


    private void RefreshPixelLight()
    {
        if (light == null || darknessTexture == null)
            return;

        System.Array.Clear(light, 0, light.Length);
        System.Array.Clear(lightSource, 0, lightSource.Length);
        System.Array.Clear(lightPower, 0, lightPower.Length);
        lightQueue.Clear();

        //start with lights painted into the level
        foreach (int i in placedLightCells)
            AddLightCell(i, placedLight[i]);

        lightHadFire = AddFireLights();

        //placed light objects join the same spreading field as fire
        foreach (CellLight2D source in CellLight2D.ActiveLights)
        {
            if (source == null ||
                !source.TryGetCell(this, out Vector2Int cell))
            {
                continue;
            }

            AddLightCell(
                Index(cell.x, cell.y),
                source.LightLevel);
        }

        SpreadLight();

        DrawDarkness();

        lightNeedsRefresh = false;
        nextLightRefresh =
            Time.time + lightRefreshSeconds;
    }


    private bool AddFireLights()
    {
        bool foundFire = false;

        //every fire cell fills itself with light and spreads from there
        for (int index = 0; index < fire.Length; index++)
        {
            if (fire[index] == 0) continue;
            foundFire = true;

            float x = index % width;
            float y = index / width;
            float noise = Mathf.PerlinNoise(
                x * 0.32f + Time.time * 0.7f,
                y * 0.3f - Time.time * fireFlickerSpeed);

            float flicker = Mathf.Lerp(
                1f - fireFlickerAmount,
                1f,
                noise);

            AddLightCell(
                index,
                Mathf.CeilToInt(
                    fireLightLevel *
                    (fire[index] / 3f) *
                    flicker));
        }

        return foundFire;
    }


    private bool HasCellLight()
    {
        foreach (CellLight2D source in CellLight2D.ActiveLights)
        {
            if (source != null && source.TryGetCell(this, out _))
                return true;
        }

        return false;
    }


    private void AddLightCell(int index, int level)
    {
        if (level <= 0 || index < 0 || index >= light.Length)
            return;

        byte newLevel =
            (byte)Mathf.Clamp(level, 1, 64);

        lightSource[index] = true;

        ushort newPower = (ushort)(newLevel * 10);

        if (newPower <= lightPower[index])
            return;

        light[index] = newLevel;
        lightPower[index] = newPower;
        lightQueue.Enqueue(index);
    }


    private void SpreadLight()
    {
        while (lightQueue.Count > 0)
        {
            int from = lightQueue.Dequeue();
            int power = lightPower[from];

            if (power <= 10)
                continue;

            //a light may begin inside a wall but walls do not pass light on
            if (solid[from] && !lightSource[from])
                continue;

            int x = from % width;
            int y = from / width;

            //straight movement costs 10 and diagonal movement costs 14
            //this keeps the light round while still following the cell grid
            SpreadLightTo(x + 1, y, power - 10);
            SpreadLightTo(x - 1, y, power - 10);
            SpreadLightTo(x, y + 1, power - 10);
            SpreadLightTo(x, y - 1, power - 10);

            SpreadLightDiagonal(x, y, 1, 1, power - 14);
            SpreadLightDiagonal(x, y, -1, 1, power - 14);
            SpreadLightDiagonal(x, y, 1, -1, power - 14);
            SpreadLightDiagonal(x, y, -1, -1, power - 14);
        }
    }


    private void SpreadLightDiagonal(
        int x,
        int y,
        int directionX,
        int directionY,
        int power)
    {
        int sideX = x + directionX;
        int sideY = y + directionY;

        if (!ContainsCell(sideX, y) ||
            !ContainsCell(x, sideY) ||
            solid[Index(sideX, y)] ||
            solid[Index(x, sideY)])
        {
            return;
        }

        SpreadLightTo(sideX, sideY, power);
    }


    private void SpreadLightTo(int x, int y, int power)
    {
        if (power <= 0 || !ContainsCell(x, y))
            return;

        int index = Index(x, y);

        if (power <= lightPower[index])
            return;

        lightPower[index] = (ushort)power;
        light[index] = (byte)Mathf.CeilToInt(power / 10f);

        //light the wall cell but do not continue through it
        if (!solid[index])
            lightQueue.Enqueue(index);
    }


    private void DrawDarkness()
    {
        byte darkestAlpha =
            (byte)Mathf.RoundToInt(darkness * 255f);

        for (int i = 0; i < darknessPixels.Length; i++)
        {
            if (roomCells != null && !roomCells[i])
            {
                darknessPixels[i] = new Color32(0, 0, 0, 0);
                continue;
            }

            float brightness = Mathf.Clamp01(
                light[i] / (float)fullyLitLevel);

            darknessPixels[i] = new Color32(
                0,
                0,
                0,
                (byte)Mathf.RoundToInt(
                    darkestAlpha * (1f - brightness)));
        }

        darknessTexture.SetPixels32(darknessPixels);
        darknessTexture.Apply(false, false);
    }


    public void AddPlacedLight(Vector2 position, int level)
    {
        if (!IsReady ||
            !TryWorldToCell(position, out Vector2Int cell))
        {
            return;
        }

        int index = Index(cell.x, cell.y);

        if (placedLight[index] == 0)
            placedLightCells.Add(index);

        placedLight[index] = (byte)Mathf.Max(
            placedLight[index],
            Mathf.Clamp(level, 1, 64));

        lightNeedsRefresh = true;
    }


    private void SetDarknessVisible(bool visible)
    {
        if (darknessRenderer != null)
            darknessRenderer.enabled =
                usePixelDarkness && visible;
    }


    private void DestroyDarknessTexture()
    {
        if (darknessSprite != null)
            Destroy(darknessSprite);

        if (darknessTexture != null)
            Destroy(darknessTexture);
    }
}
