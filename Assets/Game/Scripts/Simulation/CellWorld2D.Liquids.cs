using UnityEngine;

public partial class CellWorld2D
{
    
    private int GetTotalLiquid(int index)
    {
        return water[index] + fuel[index]+ acid[index];
    }
    
    // Basic rule: fall down, try the two lower diagonals, then level with neighbors.
    // Water and fuel share capacity, so moving one never overwrites the other.
    private void SimulateLiquidAt(byte[] liquid, bool[] moved, int x, int y, float viscosity)
    {
        
        int i = Index(x, y);
        //skip over irritant cells
        if (solid[i] || liquid[i] == 0 || moved[i]) return;
        moved[i] = true;
        
        
        //how much liquid is allowed to move every tick
        int downFlow = Mathf.Max(1, Mathf.RoundToInt(liquidCapacity * (1f - viscosity * 0.75f)));
        int sideFlow = Mathf.Max(1, Mathf.RoundToInt(liquidCapacity * 0.5f * (1f - viscosity)));
        int direction = ((x + y + tickNumber) & 1) == 0 ? -1 : 1;

        
        //water movement logic hierarchy
        //Down
        MoveLiquid(liquid, moved, x, y, x, y - 1, downFlow);
        
        //diagonally
        if (!solid[Index(x + direction, y)])
            MoveLiquid(liquid, moved, x, y, x + direction, y - 1, downFlow);
        
        if (!solid[Index(x - direction, y)])
            MoveLiquid(liquid, moved, x, y, x - direction, y - 1, downFlow);
        
        
        //left and right
        LevelLiquid(liquid, moved, x, y, x + direction, sideFlow);
        LevelLiquid(liquid, moved, x, y, x - direction, sideFlow);
    }

    //handle homogenizing liquid close by
    private void LevelLiquid(
        byte[] liquid,
        bool[] moved,
        int x,
        int y,
        int nextX,
        int maximum)
    {
        if (!InBounds(nextX, y))
            return;

        int from = Index(x, y);
        int to = Index(nextX, y);

        int difference =
            GetTotalLiquid(from) -
            GetTotalLiquid(to);

        if (difference > 1)
        {
            MoveLiquid(
                liquid,
                moved,
                x,
                y,
                nextX,
                y,
                Mathf.Min(maximum, difference / 2));
        }
    }

    
    /// handling movement of liquids between neighboring cells
    private void MoveLiquid(byte[] liquid, bool[] moved,
        int cellX, int cellY, int nextCellX, int nextCellY, int requested)
    {
        if (!InBounds(nextCellX, nextCellY)) return;
        int from = Index(cellX, cellY), to = Index(nextCellX, nextCellY);
        if (solid[to]) return;
        
        //how mcuh space is inside neighboring cell
        int space = liquidCapacity - GetTotalLiquid(to);        
        //amount transferred
        int amount = Mathf.Min(liquid[from], Mathf.Min(requested, space));
        if (amount <= 0) return;
        
        //update liquid amounts
        liquid[from] -= (byte)amount;
        liquid[to] += (byte)amount;
        moved[to] = true;
        
        
        WakeCell(cellX, cellY);
        WakeCell(nextCellX, nextCellY);
        textureNeedsRefresh = true;
    }

    
    private int AddLiquidCircle(byte[] liquid, Vector2 position, int radius, int amountPerCell)
    {
        if (amountPerCell <= 0 || !TryWorldToCell(position, out Vector2Int center)) return 0;
        radius = Mathf.Max(0, radius);
        int total = 0;
        
        //loop over neighboring cells
        for (int y = Mathf.Max(0, center.y - radius); y <= Mathf.Min(height - 1, center.y + radius); y++)
        for (int x = Mathf.Max(0, center.x - radius); x <= Mathf.Min(width - 1, center.x + radius); x++)
        {
            if (!IsInsideCircle(x, y, center, radius * radius)) continue;
            int i = Index(x, y);
            if (solid[i]) continue;
            
            //avoid overfilling cells
            int accepted = Mathf.Min(amountPerCell, liquidCapacity - GetTotalLiquid(i));            
            if (accepted <= 0) continue;
            
            
            liquid[i] += (byte)accepted;
            total += accepted;
            WakeCell(x, y);
        }
        if (total > 0) textureNeedsRefresh = true;
        return total;
    }
}
