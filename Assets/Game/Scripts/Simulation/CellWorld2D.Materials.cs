using System;
using UnityEngine;

public partial class CellWorld2D
{
    //add materials with AddLiquidCircle
    public int AddWater(Vector2 position, int radius, int amountPerCell) =>
        IsReady 
            ? AddLiquidCircle(
                water, 
                position, 
                radius, 
                amountPerCell) 
            : 0;
    
    
    public int AddFuel(Vector2 position, int radius, int amountPerCell) =>
        IsReady 
            ? AddLiquidCircle(
                fuel,
                position,
                radius, 
                amountPerCell) 
            : 0;
    
    public int AddAcid(
        Vector2 position, int radius, int amountPerCell) =>
        IsReady
            ? AddLiquidCircle(
                acid, 
                position,
                radius, 
                amountPerCell)
            : 0;
    
    
    public int AddSmoke(
        Vector2 position, int radius, int amountPerCell)
    {
        return IsReady
            ? AddGasCircle(
                smoke, 
                position,
                radius, 
                amountPerCell)
            : 0;
    }
    
    public int AddSteam(
        Vector2 position, int radius, int amountPerCell)
    {
        return IsReady
            ? AddGasCircle(
                steam, 
                position,
                radius,
                amountPerCell)
            : 0;
    }
    
    
    
    public int ApplyWaterImpact(Vector2 position, int amount)
    {
        if (!IsReady || amount <= 0 || !TryWorldToCell(position, out Vector2Int cell)) return 0;
        

        int i = Index(cell.x, cell.y);
        bool plant = HasPlantFuel(i);
        if (solid[i] && !plant) return 0;
        int spent = Mathf.Min(amount, fire[i]);
        
        //if tile is occupied with plant
        if (plant)
        {
            spent = Mathf.Max(1, spent);
            WetPlant(i);
        }
        
        if (spent > 0)
        {
            //subtract water from fire i cell
            fire[i] = (byte)Mathf.Max(0, fire[i] - spent);
            
            //create smoke 
            if (plant)
            {
                AddPlantSmoke(cell.x, cell.y, 1);
            }
            else AddSmokeAtCell(cell.x, cell.y, 1);
            WakeCell(cell.x, cell.y);
            textureNeedsRefresh = true;
        }
        int remaining = amount - spent;
        return spent + (remaining > 0 ? AddWater(position, 0, remaining) : 0);
    }
    public int Ignite(Vector2 position, int radius, byte energy = 3)
    {
        if (!IsReady || !TryWorldToCell(position, out Vector2Int center)) return 0;
        energy = (byte)Mathf.Clamp(energy, 1, 3);
        int changed = 0;
        int radiusSquared = radius * radius;

        //loop thorough all sounding neighbors 
        for (int y = center.y - radius; y <= center.y + radius; y++)
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            //check if cell is inside circle or is solid
            if (!IsInsideCircle(x, y, center, radiusSquared)) continue;
            int i = Index(x, y);
            if (solid[i] && !CanBurnPlant(i)) continue;
            if (HasPlantFuel(i) && !CanBurnPlant(i) && fuel[i] == 0) continue;
            
            //update fire value inside the cell
            byte oldValue = fire[i];
            fire[i] = (byte)Mathf.Max(fire[i], energy);
            if (fire[i] == oldValue) continue;

            changed++;
            WakeCell(x, y);
        }

        //update only if cells change
        if (changed > 0) textureNeedsRefresh = true;
        return changed;
    }

    public int ExtractWater(
        Vector2 position,
        int radius,
        int requestedAmount)
    {
        return ExtractLiquid(
            water,
            position,
            radius,
            requestedAmount);
    }

    
    public int ExtractAcid(
        Vector2 position,
        int radius,
        int requestedAmount)
    {
        return ExtractLiquid(
            acid,
            position,
            radius,
            requestedAmount);
    }
    
    private int ExtractLiquid(
        byte[] liquid,
        Vector2 position,
        int radius,
        int requestedAmount)
    {
        if (!IsReady ||
            requestedAmount <= 0 ||
            !TryWorldToCell(position, out Vector2Int center))
        {
            return 0;
        }

        int removedTotal = 0;
        int radiusSquared = radius * radius;

        for (int y = center.y - radius;
             y <= center.y + radius && removedTotal < requestedAmount;
             y++)
        {
            for (int x = center.x - radius;
                 x <= center.x + radius && removedTotal < requestedAmount;
                 x++)
            {
                if (!IsInsideCircle(x, y, center, radiusSquared))
                    continue;

                int i = Index(x, y);

                int removed = Mathf.Min(
                    liquid[i],
                    requestedAmount - removedTotal);

                if (removed <= 0)
                    continue;

                liquid[i] -= (byte)removed;
                removedTotal += removed;

                WakeCell(x, y);
            }
        }

        if (removedTotal > 0)
            textureNeedsRefresh = true;

        return removedTotal;
    }
    
    public void EraseCircle(Vector2 position, int radius)
    {
        if (!IsReady || !TryWorldToCell(position, out Vector2Int center)) return;
        int radiusSquared = radius * radius;
        bool changed = false;
        
        //loop thorough all sounding neighbors 
        for (int y = center.y - radius; y <= center.y + radius; y++)
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            //check for empty cells and skip over them
            if (!IsInsideCircle(x, y, center, radiusSquared)) continue;
            int i = Index(x, y);
            if (water[i] == 0 && fuel[i] == 0 && acid[i]==0&&
                fire[i] == 0 && smoke[i] == 0&&steam[i]==0) 
                continue;

            //clear everything and update
            water[i] = fuel[i] = acid[i] = fire[i] = smoke[i] = steam[i]= 0;
            WakeCell(x, y);
            changed = true;
        }

        if (changed) textureNeedsRefresh = true;
    }

    
    //getters
    public float GetWaterFraction(Vector2 position)
    {
        if (!IsReady || !TryWorldToCell(position, out Vector2Int cell)) return 0f;
        return water[Index(cell.x, cell.y)] / (float)liquidCapacity;
    }
    public byte GetFireEnergy(Vector2 position)
    {
        if (!IsReady || !TryWorldToCell(position, out Vector2Int cell)) return 0;
        return fire[Index(cell.x, cell.y)];
    }
    public int GetActiveChunkCount()
    {
        if (chunkAwakeTicks == null) return 0;
        int total = 0;
        for (int y = 0; y < chunkCountY; y++)
        for (int x = 0; x < chunkCountX; x++)
            if (IsChunkAwake(x, y)) total++;
        return total;
    }
    public int GetTotalWaterUnits() => Sum(water);
    public int GetTotalFuelUnits() => Sum(fuel);
    public int GetTotalAcidUnits() => Sum(acid);
    public int GetTotalSteamUnits() => Sum(steam);
    
    public void ClearAllMaterials()
    {
        if (!IsReady) return;
        Array.Clear(water, 0, water.Length);
        Array.Clear(fuel, 0, fuel.Length);
        Array.Clear(acid,0,acid.Length);
        Array.Clear(fire, 0, fire.Length);
        Array.Clear(smoke, 0, smoke.Length);
        Array.Clear(steam, 0, steam.Length);
        textureNeedsRefresh = true;
    }
    
    
    public void TogglePaused()
    {
        paused = !paused;
        accumulator = 0f;
    }
    public void StepOnce() { if (IsReady) SimulateOneTick(); }

    
    //refresh
    public void RebakeSolidsFromPhysics()
    {
        if (solid == null) return;
        BakeSolidsFromPhysics();
        WakeEveryChunk();
        textureNeedsRefresh = true;
    }



    private int Sum(byte[] values)
    {
        if (values == null) return 0;
        int total = 0;
        for (int i = 0; i < values.Length; i++) total += values[i];
        return total;
    }
    
    

}
