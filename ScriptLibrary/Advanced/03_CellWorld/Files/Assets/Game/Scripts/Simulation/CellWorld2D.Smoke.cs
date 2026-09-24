using UnityEngine;

public partial class CellWorld2D
{
    private void SimulateSmoke()
    {
        bool decayNow =
            smokeDecayEveryTicks > 0 &&
            tickNumber % smokeDecayEveryTicks == 0;
        bool leftToRight = (tickNumber & 1) == 0;

        // Top chunks and top cells are processed first.
        for (int cy = chunkCountY - 1; cy >= 0; cy--)
        {
            int cxStart = leftToRight ? 0 : chunkCountX - 1;
            int cxEnd = leftToRight ? chunkCountX : -1;
            int cxStep = leftToRight ? 1 : -1;

            for (int cx = cxStart; cx != cxEnd; cx += cxStep)
            {
                if (!IsChunkAwake(cx, cy)) continue;
                GetChunkBounds(cx, cy, out int minX, out int maxX, out int minY, out int maxY);
                minX = Mathf.Max(1, minX); maxX = Mathf.Min(width - 2, maxX);
                minY = Mathf.Max(1, minY); maxY = Mathf.Min(height - 2, maxY);

                for (int y = maxY; y >= minY; y--)
                {
                    int xStart = leftToRight ? minX : maxX;
                    int xEnd = leftToRight ? maxX + 1 : minX - 1;
                    int xStep = leftToRight ? 1 : -1;

                    for (int x = xStart; x != xEnd; x += xStep)
                    {
                        int i = Index(x, y);
                        if (movedSmoke[i] || solid[i] || smoke[i] == 0) continue;
                        movedSmoke[i] = true;

                        if (decayNow)
                        {
                            smoke[i]--;
                            WakeCell(x, y);
                            textureNeedsRefresh = true;
                            if (smoke[i] == 0) continue;
                        }

                        MoveSmoke(x, y, x, y + 1, smokeRisePerTick);
                        if (smoke[i] == 0) continue;

                        int firstDirection = ((x + y + tickNumber) & 1) == 0 ? -1 : 1;
                        if (smoke[i] > 0 &&
                            UnityEngine.Random.value <= smokeSidewaysChance)
                        {
                            int left = Index(x - 1, y);
                            int right = Index(x + 1, y);

                            int direction;

                            if (smoke[left] < smoke[right])
                                direction = -1;
                            else if (smoke[right] < smoke[left])
                                direction = 1;
                            else
                                direction = firstDirection;

                            MoveSmoke(
                                x,
                                y,
                                x + direction,
                                y,
                                1
                            );
                        }

                        if (smoke[i] > 0 && UnityEngine.Random.value <= smokeSidewaysChance)
                            MoveSmoke(x, y, x + firstDirection, y, 1);
                    }
                }
            }
        }
    }

    private int MoveSmoke(int fromX, int fromY, int toX, int toY, int requested)
    {
        if (!ContainsCell(fromX, fromY) || !ContainsCell(toX, toY)) return 0;
        int from = Index(fromX, fromY);
        int to = Index(toX, toY);
        if (solid[to]) return 0;

        int amount = Mathf.Min(smoke[from], Mathf.Min(requested, smokeCapacity - smoke[to]));
        if (amount <= 0) return 0;

        smoke[from] -= (byte)amount;
        smoke[to] += (byte)amount;
        movedSmoke[from] = true;
        movedSmoke[to] = true;
        WakeCell(fromX, fromY);
        WakeCell(toX, toY);
        textureNeedsRefresh = true;
        return amount;
    }

    private void AddSmokeAtCell(int x, int y, int amount)
    {
        if (!ContainsCell(x, y)) return;
        int i = Index(x, y);
        smoke[i] = (byte)Mathf.Min(smokeCapacity, smoke[i] + amount);
        WakeCell(x, y);
        textureNeedsRefresh = true;
    }
}
