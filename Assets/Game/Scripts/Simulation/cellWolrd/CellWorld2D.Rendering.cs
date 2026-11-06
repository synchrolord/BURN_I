using UnityEngine;

// One texture draws the cell grid; fire color varies smoothly between simulation ticks.
public partial class CellWorld2D
{
    [Header("Fire Appearance")]
    [SerializeField, Range(5f, 30f)] private float fireVisualFramesPerSecond = 20f;
    [SerializeField, Range(0f, 1f)] private float fireFlickerAmount = 0.4f;
    [SerializeField, Min(0.1f)] private float fireFlickerSpeed = 7f;
    
    [SerializeField, Range(0f, 1f)] private float fireHeat = 0.7f;
    [SerializeField, Range(0f, 1f)] private float fireMinimumAlpha = 0.35f;
    
    
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
            else if (GetTotalGas(i) > 0)
                pixels[i] = GasColor(i);
            
            //dynamically color liquids
            else if (GetTotalLiquid(i) > 0)
            {
                pixels[i] = LiquidColor(i);
            }
            else if (plant[i] > 0 && visiblePlant[i])
                pixels[i] = PlantColor(i);
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
        
        
        
        
        float strength = fire[index] / 3f;

        float heat = Mathf.Clamp01(
            strength * fireHeat +
            (noise - 0.5f) * fireFlickerAmount);

        
        //decide fire Color Dynamically between the 3 colors
        Color color = heat < 0.5f
            ? Color.Lerp(weakFireColor, mediumFireColor, heat * 2f)
            : Color.Lerp(mediumFireColor, hotFireColor, (heat - 0.5f) * 2f);

        color.a *= Mathf.Lerp(fireMinimumAlpha, 1f, noise) *
                   Mathf.Lerp(0.65f, 1f, strength);
        
        
        return color;
    }

    private Color WaterColor(byte amount) =>
        Color.Lerp(shallowWaterColor, deepWaterColor, amount / (float)liquidCapacity);
    private Color FuelColor(byte amount) =>
        Color.Lerp(shallowFuelColor, deepFuelColor, amount / (float)liquidCapacity);
    private Color AcidColor(byte amount) =>
        Color.Lerp(
            shallowAcidColor,
            deepAcidColor,
            amount / (float)liquidCapacity);
    private Color LiquidColor(int index)
    {
        float total = GetTotalLiquid(index);

        Color color = Color.clear;

        color += WaterColor(water[index]) *
                 (water[index] / total);

        color += FuelColor(fuel[index]) *
                 (fuel[index] / total);

        color += AcidColor(acid[index]) *
                 (acid[index] / total);

        return color;
    }
    private Color SmokeColor(byte amount)
    {
        Color color = smokeColor;
        color.a *= Mathf.Lerp(0.25f, 1f, amount / (float)gasCapacity);
        return color;
    }
    private Color GasColor(int index)
    {
        int total = GetTotalGas(index);

        float steamFraction =
            steam[index] / (float)total;

        Color color = Color.Lerp(
            smokeColor,
            steamColor,
            steamFraction);

        color.a *= Mathf.Lerp(
            0.25f,
            1f,
            total / (float)gasCapacity);

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
