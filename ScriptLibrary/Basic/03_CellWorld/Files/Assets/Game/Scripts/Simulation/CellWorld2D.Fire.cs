using UnityEngine;

public sealed partial class CellWorld2D
{
    private void SimulateFireAt(int x, int y)
    {
        int i = Index(x, y);
        if (fire[i] == 0) return;
        WakeCell(x, y);
        // Water cools first, even when fuel is present.
        if (water[i] > 0)
        {
            int spent = Mathf.Min(water[i], waterUsedToExtinguish);
            water[i] -= (byte)spent;
            fire[i] = (byte)Mathf.Max(0, fire[i] - spent);
            WetPlant(i);
            AddPlantSmoke(x, y, 1);
            textureNeedsRefresh = true;
            return;
        }

        bool plantBurning = BurnPlant(i, out bool firstPlantPass);
        bool hasFuel = fuel[i] > 0;
        if (plantBurning || hasFuel)
        {
            fire[i] = 3;
            if (tickNumber % fuelBurnEveryTicks == 0)
            {
                if (hasFuel) fuel[i]--;
                if (hasFuel || firstPlantPass) AddPlantSmoke(x, y, smokeMadeByFire);
                textureNeedsRefresh = true;
            }
            if (tickNumber % fireSpreadEveryTicks == 0)
            {
                TrySpreadFire(x, y, x, y + 1, true);
                TrySpreadFire(x, y, x - 1, y, false);
                TrySpreadFire(x, y, x + 1, y, false);
                TrySpreadFire(x, y, x, y - 1, false);
                if (plantBurning) SpreadPlantFireDiagonally(x, y);
            }
        }
        else if (tickNumber % fireDecayEveryTicks == 0)
        {
            fire[i]--;
            AddPlantSmoke(x, y, 1);
            textureNeedsRefresh = true;
        }
    }

    private void TrySpreadFire(int fromX, int fromY, int toX, int toY, bool upward)
    {
        if (!InBounds(toX, toY)) return;
        int target = Index(toX, toY);
        bool plant = CanBurnPlant(target);
        if (fire[target] > 0 || water[target] > 0 || (!plant && (solid[target] || fuel[target] == 0))) return;
        float chance = fireSpreadChance + (upward ? upwardSpreadBonus : 0f);
        if (Random.value > Mathf.Clamp01(chance)) return;
        if (pendingFire[target] == 0) pendingFireIndices.Add(target);
        pendingFire[target] = 2;
        WakeCell(toX, toY);
    }

    private void ApplyPendingFire()
    {
        foreach (int i in pendingFireIndices)
        {
            fire[i] = (byte)Mathf.Max(fire[i], pendingFire[i]);
            pendingFire[i] = 0;
        }
        if (pendingFireIndices.Count > 0) textureNeedsRefresh = true;
        pendingFireIndices.Clear();
    }

    // FireLightPool2D reuses a few Unity lights. Each awake chunk offers one fire center.
    public int GetBrightestFireClusters(Vector2[] positions, float[] strengths, byte minimumEnergy)
    {
        if (!IsReady || positions == null || strengths == null) return 0;
        int limit = Mathf.Min(positions.Length, strengths.Length);
        if (limit == 0) return 0;
        int count = 0;
        for (int n = 0; n < limit; n++) strengths[n] = 0f;
        for (int cy = 0; cy < chunkCountY; cy++)
        for (int cx = 0; cx < chunkCountX; cx++)
        {
            if (!IsChunkAwake(cx, cy)) continue;
            GetChunkBounds(cx, cy, out int minX, out int maxX, out int minY, out int maxY);
            float total = 0f, weightedX = 0f, weightedY = 0f;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int energy = fire[Index(x, y)];
                if (energy < minimumEnergy) continue;
                total += energy;
                weightedX += x * energy;
                weightedY += y * energy;
            }
            if (total <= 0f) continue;
            int slot = count;
            if (count == limit)
            {
                slot = 0;
                for (int n = 1; n < limit; n++)
                    if (strengths[n] < strengths[slot]) slot = n;
                if (total <= strengths[slot]) continue;
            }
            else count++;
            strengths[slot] = total;
            positions[slot] = CellToWorld(Mathf.RoundToInt(weightedX / total), Mathf.RoundToInt(weightedY / total));
        }
        return count;
    }
}
