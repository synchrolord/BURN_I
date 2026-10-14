using System;
using UnityEngine;

public partial class CellWorld2D
{

    //total amount of all gases in a tile
    private int GetTotalGas(int index)
    {
        return smoke[index] + steam[index];
    }
    
    

    private void SimulateGasAt(
        byte[] gas, bool[] movedGas,
        int cellX, int cellY,
        int risePerTick, float sidewaysChance, int decayEveryTicks)
    {

        int current = Index(cellX, cellY);

        //don't simulate if cell is solid, empty, or has been simulated this update
        if (solid[current] || gas[current] == 0 || movedGas[current])
            return;

        movedGas[current] = true;

        //cell remains awake to finish its updates
        WakeCell(cellX, cellY);

        //decay gas particles after some time
        if (decayEveryTicks > 0 && tickNumber % decayEveryTicks == 0)
        {
            gas[current]--;
            textureNeedsRefresh = true;

            if (gas[current] == 0)
                return;
        }
        
        //try moving upwards
        MoveGas(
            gas, movedGas,
            cellX, cellY,
            cellX, cellY + 1,
            risePerTick);

        //switch between left and right sudo-randomly
        int direction = ((cellX + cellY + tickNumber) & 1) == 0
                ? -1
                : 1;
        
        //decide side and find that cell
        int sideX = cellX + direction;
        if (!ContainsCell(sideX, cellY)) return;
        int side = Index(sideX, cellY);
            
        
        if (gas[side] < gas[current] &&
            UnityEngine.Random.value < sidewaysChance)
        {
            MoveGas(
                gas, movedGas,
                cellX, cellY,
                sideX, cellY,
                1);
        }
    }
    
    private void MoveGas(
        byte[] gas, bool[] movedGas,
        int cellX, int cellY,
        int nextCellX, int nextCellY,
        int requested)
    {
        //get Array index of both cells
        if (!ContainsCell(nextCellX, nextCellY)) return;
        int from = Index(cellX, cellY);
        int to = Index(nextCellX, nextCellY);

        if (solid[to])return;
       
        
        //calculate space available in the "to" cell and transfer as much amount as possible without overfilling 
        int freeSpace = gasCapacity - GetTotalGas(to);
        int amount = Mathf.Min(gas[from], Mathf.Min(requested, freeSpace));
        if (amount <= 0) return;
        //actual transfer
        gas[from] -= (byte)amount;
        gas[to] += (byte)amount;
        
        
        //prevent "to" cell from updating and wake both cells to be included in the update loop
        movedGas[to] = true;
        WakeCell(cellX, cellY);
        WakeCell(nextCellX, nextCellY);
        textureNeedsRefresh = true;
        
        
    }


    private void AddGasAtCell(
        byte[] gas, bool[] movedGas,
        int cellX, int cellY,
        int amount)
    {
        //get cell's array index and make sure it is within cellWorld
        if (!ContainsCell(cellX, cellY) || amount <= 0) return;
        int index = Index(cellX, cellY);
        
        if (solid[index]) return;
        
        //add as much Gas as possible as to not overfill
        int accepted = Mathf.Min(amount, gasCapacity - GetTotalGas(index));
        if (accepted <= 0) return;
        
        //actual transfer of Gas
        gas[index] += (byte)accepted;
        movedGas[index] = true;

        WakeCell(cellX, cellY);
        textureNeedsRefresh = true;
    }



    private int AddGasCircle(
        byte[] gas,
        Vector2 position, int radius,
        int amountPerCell)
    {

        if (!TryWorldToCell(position, out Vector2Int center))
            return 0;

        int acceptedTotal = 0;
        int radiusSquared = radius * radius;

        //loop over Y from bottom
        for (int y = center.y - radius; y <= center.y + radius; y++)
        {
            //loop over x from bottom
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                //carve the circle and get cell indices for cells inside
                if (!IsInsideCircle(x, y, center, radiusSquared))continue;
                int index = Index(x, y);

                if (solid[index]) continue;
                
                // calculate amount to put in cell as to not overfill
                int accepted = Mathf.Min(amountPerCell, gasCapacity - GetTotalGas(index));

                if (accepted <= 0) continue;

                //actual transfer
                gas[index] += (byte)accepted;
                acceptedTotal += accepted;
                WakeCell(x, y);
                
            }
            
            
        }

        //mark texture for refresh
        if (acceptedTotal > 0)
            textureNeedsRefresh = true;

        return acceptedTotal;
    }
    
    
    
//fill cells around current cell for water turning to steam and plants burning
    private void AddGasNearCell(
        byte[] gas, bool[] movedGas,
        int cellX, int cellY,
        int amount)
    {
        if (!solid[Index(cellX, cellY)])
        {
            AddGasAtCell(
                gas, movedGas,
                cellX, cellY,
                amount);
    
            return;
        }
        
        if (ContainsCell(cellX, cellY + 1) &&
            !solid[Index(cellX, cellY + 1)])
        {
            AddGasAtCell(
                gas, movedGas,
                cellX, cellY + 1,
                amount);
        }
        else if (ContainsCell(cellX - 1, cellY) &&
                 !solid[Index(cellX - 1, cellY)])
        {
            AddGasAtCell(
                gas, movedGas,
                cellX - 1, cellY,
                amount);
        }
        else if (ContainsCell(cellX + 1, cellY) &&
                 !solid[Index(cellX + 1, cellY)])
        {
            AddGasAtCell(
                gas, movedGas,
                cellX + 1, cellY,
                amount);
        }
    }




}