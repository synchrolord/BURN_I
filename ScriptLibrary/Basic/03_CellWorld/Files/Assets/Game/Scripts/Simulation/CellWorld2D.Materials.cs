using System;
using UnityEngine;

public sealed partial class CellWorld2D
{
    // ----- Public methods used by weapons, pickups, drains, and debug tools.

    public int AddWater(Vector2 position, int radius, int amountPerCell) =>
        IsReady ? AddLiquidCircle(water, fuel, position, radius, amountPerCell) : 0;

    public int AddFuel(Vector2 position, int radius, int amountPerCell) =>
        IsReady ? AddLiquidCircle(fuel, water, position, radius, amountPerCell) : 0;

    // A moving water shot must cool on contact, before liquid flow can move it
    // away on the next tick. Return BOTH spent cooling units and stored water
    // so the caller can subtract them from the shot's finite payload.
    public int ApplyWaterImpact(Vector2 position, int amount)
    {
        if (!IsReady || amount <= 0 || !TryWorldToCell(position, out Vector2Int cell)) return 0;
        int i = Index(cell.x, cell.y);
        bool plant = HasPlantFuel(i);
        if (solid[i] && !plant) return 0;
        int spent = Mathf.Min(amount, fire[i]);
        // Wetting solid-backed leaves still consumes a real shot unit even when there is no flame.
        if (plant)
        {
            spent = Mathf.Max(1, spent);
            WetPlant(i);
        }
        if (spent > 0)
        {
            fire[i] = (byte)Mathf.Max(0, fire[i] - spent);
            if (plant) AddPlantSmoke(cell.x, cell.y, 1);
            else AddSmokeAtCell(cell.x, cell.y, 1);
            WakeCell(cell.x, cell.y);
            textureNeedsRefresh = true;
        }
        int remaining = amount - spent;
        return spent + (remaining > 0 ? AddWater(position, 0, remaining) : 0);
    }

    public int AddSmoke(Vector2 position, int radius, int amountPerCell)
    {
        if (!IsReady || !TryWorldToCell(position, out Vector2Int center)) return 0;
        int acceptedTotal = 0;
        int radiusSquared = radius * radius;

        for (int y = center.y - radius; y <= center.y + radius; y++)
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            if (!IsInsideCircle(x, y, center, radiusSquared)) continue;
            int i = Index(x, y);
            if (solid[i]) continue;

            int accepted = Mathf.Min(amountPerCell, smokeCapacity - smoke[i]);
            if (accepted <= 0) continue;

            smoke[i] += (byte)accepted;
            acceptedTotal += accepted;
            WakeCell(x, y);
        }

        if (acceptedTotal > 0) textureNeedsRefresh = true;
        return acceptedTotal;
    }

    public int Ignite(Vector2 position, int radius, byte energy = 3)
    {
        if (!IsReady || !TryWorldToCell(position, out Vector2Int center)) return 0;
        energy = (byte)Mathf.Clamp(energy, 1, 3);
        int changed = 0;
        int radiusSquared = radius * radius;

        for (int y = center.y - radius; y <= center.y + radius; y++)
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            if (!IsInsideCircle(x, y, center, radiusSquared)) continue;
            int i = Index(x, y);
            if (solid[i] && !CanBurnPlant(i)) continue;

            if (HasPlantFuel(i) && !CanBurnPlant(i) && fuel[i] == 0) continue;
            byte oldValue = fire[i];
            fire[i] = (byte)Mathf.Max(fire[i], energy);
            if (fire[i] == oldValue) continue;

            changed++;
            WakeCell(x, y);
        }

        if (changed > 0) textureNeedsRefresh = true;
        return changed;
    }

    public int ExtractWater(Vector2 position, int radius, int requestedAmount)
    {
        if (!IsReady || requestedAmount <= 0 || !TryWorldToCell(position, out Vector2Int center)) return 0;
        int removedTotal = 0;
        int radiusSquared = radius * radius;

        for (int y = center.y - radius; y <= center.y + radius && removedTotal < requestedAmount; y++)
        for (int x = center.x - radius; x <= center.x + radius && removedTotal < requestedAmount; x++)
        {
            if (!IsInsideCircle(x, y, center, radiusSquared)) continue;
            int i = Index(x, y);
            int removed = Mathf.Min(water[i], requestedAmount - removedTotal);
            if (removed <= 0) continue;

            water[i] -= (byte)removed;
            removedTotal += removed;
            WakeCell(x, y);
        }

        if (removedTotal > 0) textureNeedsRefresh = true;
        return removedTotal;
    }

    public void EraseCircle(Vector2 position, int radius)
    {
        if (!IsReady || !TryWorldToCell(position, out Vector2Int center)) return;
        int radiusSquared = radius * radius;
        bool changed = false;

        for (int y = center.y - radius; y <= center.y + radius; y++)
        for (int x = center.x - radius; x <= center.x + radius; x++)
        {
            if (!IsInsideCircle(x, y, center, radiusSquared)) continue;
            int i = Index(x, y);
            if (water[i] == 0 && fuel[i] == 0 && fire[i] == 0 && smoke[i] == 0) continue;

            water[i] = fuel[i] = fire[i] = smoke[i] = 0;
            WakeCell(x, y);
            changed = true;
        }

        if (changed) textureNeedsRefresh = true;
    }

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

    public void ClearAllMaterials()
    {
        if (!IsReady) return;
        Array.Clear(water, 0, water.Length);
        Array.Clear(fuel, 0, fuel.Length);
        Array.Clear(fire, 0, fire.Length);
        Array.Clear(smoke, 0, smoke.Length);
        textureNeedsRefresh = true;
    }

    public void TogglePaused()
    {
        paused = !paused;
        accumulator = 0f;
    }

    public void StepOnce() { if (IsReady) SimulateOneTick(); }

    public void RebakeSolidsFromPhysics()
    {
        if (solid == null) return;
        BakeSolidsFromPhysics();
        WakeEveryChunk();
        textureNeedsRefresh = true;
    }

    public int GetActiveChunkCount()
    {
        if (chunkAwakeTicks == null) return 0;
        if (simulateAllChunks) return chunkAwakeTicks.Length;
        int total = 0;
        for (int i = 0; i < chunkAwakeTicks.Length; i++)
            if (chunkAwakeTicks[i] > 0) total++;
        return total;
    }

    public int GetTotalWaterUnits() => Sum(water);
    public int GetTotalFuelUnits() => Sum(fuel);

    private int Sum(byte[] values)
    {
        if (values == null) return 0;
        int total = 0;
        for (int i = 0; i < values.Length; i++) total += values[i];
        return total;
    }

}
