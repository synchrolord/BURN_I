using UnityEngine;

public partial class CellWorld2D
{
  
    private void SimulateSmokeAt(int x, int y)
    {
        int i = Index(x, y);
        if (solid[i] || smoke[i] == 0 || movedSmoke[i]) return;
        movedSmoke[i] = true;
        
        //Smoke always updates so it can decay
        WakeCell(x, y);
        
        //decay smoke
        if (smokeDecayEveryTicks > 0 && tickNumber % smokeDecayEveryTicks == 0)
        {
            smoke[i]--;
            textureNeedsRefresh = true;
        }
        
        // move smoke upwards with random diagonal
        MoveSmoke(x, y, x, y + 1, smokeRisePerTick);
        int direction = ((x + y + tickNumber) & 1) == 0 ? -1 : 1;
        int neighbor = Index(x + direction, y);
        
        //homogenize smoke
        if (smoke[neighbor] < smoke[i] && Random.value < smokeSidewaysChance)
            MoveSmoke(x, y, x + direction, y, 1);
    }

    
    //transfer smoke between cells without exceeding the destination capacity
    private void MoveSmoke(int x, int y, int nextX, int nextY, int requested)
    {
        int from = Index(x, y), to = Index(nextX, nextY);
        if (solid[to]) return;
        //limit the transfer by available smoke, requested amount and free space
        int amount = Mathf.Min(smoke[from], Mathf.Min(requested, smokeCapacity - smoke[to]));
        if (amount <= 0) return;
      
        smoke[from] -= (byte)amount;
        smoke[to] += (byte)amount;
        movedSmoke[to] = true;
        
        WakeCell(x, y);
        WakeCell(nextX, nextY);
        textureNeedsRefresh = true;
    }

    //add smoke to one valid open cell and prevent it moving again this tick
    private void AddSmokeAtCell(int x, int y, int amount)
    {
        if (!ContainsCell(x, y)) return;
        int i = Index(x, y);
        if (solid[i] || amount <= 0) return;
        
        smoke[i] = (byte)Mathf.Min(smokeCapacity, smoke[i] + amount);
        movedSmoke[i] = true;
        
        WakeCell(x, y);
        textureNeedsRefresh = true;
    }
}
