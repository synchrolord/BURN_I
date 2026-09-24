using UnityEngine;

public partial class CellWorld2D
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

            if (!ContainsCell(x, y))
                break;

            int current = Index(x, y);

            // A wall blocks horizontal searching.
            if (solid[current])
                break;

            int belowY = y - 1;

            if (!ContainsCell(x, belowY))
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
        if (!ContainsCell(toX, toY))
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
            !ContainsCell(fromX, fromY) || !ContainsCell(toX, toY))
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
}
