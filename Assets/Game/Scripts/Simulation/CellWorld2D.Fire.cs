using UnityEngine;

public partial class CellWorld2D
{
    private void SimulateFireAt(int x, int y)
    {
        int i = Index(x, y);
        if (fire[i] == 0) return;
        WakeCell(x, y);
   
        // fire is spent to destroy water
        if (water[i] > 0)
        {
            //exhaust fire if possible
            int spent = Mathf.Min(water[i], waterUsedToExtinguish);
            water[i] -= (byte)spent;
            fire[i] = (byte)Mathf.Max(0, fire[i] - spent);
            
            //hot water touching fire makes a little smoke
            AddPlantSmoke(x, y, 1);
            textureNeedsRefresh = true;
            return;
        }
        
        
        
        
        //fire stays alive while it has fuel or plant matter
        bool plantBurning = HasPlant(i);
        bool hasFuel = fuel[i] > 0;
        
       //burning and fire spreading
        if (plantBurning || hasFuel)
        {
            fire[i] = 3;//max firePower
            
            //burn fuel every N ticks
            if (hasFuel && tickNumber % fuelBurnEveryTicks == 0)
            {
                fuel[i]--;
                AddPlantSmoke(x, y, smokeMadeByFire);
                textureNeedsRefresh = true;
            }
            
            // fire spreading logic hierarchy
            int spreadEvery = plantBurning ? plantSpreadEveryTicks : fireSpreadEveryTicks;
            if (tickNumber % spreadEvery == 0)
            {
                TrySpreadFire(x, y, x, y + 1, true);//try up
                TrySpreadFire(x, y, x - 1, y, false);//try left
                TrySpreadFire(x, y, x + 1, y, false);//try right
                TrySpreadFire(x, y, x, y - 1, false);//try down
                if (plantBurning) SpreadPlantFireDiagonally(x, y);
            }
        }
        
        
        //decay fire every N ticks
        else if (tickNumber % fireDecayEveryTicks == 0)
        {
            fire[i]--;
            AddPlantSmoke(x, y, 1);
            textureNeedsRefresh = true;
        }
    }

    
    private void TrySpreadFire(int fromX, int fromY, int toX, int toY, bool upward)
    {
        if (!ContainsCell(toX, toY)) return;
        int target = Index(toX, toY);
        bool plant = HasPlant(target);
        
        //fire cant spread to water
        if (fire[target] > 0 || water[target] > 0 || (!plant && (solid[target] || fuel[target] == 0))) return;
        
        float chance = plant ? plantSpreadChance : fireSpreadChance;
        if (upward) chance += plant ? plantUpwardSpreadBonus : upwardSpreadBonus;
        
        if (Random.value > Mathf.Clamp01(chance)) return;
        //queue each target once so new fire starts after this tick's cell processing
        if (pendingFire[target] == 0) pendingFireIndices.Add(target);
        pendingFire[target] = 2;
        WakeCell(toX, toY);
    }

    //apply queued fire without weakening any fire already in the cell
    private void ApplyPendingFire()
    {
        foreach (int i in pendingFireIndices)
        {
            if (ContainsCell(i % width, i / width)) fire[i] = (byte)Mathf.Max(fire[i], pendingFire[i]);
            pendingFire[i] = 0;
        }
        if (pendingFireIndices.Count > 0) textureNeedsRefresh = true;
        pendingFireIndices.Clear();
    }

    
    
    //fill the lighting arrays with the strongest awake chunks and return their count
    public int GetBrightestFireClusters(Vector2[] positions, float[] strengths, byte minimumEnergy)
    {
        if (!IsReady || positions == null || strengths == null) return 0;
        int limit = Mathf.Min(positions.Length, strengths.Length);
        if (limit == 0) return 0;
        int count = 0;
        
        //loop over chunks
        for (int n = 0; n < limit; n++) strengths[n] = 0f;
        for (int cy = 0; cy < chunkCountY; cy++)
        for (int cx = 0; cx < chunkCountX; cx++)
        {
            if (!IsChunkAwake(cx, cy)) continue;
            GetChunkBounds(cx, cy, out int minX, out int maxX, out int minY, out int maxY);
           
            //loop inside chunks
            float total = 0f, weightedX = 0f, weightedY = 0f;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                
                if (!ContainsCell(x, y)) continue;
                int energy = fire[Index(x, y)];
                if (energy < minimumEnergy) continue;
                total += energy;
                //give stronger fire more weight when finding the cluster's center
                weightedX += x * energy;
                weightedY += y * energy;
            }
            if (total <= 0f) continue;
            int slot = count;
            //when the output is full, replace its weakest cluster only if this one is stronger
            if (count == limit)
            {
                slot = 0;
                for (int n = 1; n < limit; n++)
                    if (strengths[n] < strengths[slot]) slot = n;
                if (total <= strengths[slot]) continue;
            }
            else count++;
            strengths[slot] = total;
            //round the weighted center to a cell and convert it to a world position
            positions[slot] = CellToWorld(Mathf.RoundToInt(weightedX / total), Mathf.RoundToInt(weightedY / total));
        }
        return count;
    }
}
