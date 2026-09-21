using UnityEngine;

public sealed partial class CellWorld2D
{
    private void SimulateLiquid(
        byte[] liquid,
        byte[] otherLiquid,
        bool[] moved,
        float viscosity,
        int spreadSearchDistance,
        int sidewaysPasses)
    {
        // Thick liquids transfer less material and spread less aggressively.
        int downFlow = Mathf.Max(
            1,
            Mathf.RoundToInt(
                Mathf.Lerp(liquidCapacity, 2f, viscosity)
            )
        );

        int sideFlow = Mathf.Max(
            1,
            Mathf.RoundToInt(
                Mathf.Lerp(liquidCapacity / 2f, 1f, viscosity)
            )
        );

        int requiredDifference = Mathf.RoundToInt(
            Mathf.Lerp(1f, 4f, viscosity)
        );

        float sidewaysChance = Mathf.Lerp(
            1f,
            0.20f,
            viscosity
        );

        spreadSearchDistance = Mathf.Max(1, spreadSearchDistance);
        sidewaysPasses = Mathf.Max(1, sidewaysPasses);

        bool leftToRight = (tickNumber & 1) == 0;

        // Bottom chunks and bottom cells are processed first.
        for (int cy = 0; cy < chunkCountY; cy++)
        {
            int cxStart = leftToRight ? 0 : chunkCountX - 1;
            int cxEnd = leftToRight ? chunkCountX : -1;
            int cxStep = leftToRight ? 1 : -1;

            for (int cx = cxStart; cx != cxEnd; cx += cxStep)
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

                for (int y = minY; y <= maxY; y++)
                {
                    int xStart = leftToRight ? minX : maxX;
                    int xEnd = leftToRight ? maxX + 1 : minX - 1;
                    int xStep = leftToRight ? 1 : -1;

                    for (int x = xStart; x != xEnd; x += xStep)
                    {
                        int i = Index(x, y);

                        if (moved[i] ||
                            solid[i] ||
                            liquid[i] == 0)
                        {
                            continue;
                        }

                        moved[i] = true;

                        // -------------------------------------------------
                        // 1. Gravity
                        // -------------------------------------------------

                        MoveLiquid(
                            liquid,
                            otherLiquid,
                            moved,
                            x,
                            y,
                            x,
                            y - 1,
                            downFlow
                        );

                        if (liquid[i] == 0)
                            continue;

                        // -------------------------------------------------
                        // 2. Diagonal falling
                        // -------------------------------------------------

                        int firstDirection =
                            ((x + y + tickNumber) & 1) == 0
                                ? -1
                                : 1;

                        MoveLiquid(
                            liquid,
                            otherLiquid,
                            moved,
                            x,
                            y,
                            x + firstDirection,
                            y - 1,
                            downFlow
                        );

                        MoveLiquid(
                            liquid,
                            otherLiquid,
                            moved,
                            x,
                            y,
                            x - firstDirection,
                            y - 1,
                            downFlow
                        );

                        if (liquid[i] == 0)
                            continue;

                        // -------------------------------------------------
                        // 3. Sideways flow
                        // -------------------------------------------------

                        if (UnityEngine.Random.value > sidewaysChance)
                            continue;

                        int dropDirection =
                            FindNearbyDropDirection(
                                liquid,
                                otherLiquid,
                                x,
                                y,
                                spreadSearchDistance,
                                out int dropDistance
                            );

                        // If there is somewhere nearby where the liquid
                        // can fall, actively push liquid toward it.
                        if (dropDirection != 0)
                        {
                            bool flowedTowardDrop =
                                FlowTowardDrop(
                                    liquid,
                                    otherLiquid,
                                    moved,
                                    x,
                                    y,
                                    dropDirection,
                                    dropDistance,
                                    sideFlow,
                                    downFlow,
                                    sidewaysPasses
                                );

                            if (flowedTowardDrop)
                                continue;
                        }

                        // -------------------------------------------------
                        // 4. No nearby drop:
                        // just equalize with immediate neighbours.
                        // -------------------------------------------------

                        LevelLiquid(
                            liquid,
                            otherLiquid,
                            moved,
                            x,
                            y,
                            x + firstDirection,
                            y,
                            sideFlow,
                            requiredDifference
                        );

                        LevelLiquid(
                            liquid,
                            otherLiquid,
                            moved,
                            x,
                            y,
                            x - firstDirection,
                            y,
                            sideFlow,
                            requiredDifference
                        );
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------
    // Finds whether the nearest useful drop is left or right.
    //
    // Returns:
    // -1 = left
    //  1 = right
    //  0 = no nearby drop
    // ---------------------------------------------------------

    private int FindNearbyDropDirection(
        byte[] liquid,
        byte[] otherLiquid,
        int x,
        int y,
        int searchDistance,
        out int distance)
    {
        int leftDistance =
            FindDropDistance(
                liquid,
                otherLiquid,
                x,
                y,
                -1,
                searchDistance
            );

        int rightDistance =
            FindDropDistance(
                liquid,
                otherLiquid,
                x,
                y,
                1,
                searchDistance
            );

        if (leftDistance == 0 &&
            rightDistance == 0)
        {
            distance = 0;
            return 0;
        }

        if (leftDistance == 0)
        {
            distance = rightDistance;
            return 1;
        }

        if (rightDistance == 0)
        {
            distance = leftDistance;
            return -1;
        }

        if (leftDistance < rightDistance)
        {
            distance = leftDistance;
            return -1;
        }

        if (rightDistance < leftDistance)
        {
            distance = rightDistance;
            return 1;
        }

        // Same distance on both sides.
        // Alternate preference so we don't bias one direction.
        int direction =
            ((x + y + tickNumber) & 1) == 0
                ? -1
                : 1;

        distance = leftDistance;

        return direction;
    }

    // ---------------------------------------------------------
    // Search horizontally until:
    //
    // - we find a cell that has somewhere below it to fall into,
    // - we hit a solid,
    // - or we reach the maximum search distance.
    // ---------------------------------------------------------

    private int FindDropDistance(
        byte[] liquid,
        byte[] otherLiquid,
        int startX,
        int y,
        int direction,
        int searchDistance)
    {
        for (int distance = 1;
             distance <= searchDistance;
             distance++)
        {
            int x = startX + direction * distance;

            if (!InBounds(x, y))
                break;

            int current = Index(x, y);

            // A wall blocks horizontal searching.
            if (solid[current])
                break;

            int belowY = y - 1;

            if (!InBounds(x, belowY))
                continue;

            int below = Index(x, belowY);

            if (solid[below])
                continue;

            int freeBelow =
                liquidCapacity
                - liquid[below]
                - otherLiquid[below];

            if (freeBelow > 0)
                return distance;
        }

        return 0;
    }

    // ---------------------------------------------------------
    // Water found a drop some distance away.
    //
    // First empty some liquid downward at the destination.
    // Then propagate that free space backwards through the
    // liquid row.
    //
    // This is what makes water spread rapidly over surfaces
    // instead of behaving like syrup.
    // ---------------------------------------------------------

    private bool FlowTowardDrop(
        byte[] liquid,
        byte[] otherLiquid,
        bool[] moved,
        int startX,
        int y,
        int direction,
        int dropDistance,
        int maximumSideFlow,
        int maximumDownFlow,
        int passes)
    {
        bool changedAtAll = false;

        int targetX =
            startX + direction * dropDistance;

        for (int pass = 0; pass < passes; pass++)
        {
            bool changedThisPass = false;

            // Let the target location pour downward first.
            int dropped =
                MoveLiquid(
                    liquid,
                    otherLiquid,
                    moved,
                    targetX,
                    y,
                    targetX,
                    y - 1,
                    maximumDownFlow
                );

            if (dropped > 0)
            {
                changedThisPass = true;
                changedAtAll = true;
            }

            // Work backwards from the drop.
            //
            // Example:
            //
            // 8 8 8 0
            //
            // becomes something closer to:
            //
            // 7 7 6 4
            //
            // rather than waiting several ticks for the
            // empty space to crawl left one cell at a time.
            for (int distance = dropDistance - 1;
                 distance >= 0;
                 distance--)
            {
                int fromX =
                    startX + direction * distance;

                int toX =
                    fromX + direction;

                int from = Index(fromX, y);
                int to = Index(toX, y);

                if (solid[to] ||
                    liquid[from] == 0)
                {
                    continue;
                }

                int freeSpace =
                    liquidCapacity
                    - liquid[to]
                    - otherLiquid[to];

                if (freeSpace <= 0)
                    continue;

                int difference =
                    liquid[from] - liquid[to];

                if (difference <= 0)
                    continue;

                int requested =
                    Mathf.Min(
                        maximumSideFlow,
                        (difference + 1) / 2
                    );

                int transferred =
                    MoveLiquid(
                        liquid,
                        otherLiquid,
                        moved,
                        fromX,
                        y,
                        toX,
                        y,
                        requested
                    );

                if (transferred > 0)
                {
                    changedThisPass = true;
                    changedAtAll = true;
                }
            }

            if (!changedThisPass)
                break;
        }

        return changedAtAll;
    }

    // ---------------------------------------------------------
    // Normal local horizontal leveling.
    // ---------------------------------------------------------

    private void LevelLiquid(
        byte[] liquid,
        byte[] otherLiquid,
        bool[] moved,
        int fromX,
        int fromY,
        int toX,
        int toY,
        int maximumFlow,
        int requiredDifference)
    {
        if (!InBounds(toX, toY))
            return;

        int from = Index(fromX, fromY);
        int to = Index(toX, toY);

        int difference =
            liquid[from] - liquid[to];

        if (difference <= requiredDifference)
            return;

        int requested =
            Mathf.Min(
                maximumFlow,
                (difference + 1) / 2
            );

        MoveLiquid(
            liquid,
            otherLiquid,
            moved,
            fromX,
            fromY,
            toX,
            toY,
            requested
        );
    }

    // ---------------------------------------------------------
    // Transfers actual liquid units between two cells.
    // ---------------------------------------------------------

    private int MoveLiquid(
        byte[] liquid,
        byte[] otherLiquid,
        bool[] moved,
        int fromX,
        int fromY,
        int toX,
        int toY,
        int requested)
    {
        if (requested <= 0 ||
            !InBounds(toX, toY))
        {
            return 0;
        }

        int from = Index(fromX, fromY);
        int to = Index(toX, toY);

        if (solid[to])
            return 0;

        int freeSpace =
            liquidCapacity
            - liquid[to]
            - otherLiquid[to];

        int amount =
            Mathf.Min(
                liquid[from],
                Mathf.Min(
                    requested,
                    freeSpace
                )
            );

        if (amount <= 0)
            return 0;

        liquid[from] -= (byte)amount;
        liquid[to] += (byte)amount;

        moved[from] = true;
        moved[to] = true;

        WakeCell(fromX, fromY);
        WakeCell(toX, toY);

        textureNeedsRefresh = true;

        return amount;
    }

    // ---------------------------------------------------------
    // Painting / adding liquids.
    // ---------------------------------------------------------

    private int AddLiquidCircle(
        byte[] liquid,
        byte[] otherLiquid,
        Vector2 position,
        int radius,
        int amountPerCell)
    {
        if (!TryWorldToCell(
                position,
                out Vector2Int center))
        {
            return 0;
        }

        radius = Mathf.Max(0, radius);
        amountPerCell =
            Mathf.Max(1, amountPerCell);

        int acceptedTotal = 0;

        int radiusSquared =
            radius * radius;

        for (int y = center.y - radius;
             y <= center.y + radius;
             y++)
        {
            for (int x = center.x - radius;
                 x <= center.x + radius;
                 x++)
            {
                if (!IsInsideCircle(
                        x,
                        y,
                        center,
                        radiusSquared))
                {
                    continue;
                }

                int i = Index(x, y);

                if (solid[i])
                    continue;

                int freeSpace =
                    liquidCapacity
                    - liquid[i]
                    - otherLiquid[i];

                int accepted =
                    Mathf.Min(
                        amountPerCell,
                        freeSpace
                    );

                if (accepted <= 0)
                    continue;

                liquid[i] +=
                    (byte)accepted;

                acceptedTotal +=
                    accepted;

                WakeCell(x, y);
            }
        }

        if (acceptedTotal > 0)
            textureNeedsRefresh = true;

        return acceptedTotal;
    }
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
        if (!InBounds(oilX, oilY) ||
            !InBounds(waterX, waterY))
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

        if (!InBounds(waterX, y))
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
        if (!InBounds(
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