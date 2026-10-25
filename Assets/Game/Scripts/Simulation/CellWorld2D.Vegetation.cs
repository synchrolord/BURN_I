using UnityEngine;

public partial class CellWorld2D
{
    //0 is empty, 5 is healthy and 1 is nearly burned
    private byte[] plant;
    private Color32[] plantColor;
    private bool[] visiblePlant;

    //add one visible, burnable plant cell
    internal void AddPlantCell(int x, int y, Color32 color, bool visible = false)
    {
        if (!ContainsCell(x, y)) return;

        int i = Index(x, y);
        plant[i] = 5;
        plantColor[i] = color;
        visiblePlant[i] = visible;
        WakeCell(x, y);
        textureNeedsRefresh = true;
    }


    //add visible plant matter that also holds liquids
    public bool AddPlant(Vector2 position, Color32 color)
    {
        if (!IsReady ||
            !TryWorldToCell(position, out Vector2Int cell))
        {
            return false;
        }

        int i = Index(cell.x, cell.y);

        solid[i] = true;
        ClearCell(i);
        AddPlantCell(cell.x, cell.y, color, true);
        return true;
    }

    

    private bool HasPlant(int index) => plant[index] > 0;

    //let the Tilemap check how much plant remains in one cell
    internal byte GetPlant(int x, int y)
    {
        return ContainsCell(x, y) ? plant[Index(x, y)] : (byte)0;
    }

    //fire and plant in the same cell means that plant is burning
    private void SimulatePlantAt(int cellX, int cellY)
    {
        int i = Index(cellX, cellY);
        if (!HasPlant(i) || fire[i] == 0 || water[i] > 0) return;
        if (tickNumber % plantBurnEveryTicks != 0) return;

        plant[i]--;
        AddPlantSmoke(cellX, cellY, plant[i] == 0 ? 3 : 1);

        //visible plant walls open when they finish burning
        if (plant[i] == 0 && visiblePlant[i])
            solid[i] = false;

        WakeCell(cellX, cellY);
        textureNeedsRefresh = true;
    }

    //use the real sprite color, then darken it as the cell burns
    private Color PlantColor(int index)
    {
        float health = plant[index] / 5f;
        return Color.Lerp(burnedPlantColor, plantColor[index], health);
    }

    
    //let sprayed water reach fire sitting on a plant
    public int ApplyPlantWaterImpact(Vector2 position, int amount)
    {
        if (!IsReady || !TryWorldToCell(position, out Vector2Int cell) ||
            !HasPlant(Index(cell.x, cell.y))) return 0;

        return ApplyWaterImpact(position, amount);
    }

    private void AddPlantSmoke(int cellX, int cellY, int amount)
    {
        //use the nearest open cell
        if (!solid[Index(cellX, cellY)]) { AddSmokeAtCell(cellX, cellY, amount); return; }
        if (cellY + 1 < height - 1 && !solid[Index(cellX, cellY + 1)]) AddSmokeAtCell(cellX, cellY + 1, amount);
        else if (cellX > 1 && !solid[Index(cellX - 1, cellY)]) AddSmokeAtCell(cellX - 1, cellY, amount);
        else if (cellX + 1 < width - 1 && !solid[Index(cellX + 1, cellY)]) AddSmokeAtCell(cellX + 1, cellY, amount);
    }

    private void SpreadPlantFireDiagonally(int x, int y)
    {
        //plant cells can touch at their corners
        for (int dy = -1; dy <= 1; dy += 2)
        for (int dx = -1; dx <= 1; dx += 2)
        {
            int newX = x + dx, newY = y + dy;
            if (ContainsCell(newX, newY) && HasPlant(Index(newX, newY)))
                TrySpreadFire(x, y, newX, newY, dy > 0);
        }
    }
}
