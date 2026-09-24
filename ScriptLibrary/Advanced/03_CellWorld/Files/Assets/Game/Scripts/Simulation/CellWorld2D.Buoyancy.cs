using UnityEngine;

public partial class CellWorld2D
{
    private void SimulateOilWaterSeparation()
{
    bool leftToRight = (tickNumber & 1) == 0;

    // Process from top to bottom.
    //
    // If oil moves upward, it moves into a row that has already
    // been processed, preventing it from climbing many cells
    // during a single tick.
    for (int cy = chunkCountY - 1; cy >= 0; cy--)
    {
        for (int cx = 0; cx < chunkCountX; cx++)
        {
            if (!IsChunkAwake(cx, cy))
                continue;

            GetChunkBounds(
                cx,
                cy,
                out int minX,
                out int maxX,
                out int minY,
                out int maxY
            );

            minX = Mathf.Max(1, minX);
            maxX = Mathf.Min(width - 2, maxX);

            minY = Mathf.Max(1, minY);
            maxY = Mathf.Min(height - 2, maxY);

            for (int y = maxY; y >= minY; y--)
            {
                int xStart = leftToRight ? minX : maxX;
                int xEnd = leftToRight ? maxX + 1 : minX - 1;
                int xStep = leftToRight ? 1 : -1;

                for (int x = xStart; x != xEnd; x += xStep)
                {
                    int i = Index(x, y);

                    if (solid[i] || fuel[i] == 0)
                        continue;

                    int firstDirection =
                        ((x + y + tickNumber) & 1) == 0
                            ? -1
                            : 1;

                    // -------------------------------------------------
                    // 1. Oil directly underneath water.
                    //
                    //     W
                    //     O
                    //
                    // becomes
                    //
                    //     O
                    //     W
                    // -------------------------------------------------

                    if (TryOilWaterSwap(
                        x,
                        y,
                        x,
                        y + 1))
                    {
                        continue;
                    }

                    // -------------------------------------------------
                    // 2. Diagonal separation.
                    //
                    // This is extremely important for side boundaries.
                    //
                    //       W
                    //     O
                    //
                    // oil can climb diagonally into the water.
                    // -------------------------------------------------

                    if (TryOilWaterSwap(
                        x,
                        y,
                        x + firstDirection,
                        y + 1))
                    {
                        continue;
                    }

                    if (TryOilWaterSwap(
                        x,
                        y,
                        x - firstDirection,
                        y + 1))
                    {
                        continue;
                    }

                    // -------------------------------------------------
                    // 3. Explicit side-by-side interaction.
                    //
                    // O W
                    //
                    // Water can push into the oil's old location,
                    // while the oil rolls upward over the water.
                    // -------------------------------------------------

                    if (TryOilRollOverWater(
                        x,
                        y,
                        firstDirection))
                    {
                        continue;
                    }

                    TryOilRollOverWater(
                        x,
                        y,
                        -firstDirection
                    );
                }
            }
        }
    }
    
}
    private bool TryOilWaterSwap(
        int oilX,
        int oilY,
        int waterX,
        int waterY)
    {
        if (!ContainsCell(oilX, oilY) ||
            !ContainsCell(waterX, waterY))
        {
            return false;
        }

        int oilCell =
            Index(oilX, oilY);

        int waterCell =
            Index(waterX, waterY);

        if (solid[oilCell] ||
            solid[waterCell])
        {
            return false;
        }

        if (fuel[oilCell] == 0 ||
            water[waterCell] == 0)
        {
            return false;
        }

        int amount =
            Mathf.Min(
                oilBuoyancyStrength,
                Mathf.Min(
                    fuel[oilCell],
                    water[waterCell]
                )
            );

        if (amount <= 0)
            return false;

        // Exchange equal volumes.
        //
        // This means the total amount inside each cell stays exactly
        // the same, so we're not creating or destroying liquid.

        fuel[oilCell] -= (byte)amount;
        water[oilCell] += (byte)amount;

        water[waterCell] -= (byte)amount;
        fuel[waterCell] += (byte)amount;

        WakeCell(oilX, oilY);
        WakeCell(waterX, waterY);

        textureNeedsRefresh = true;

        return true;
    }
    private bool TryOilRollOverWater(
        int oilX,
        int y,
        int direction)
    {
        int waterX =
            oilX + direction;

        if (!ContainsCell(waterX, y))
            return false;

        int oilCell =
            Index(oilX, y);

        int waterCell =
            Index(waterX, y);

        if (solid[waterCell])
            return false;

        // There must actually be water beside the oil.
        if (fuel[oilCell] == 0 ||
            water[waterCell] == 0)
        {
            return false;
        }

        // First try putting the oil above the water.
        //
        //        destination
        //             ↑
        //
        //        [ ][ ]
        //        [O][W]
        //
        int oilDestinationX =
            waterX;

        int oilDestinationY =
            y + 1;

        if (TryOilSideRoll(
                oilX,
                y,
                waterX,
                oilDestinationX,
                oilDestinationY))
        {
            return true;
        }

        // If that position is blocked, try rising directly
        // above the oil while water slides underneath it.
        //
        //        ↑
        //       oil
        //
        //      [ ]
        //      [O][W]
        //
        return TryOilSideRoll(
            oilX,
            y,
            waterX,
            oilX,
            y + 1
        );
    }
    private bool TryOilSideRoll(
        int oilX,
        int y,
        int waterX,
        int oilDestinationX,
        int oilDestinationY)
    {
        if (!ContainsCell(
                oilDestinationX,
                oilDestinationY))
        {
            return false;
        }

        int oilCell =
            Index(oilX, y);

        int waterCell =
            Index(waterX, y);

        int oilDestination =
            Index(
                oilDestinationX,
                oilDestinationY
            );

        if (solid[oilDestination])
            return false;

        int freeSpace =
            liquidCapacity
            - fuel[oilDestination]
            - water[oilDestination];

        if (freeSpace <= 0)
            return false;

        int amount =
            Mathf.Min(
                oilBuoyancyStrength,
                Mathf.Min(
                    fuel[oilCell],
                    Mathf.Min(
                        water[waterCell],
                        freeSpace
                    )
                )
            );

        if (amount <= 0)
            return false;

        // Oil climbs upward.
        fuel[oilCell] -=
            (byte)amount;

        fuel[oilDestination] +=
            (byte)amount;

        // Water moves into the space the oil left behind.
        water[waterCell] -=
            (byte)amount;

        water[oilCell] +=
            (byte)amount;

        WakeCell(oilX, y);
        WakeCell(waterX, y);

        WakeCell(
            oilDestinationX,
            oilDestinationY
        );

        textureNeedsRefresh = true;

        return true;
    }
}
