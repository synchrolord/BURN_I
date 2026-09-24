using System.Collections.Generic;
using UnityEngine;

public partial class CellWorld2D
{
    // Sparse, anchored biomass, independent of liquid fuel. One linked entry per painted
    // plant per occupied cell also lets oversized/overlapping sprites burn independently.
    private sealed class PlantFuel
    {
        public BurnablePlantMap2D owner;
        public int plant;
        public float remaining, wetSeconds;
        public double wetUntil;
        public int lastBurnTick = -1;
        public PlantFuel next;
    }

    private readonly Dictionary<int, PlantFuel> plantFuel = new Dictionary<int, PlantFuel>();
    private double plantSimulationTime;
    public int PlantFuelCellCount => plantFuel.Count;

    // Called at the actual wall contact before the water shot deposits its remaining
    // payload into air. Ordinary walls consume no extra water through this path.
    public int ApplyPlantWaterImpact(Vector2 position, int amount)
    {
        if (!IsReady || !TryWorldToCell(position, out Vector2Int cell) || !HasPlantFuel(Index(cell.x, cell.y))) return 0;
        return ApplyWaterImpact(position, amount);
    }

    internal bool RegisterPlantFuel(Vector2Int cell, BurnablePlantMap2D owner, int plant,
        float seconds, float wetSeconds)
    {
        if (!IsReady || !ContainsCell(cell.x, cell.y) || cell.x <= 0 || cell.y <= 0 || cell.x >= width - 1 || cell.y >= height - 1) return false;
        int index = Index(cell.x, cell.y);
        plantFuel.TryGetValue(index, out PlantFuel head);
        plantFuel[index] = new PlantFuel
        {
            owner = owner, plant = plant, remaining = seconds, wetSeconds = wetSeconds, next = head
        };
        return true;
    }

    internal void UnregisterPlantFuel(BurnablePlantMap2D owner)
    {
        // Registration changes are infrequent; never enumerate this dictionary every frame.
        var keys = new List<int>(plantFuel.Keys);
        foreach (int key in keys)
        {
            PlantFuel head = plantFuel[key], previous = null, entry = head;
            while (entry != null)
            {
                if (entry.owner == owner)
                {
                    if (previous == null) head = entry.next;
                    else previous.next = entry.next;
                }
                else previous = entry;
                entry = entry.next;
            }
            if (head == null) plantFuel.Remove(key);
            else plantFuel[key] = head;
        }
    }

    private bool HasPlantFuel(int index) => plantFuel.ContainsKey(index);

    private bool CanBurnPlant(int index)
    {
        if (!ContainsCell(index % width, index / width) || !plantFuel.TryGetValue(index, out PlantFuel entry)) return false;
        for (; entry != null; entry = entry.next)
            if (entry.remaining > 0f && entry.wetUntil <= plantSimulationTime) return true;
        return false;
    }

    private void WetPlant(int index)
    {
        if (!plantFuel.TryGetValue(index, out PlantFuel entry)) return;
        for (; entry != null; entry = entry.next)
            entry.wetUntil = plantSimulationTime + entry.wetSeconds;
    }

    private bool BurnPlant(int index, out bool firstPass)
    {
        firstPass = false;
        if (!plantFuel.TryGetValue(index, out PlantFuel head)) return false;
        bool burning = false;
        PlantFuel entry = head, previous = null;
        while (entry != null)
        {
            if (entry.wetUntil <= plantSimulationTime && entry.remaining > 0f)
            {
                burning = true;
                if (entry.lastBurnTick != tickNumber)
                {
                    firstPass = true;
                    entry.lastBurnTick = tickNumber;
                    float spent = Mathf.Min(entry.remaining, 1f / ticksPerSecond);
                    entry.remaining -= spent;
                    if (entry.owner != null) entry.owner.OnPlantFuelBurned(entry.plant, spent);
                }
            }
            if (entry.remaining <= 0f)
            {
                if (previous == null) head = entry.next;
                else previous.next = entry.next;
            }
            else previous = entry;
            entry = entry.next;
        }
        if (head == null) plantFuel.Remove(index);
        else plantFuel[index] = head;
        return burning;
    }

    private void AddPlantSmoke(int x, int y, int amount)
    {
        // Vines may sit on a wall. Smoke must enter air, without turning that wall into liquid space.
        if (!solid[Index(x, y)]) { AddSmokeAtCell(x, y, amount); return; }
        if (y + 1 < height - 1 && !solid[Index(x, y + 1)]) AddSmokeAtCell(x, y + 1, amount);
        else if (x > 1 && !solid[Index(x - 1, y)]) AddSmokeAtCell(x - 1, y, amount);
        else if (x + 1 < width - 1 && !solid[Index(x + 1, y)]) AddSmokeAtCell(x + 1, y, amount);
    }

    private void SpreadPlantFireDiagonally(int x, int y)
    {
        // Pixel-art diagonal stems touch at corners. Only plant-to-plant gets diagonal spread.
        for (int dy = -1; dy <= 1; dy += 2)
        for (int dx = -1; dx <= 1; dx += 2)
        {
            int nx = x + dx, ny = y + dy;
            if (ContainsCell(nx, ny) && CanBurnPlant(Index(nx, ny)))
                TrySpreadFire(x, y, nx, ny, dy > 0);
        }
    }
}
