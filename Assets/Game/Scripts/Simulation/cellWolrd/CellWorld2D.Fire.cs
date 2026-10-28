using UnityEngine;

public partial class CellWorld2D
{
    private void SimulateFireAt(int x, int y)
    {
        int i = Index(x, y);
        if (movedFire[i]||fire[i]==0) return;
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
            
            //surface fire sometimes releases a small rising plume
            if (tickNumber % firePuffEveryTicks == 0)
                MakeFirePuff(x, y);
            
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

        else
        {
            //loose fire slowly loses its energy
            if (tickNumber % fireDecayEveryTicks == 0)
            {
                fire[i]--;
                AddPlantSmoke(x, y, 1);
                textureNeedsRefresh = true;

                if (fire[i] == 0) return;
            }

            MoveFire(x, y);
        }
    }

    
    
    private void MakeFirePuff(int x, int y)
    {
        if (Random.value > firePuffChance) return;

        for (int puff = 0; puff < firePuffAmount; puff++)
        {
            int side = Random.value < 0.5f ? -1 : 1;
            int newX = Random.value < fireWander ? x + side : x;
            
            //try diagonally first so the fire does not rise in straight lines
            if (TryMakeFire(newX, y + 1)) continue;
            TryMakeFire(x - side, y + 1);
        }

    }
    private bool TryMakeFire(int x, int y)
    {
        if (!IsOpenForFire(x, y)) return false;

        int i = Index(x, y);

        //add it after this simulation tick
        if (pendingFire[i] == 0)
            pendingFireIndices.Add(i);

        pendingFire[i] = (byte)firePuffPower;
        WakeCell(x, y);
        return true;
    }
    
    private void MoveFire(int x, int y)
    {
        //randomly pause
        if (Random.value < fireDrag) return;

        //decide random Side
        int side = Random.value < 0.5f ? -1 : 1;

        if (Random.value < fireWander &&
            TryMoveFire(x, y, x + side, y + 1))
            return;
        if (TryMoveFire(x, y, x, y + 1)) return;//up

        TryMoveFire(x, y, x - side, y + 1);//other side
        
        
    }
    private bool TryMoveFire(int fromX, int fromY, int toX, int toY)
    {
        if (!ContainsCell(toX, toY)) return false;

        int from = Index(fromX, fromY);
        int to = Index(toX, toY);

        //a loose fire bit ignites plant or fuel when it touches it
        if (water[to] == 0 && (HasPlant(to) || fuel[to] > 0))
        {
            fire[to] = (byte)Mathf.Max(fire[to], fire[from]);
            fire[from] = 0;
            movedFire[to] = true;

            WakeCell(fromX, fromY);
            WakeCell(toX, toY);
            textureNeedsRefresh = true;
            return true;
        }

        if (!IsOpenForFire(toX, toY)) return false;

        fire[to] = fire[from];
        fire[from] = 0;
        movedFire[to] = true;

        WakeCell(fromX, fromY);
        WakeCell(toX, toY);
        textureNeedsRefresh = true;
        return true;
    }   

    //cell must be empty for for fire plume
    private bool IsOpenForFire(int x, int y)
    {
        if (!ContainsCell(x, y)) return false;

        int i = Index(x, y);

        return !solid[i] &&
               water[i] == 0 &&
               fuel[i] == 0 &&
               fire[i] == 0 &&
               pendingFire[i] == 0 &&
               !HasPlant(i);
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

}
