using UnityEngine;

public partial class CellWorld2D
{
    // Smoke rises once per tick, sometimes spreads sideways, and slowly fades.
    private void SimulateSmokeAt(int x, int y)
    {
        int i = Index(x, y);
        if (solid[i] || smoke[i] == 0 || movedSmoke[i]) return;
        movedSmoke[i] = true;
        // Trapped smoke must stay awake too, otherwise it would never reach its decay tick.
        WakeCell(x, y);
        if (smokeDecayEveryTicks > 0 && tickNumber % smokeDecayEveryTicks == 0)
        {
            smoke[i]--;
            textureNeedsRefresh = true;
        }
        MoveSmoke(x, y, x, y + 1, smokeRisePerTick);
        int direction = ((x + y + tickNumber) & 1) == 0 ? -1 : 1;
        int neighbor = Index(x + direction, y);
        if (smoke[neighbor] < smoke[i] && Random.value < smokeSidewaysChance)
            MoveSmoke(x, y, x + direction, y, 1);
    }

    private void MoveSmoke(int x, int y, int nextX, int nextY, int requested)
    {
        int from = Index(x, y), to = Index(nextX, nextY);
        if (solid[to]) return;
        int amount = Mathf.Min(smoke[from], Mathf.Min(requested, smokeCapacity - smoke[to]));
        if (amount <= 0) return;
        smoke[from] -= (byte)amount;
        smoke[to] += (byte)amount;
        movedSmoke[to] = true;
        WakeCell(x, y);
        WakeCell(nextX, nextY);
        textureNeedsRefresh = true;
    }

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
