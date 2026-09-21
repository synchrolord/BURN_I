using UnityEngine;

public sealed partial class CellWorld2D
{
    private void SimulateFire()
    {
        bool burnFuelNow = tickNumber % fuelBurnEveryTicks == 0;
        bool decayNow = tickNumber % fireDecayEveryTicks == 0;
        bool spreadNow = tickNumber % fireSpreadEveryTicks == 0;

        for (int cy = 0; cy < chunkCountY; cy++)
        for (int cx = 0; cx < chunkCountX; cx++)
        {
            if (!IsChunkAwake(cx, cy)) continue;
            GetChunkBounds(cx, cy, out int minX, out int maxX, out int minY, out int maxY);
            minX = Mathf.Max(1, minX); maxX = Mathf.Min(width - 2, maxX);
            minY = Mathf.Max(1, minY); maxY = Mathf.Min(height - 2, maxY);

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int i = Index(x, y);
                if (fire[i] == 0) continue;
                WakeCell(x, y);

                // Water wins first.
                if (water[i] > 0)
                {
                    int spent = Mathf.Min(water[i], waterUsedToExtinguish);
                    water[i] -= (byte)spent;
                    fire[i] = (byte)Mathf.Max(0, fire[i] - spent);
                    AddSmokeAtCell(x, y, 1); // Temporary steam-like effect.
                    textureNeedsRefresh = true;
                    continue;
                }

                // Fuel keeps fire hot and is slowly consumed.
                if (fuel[i] > 0)
                {
                    if (burnFuelNow)
                    {
                        fuel[i]--;
                        fire[i] = 3;
                        AddSmokeAtCell(x, y, smokeMadeByFire);
                        textureNeedsRefresh = true;
                    }

                    if (spreadNow)
                    {
                        TrySpreadFire(x, y, x, y + 1, true);
                        TrySpreadFire(x, y, x - 1, y, false);
                        TrySpreadFire(x, y, x + 1, y, false);
                        TrySpreadFire(x, y, x, y - 1, false);
                    }
                }
                else if (decayNow)
                {
                    fire[i]--;
                    AddSmokeAtCell(x, y, 1);
                    textureNeedsRefresh = true;
                }
            }
        }
    }

    private void TrySpreadFire(int fromX, int fromY, int toX, int toY, bool upward)
    {
        if (!InBounds(toX, toY)) return;
        int target = Index(toX, toY);
        if (solid[target] || fuel[target] == 0 || fire[target] > 0) return;

        float chance = fireSpreadChance + (upward ? upwardSpreadBonus : 0f);
        if (UnityEngine.Random.value <= Mathf.Clamp01(chance)) ScheduleFire(toX, toY, 2);
    }

    private void ScheduleFire(int x, int y, byte energy)
    {
        int i = Index(x, y);
        if (pendingFire[i] == 0) pendingFireIndices.Add(i);
        pendingFire[i] = (byte)Mathf.Max(pendingFire[i], energy);
        WakeCell(x, y);
    }

    private void ApplyPendingFire()
    {
        for (int n = 0; n < pendingFireIndices.Count; n++)
        {
            int i = pendingFireIndices[n];
            fire[i] = (byte)Mathf.Max(fire[i], pendingFire[i]);
            pendingFire[i] = 0;
        }

        if (pendingFireIndices.Count > 0) textureNeedsRefresh = true;
        pendingFireIndices.Clear();
    }

    // Gives FireLightPool2D one weighted fire center per awake chunk.
    public int GetBrightestFireClusters(Vector2[] positions, float[] strengths, byte minimumEnergy)
    {
        if (positions == null || strengths == null) return 0;
        int maximumResults = Mathf.Min(positions.Length, strengths.Length);
        if (maximumResults == 0) return 0;
        for (int i = 0; i < maximumResults; i++) strengths[i] = 0f;
        int resultCount = 0;

        for (int cy = 0; cy < chunkCountY; cy++)
        for (int cx = 0; cx < chunkCountX; cx++)
        {
            if (!IsChunkAwake(cx, cy)) continue;
            GetChunkBounds(cx, cy, out int minX, out int maxX, out int minY, out int maxY);
            float strength = 0f, weightedX = 0f, weightedY = 0f;

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                byte energy = fire[Index(x, y)];
                if (energy < minimumEnergy) continue;
                strength += energy;
                weightedX += x * energy;
                weightedY += y * energy;
            }

            if (strength <= 0f) continue;
            Vector2 position = CellToWorld(
                Mathf.RoundToInt(weightedX / strength),
                Mathf.RoundToInt(weightedY / strength)
            );
            InsertFireCluster(positions, strengths, maximumResults, ref resultCount, position, strength);
        }

        return resultCount;
    }

    private void InsertFireCluster(Vector2[] positions, float[] strengths, int maximumResults,
        ref int resultCount, Vector2 position, float strength)
    {
        if (resultCount < maximumResults)
        {
            positions[resultCount] = position;
            strengths[resultCount] = strength;
            resultCount++;
            return;
        }

        int weakest = 0;
        for (int i = 1; i < maximumResults; i++)
            if (strengths[i] < strengths[weakest]) weakest = i;

        if (strength <= strengths[weakest]) return;
        positions[weakest] = position;
        strengths[weakest] = strength;
    }
}
