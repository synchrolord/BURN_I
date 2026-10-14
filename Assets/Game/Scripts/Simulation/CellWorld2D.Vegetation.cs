using System.Collections.Generic;
using UnityEngine;

public partial class CellWorld2D
{
    
        
    //store one plant's fuel and wetness in one simulation cell
    private sealed class PlantFuel
    {
        public BurnablePlantMap2D owner;
        public int plant;
        public float remaining, wetSeconds;
        public double wetUntil;
        public int lastBurnTick = -1;
        //link to another plant entry overlapping the same cell
        public PlantFuel next;
    }

    //map each occupied cell index to the first plant entry in its chain
    private readonly Dictionary<int, PlantFuel> plantFuel = new Dictionary<int, PlantFuel>();
    private double plantSimulationTime;
    public int PlantFuelCellCount => plantFuel.Count;

    //apply water at the contact point only if a plant is registered there
    public int ApplyPlantWaterImpact(Vector2 position, int amount)
    {
        if (!IsReady || !TryWorldToCell(position, out Vector2Int cell) || !HasPlantFuel(Index(cell.x, cell.y))) return 0;
        return ApplyWaterImpact(position, amount);
    }

    //register plant fuel separately from the liquid fuel array
    internal bool RegisterPlantFuel(Vector2Int cell, BurnablePlantMap2D owner, int plant,
        float seconds, float wetSeconds)
    {
        if (!IsReady || !ContainsCell(cell.x, cell.y) || cell.x <= 0 || cell.y <= 0 || cell.x >= width - 1 || cell.y >= height - 1) return false;
        int index = Index(cell.x, cell.y);
        plantFuel.TryGetValue(index, out PlantFuel head);
        //put the new entry at the front and keep any overlapping plants linked
        plantFuel[index] = new PlantFuel
        {
            owner = owner, plant = plant, remaining = seconds, wetSeconds = wetSeconds, next = head
        };
        return true;
    }

    //remove entries belonging to this plant map without removing other maps' plants
    internal void UnregisterPlantFuel(BurnablePlantMap2D owner)
    {
        // Registration changes are infrequent; never enumerate this dictionary every frame.
        //copy the keys so dictionary entries can be removed while looping
        var keys = new List<int>(plantFuel.Keys);
        foreach (int key in keys)
        {
            PlantFuel head = plantFuel[key], previous = null, entry = head;
            while (entry != null)
            {
                //remove this entry by linking the previous entry to the next one
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

    //check whether this cell has registered plant entries
    private bool HasPlantFuel(int index) => plantFuel.ContainsKey(index);

    //look for an overlapping plant that has fuel left and is no longer wet
    private bool CanBurnPlant(int index)
    {
        if (!ContainsCell(index % width, index / width) || !plantFuel.TryGetValue(index, out PlantFuel entry)) return false;
        for (; entry != null; entry = entry.next)
            if (entry.remaining > 0f && entry.wetUntil <= plantSimulationTime) return true;
        return false;
    }

    //set the wetness expiry time for every plant entry in this cell
    private void WetPlant(int index)
    {
        if (!plantFuel.TryGetValue(index, out PlantFuel entry)) return;
        for (; entry != null; entry = entry.next)
            entry.wetUntil = plantSimulationTime + entry.wetSeconds;
    }

    //consume plant burn time and report the damage to its owning map
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
                //consume each entry only once per tick
                if (entry.lastBurnTick != tickNumber)
                {
                    firstPass = true;
                    entry.lastBurnTick = tickNumber;
                    //spend up to one tick of remaining burn time
                    float spent = Mathf.Min(entry.remaining, 1f / ticksPerSecond);
                    entry.remaining -= spent;
                    if (entry.owner != null) entry.owner.OnPlantFuelBurned(entry.plant, spent);
                }
            }
            //unlink entries that have used all their plant fuel
            if (entry.remaining <= 0f)
            {
                if (previous == null) head = entry.next;
                else previous.next = entry.next;
            }
            else previous = entry;
            entry = entry.next;
        }
        //remove the cell key if its plant chain is now empty
        if (head == null) plantFuel.Remove(index);
        else plantFuel[index] = head;
        return burning;
    }

    private void AddPlantSmoke(int cellX, int cellY, int amount)
    {
        AddGasNearCell(
            smoke, movedSmoke,
            cellX, cellY,
            amount);
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