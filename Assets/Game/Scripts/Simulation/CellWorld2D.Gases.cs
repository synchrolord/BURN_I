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
        Vector2[] velocity,
        int cellX, int cellY,
        int transferPerTick, float turbulenceChance, int decayEveryTicks)
    {

        int current = Index(cellX, cellY);

        //don't simulate if cell is solid, empty, or has been simulated this update
        if (solid[current] || gas[current] == 0 || movedGas[current]) return;

        movedGas[current] = true;

        //cell remains awake to finish its updates
        WakeCell(cellX, cellY);

        //decay gas particles after some time
        if (decayEveryTicks > 0 && tickNumber % decayEveryTicks == 0)
        {
            gas[current]--;
            textureNeedsRefresh = true;
            if (gas[current] == 0)
            {
                velocity[current] = Vector2.zero;
                return;
            }
        }

        //measure velocity
        Vector2 nextVelocity = velocity[current];
        nextVelocity += Vector2.up * gasBuoyancy;


        // Introduce random sway
        if (UnityEngine.Random.value < turbulenceChance)
        {
            nextVelocity.x += UnityEngine.Random.Range(-1f, 1f);
        }

        // slow down and clamp the velocity
        nextVelocity *= 1f - gasVelocityDrag;

        nextVelocity = Vector2.ClampMagnitude(
            nextVelocity,
            gasMaximumSpeedInCells);


        //gas moves slowly upwards when they run out of velocity
        if (nextVelocity.sqrMagnitude < 0.01f)
            nextVelocity = Vector2.up;

        velocity[current] = nextVelocity;




        //find cell that current gas is trying to travel to and return its quarantines
        if (FindGasDestination(cellX, cellY, nextVelocity, 
                                out int destinationX, out int destinationY))
        {
            if (MoveGas(gas, movedGas, velocity, cellX, cellY, destinationX, destinationY, transferPerTick))
                return;
        }
        
        
        

    //move gas sideways if it fails to move following normal gas rules  
    if (FindGasSideways(cellX, cellY,nextVelocity,
                        out destinationX, out destinationY, out int surfaceDirection))
        {
            velocity[current] = new Vector2(surfaceDirection * Mathf.Max(1f, Mathf.Abs(nextVelocity.x)), 0f);

            if (MoveGas(gas, movedGas, velocity, cellX, cellY, destinationX, destinationY, transferPerTick))
                 return;
            
            velocity[current] = new Vector2(
                nextVelocity.x * 0.5f, 0f);

        }

    }
    
    
    
private bool FindGasSideways(
    int cellX, int cellY,
    Vector2 velocity,
    out int destinationX, out int destinationY,
    out int surfaceDirection)
{
    destinationX = cellX;
    destinationY = cellY;
    surfaceDirection = 0;

    if (velocity.y <= 0f)
        return false;

  
    int aboveY = cellY + 1;

    if (ContainsCell(cellX, aboveY))
    {
        int above = Index(cellX, aboveY);

        if (!solid[above] &&
            GetTotalGas(above) < gasCapacity)
        {
            return false;
        }
    }

    
    //decide direction based on velocity vector or randomly as fallback
    int preferredDirection;

    if (Mathf.Abs(velocity.x) >= 0.1f)
        preferredDirection = velocity.x < 0f
            ? -1 
            : 1;
    else
        preferredDirection =
            ((cellX + cellY + tickNumber) & 1) == 0
                ? -1
                : 1;

    //look at both sides to mov
    if (FindOPenSide(cellX, cellY, preferredDirection, out destinationX))
    { surfaceDirection = preferredDirection; return true; }

    if (FindOPenSide(cellX, cellY, -preferredDirection, out destinationX))
    { surfaceDirection = -preferredDirection; return true; }

    return false;
}


private bool FindOPenSide(
    int cellX, int cellY,
    int direction,
    out int destinationX)
{
    destinationX = cellX;

    //spread out gas till it gets to gas Spread Distance
    for (int distance = 1;
         distance <= gasSpreadDistance;
         distance++)
    {
        //check if cell is available and empty
        int nextX = cellX + direction * distance;

        if (!ContainsCell(nextX, cellY))
            break;

        int next = Index(nextX, cellY);
        
        if (solid[next])
            break;
        
        if (GetTotalGas(next) >= gasCapacity)
            continue;

        destinationX = nextX;
        return true;
    }

    return false;
}


    private bool   MoveGas(
        byte[] gas, bool[] movedGas,
        Vector2[] velocity,
        int cellX, int cellY,
        int nextCellX, int nextCellY, 
        int requested)
    {
        //get Array index of both cells
        if (!ContainsCell(nextCellX, nextCellY)) return false;
        int from = Index(cellX, cellY);
        int to = Index(nextCellX, nextCellY);

        if (solid[to])return false;
       
        
        //calculate space available in the "to" cell and transfer as much amount as possible without overfilling 
        int freeSpace = gasCapacity - GetTotalGas(to);
        int amount = Mathf.Min(gas[from], Mathf.Min(requested, freeSpace));
        if (amount <= 0) return false;
        //actual transfer
        
        int previousDestinationAmount = gas[to];
        Vector2 incomingVelocity = velocity[from];
        
        gas[from] -= (byte)amount;
        gas[to] += (byte)amount;
        
        //blend velocities
        velocity[to] =
            (velocity[to] * previousDestinationAmount + incomingVelocity * amount) / (previousDestinationAmount + amount);

        if (gas[from] == 0)
            velocity[from] = Vector2.zero;
        
       
        
        //prevent "to" cell from updating and wake both cells to be included in the update loop
        movedGas[to] = true;
        WakeCell(cellX, cellY);
        WakeCell(nextCellX, nextCellY);
        textureNeedsRefresh = true;


        return true;
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

    
        //trace the path the gas wants to take and return the cell it wants to go to
    private bool FindGasDestination(
        int cellX, int cellY,
        Vector2 velocity,
        out int destinationX, out int destinationY)
    {
        
        Vector2Int travel = new Vector2Int(
            Mathf.RoundToInt(velocity.x),
            Mathf.RoundToInt(velocity.y));

        //go up if velocity has run out
        if (travel == Vector2Int.zero)
            travel = Vector2Int.up;

        int steps = Mathf.Max(
            Mathf.Abs(travel.x),
            Mathf.Abs(travel.y));

        destinationX = cellX;
        destinationY = cellY;

        
        //calculate 
        for (int step = 1; step <= steps; step++)
        {
            int nextX = cellX + Mathf.RoundToInt(travel.x * step / (float)steps);
            int nextY = cellY + Mathf.RoundToInt(travel.y * step / (float)steps);

            //return if no movement happen
            if (nextX == destinationX && nextY == destinationY) continue;

            if (!ContainsCell(nextX, nextY)) break;

            //get the cell index
            int next = Index(nextX, nextY);

            if (solid[next] || GetTotalGas(next) >= gasCapacity)break;
            

            destinationX = nextX;
            destinationY = nextY;
        }

        return destinationX != cellX || destinationY != cellY;
    }


}