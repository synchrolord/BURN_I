using UnityEngine;

// One texture draws the cell grid; fire color varies smoothly between simulation ticks.
public partial class CellWorld2D
{
    [Header("Fire Appearance")]
    [SerializeField, Range(5f, 30f)] private float fireVisualFramesPerSecond = 20f;
    [SerializeField, Range(0f, 1f)] private float fireFlickerAmount = 0.4f;
    [SerializeField, Min(0.1f)] private float fireFlickerSpeed = 7f;
    private bool hasVisibleFire;
    private float nextFireVisualUpdate;

    //create one texture pixel for each simulation cell
    private void CreateRuntimeTexture()
    {
        runtimeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "CellWorld Runtime Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        //make the texture a sprite with a bottom-left pivot and matching cell size
        runtimeSprite = Sprite.Create(
            runtimeTexture,
            new Rect(0f, 0f, width, height),
            Vector2.zero,
            1f / cellSize,
            0,
            SpriteMeshType.FullRect
        );

        runtimeSprite.name = "CellWorld Runtime Sprite";
        //give the renderer the new sprite without adding a color tint
        spriteRenderer.sprite = runtimeSprite;
        spriteRenderer.color = Color.white;
    }

    //choose one visible color for every cell
    private void RenderNow()
    {
        if (runtimeTexture == null) return;

        hasVisibleFire = false;
        for (int i = 0; i < pixels.Length; i++)
        {
            //hide cells outside a polygon room
            if (roomCells != null && !roomCells[i]) { pixels[i] = new Color32(0, 0, 0, 0); continue; }
            //draw debug walls first, then fire, smoke, liquids, or empty space
            if (showSolidCells && solid[i]) pixels[i] = solidDebugColor;
            else if (fire[i] > 0)
            {
                pixels[i] = FireColor(i);
                hasVisibleFire = true;
            }
            else if (smoke[i] > 0) pixels[i] = SmokeColor(smoke[i]);
            else if (water[i] > 0 && fuel[i] > 0)
            {
                //blend the two liquid colors using their relative amounts
                float waterShare = water[i] / (float)(water[i] + fuel[i]);
                pixels[i] = Color.Lerp(FuelColor(fuel[i]), WaterColor(water[i]), waterShare);
            }
            else if (water[i] > 0) pixels[i] = WaterColor(water[i]);
            else if (fuel[i] > 0) pixels[i] = FuelColor(fuel[i]);
            else pixels[i] = new Color32(0, 0, 0, 0);
        }

        //send the finished pixel colors to the texture and refresh it
        runtimeTexture.SetPixels32(pixels);
        runtimeTexture.Apply(false, false);
        textureNeedsRefresh = false;
    }

    //choose fire color from its strength and changing flicker
    private Color FireColor(int index)
    {
        //convert the array index back into cell coordinates
        float x = index % width, y = index / width;
        float noise = Mathf.PerlinNoise(x * 0.32f + Time.time * 0.7f,
            y * 0.3f - Time.time * fireFlickerSpeed);
        float heat = Mathf.Clamp01((fire[index] - 1f) * 0.5f + (noise - 0.5f) * fireFlickerAmount);
        //blend weak to medium fire, then medium to hot fire
        Color color = heat < 0.5f ? Color.Lerp(weakFireColor, mediumFireColor, heat * 2f)
            : Color.Lerp(mediumFireColor, hotFireColor, (heat - 0.5f) * 2f);
        color.a *= Mathf.Lerp(0.72f, 1f, noise);
        return color;
    }

    //fuller water cells use more of the deep water color
    private Color WaterColor(byte amount) =>
        Color.Lerp(shallowWaterColor, deepWaterColor, amount / (float)liquidCapacity);

    //fuller fuel cells use more of the deep fuel color
    private Color FuelColor(byte amount) =>
        Color.Lerp(shallowFuelColor, deepFuelColor, amount / (float)liquidCapacity);

    //more smoke makes the cell less transparent
    private Color SmokeColor(byte amount)
    {
        Color color = smokeColor;
        color.a *= Mathf.Lerp(0.25f, 1f, amount / (float)smokeCapacity);
        return color;
    }

    //show the grid bounds when selecting the object in the editor
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
