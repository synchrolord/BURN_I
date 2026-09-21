using UnityEngine;

public sealed partial class PressureTool2D
{
    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[16];

    private void UpdateEmission(float deltaTime)
    {
        NozzleSettings nozzle = GetCurrentNozzle();
        bool usingTool = sprayHeld && HasEnoughResource(nozzle.resourceCostPerProbe);
        if (playerMotor != null) playerMotor.SetUsingTool(usingTool);
        if (!usingTool) return;

        float rate = nozzle.probesPerSecond;
        if (currentMaterial == ToolMaterial.Water) rate *= waterEmissionMultiplier;
        emissionBudget = Mathf.Min(emissionBudget + rate * deltaTime, 24f);
        while (emissionBudget >= 1f)
        {
            // Never overwrite a live drop: it still owns its water payload.
            int slot = FindFreeProbe();
            if (slot < 0 || !TrySpendResource(nozzle.resourceCostPerProbe)) break;
            SpawnProbe(nozzle, slot);
            emissionBudget -= 1f;
        }
    }

    private int FindFreeProbe()
    {
        for (int i = 0; i < probes.Length; i++)
        {
            int index = (nextProbeSlot + i) % probes.Length;
            if (probes[index].active) continue;
            nextProbeSlot = (index + 1) % probes.Length;
            return index;
        }
        return -1;
    }

    private void SpawnProbe(NozzleSettings nozzle, int slot)
    {
        SprayProbe probe = probes[slot];
        Vector2 forward = muzzle.right;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector2.right;
        Vector2 direction = Rotate(forward.normalized,
            Random.Range(-nozzle.angleDegrees * 0.5f, nozzle.angleDegrees * 0.5f));
        bool isWater = currentMaterial == ToolMaterial.Water;
        float speed = nozzle.speed * (isWater ? waterSpeedMultiplier : 1f);
        speed *= Random.Range(1f - nozzle.speedRandomness, 1f + nozzle.speedRandomness);
        Vector2 inheritedVelocity = playerBody != null
            ? playerBody.linearVelocity * inheritPlayerVelocity : Vector2.zero;

        probe.active = true;
        probe.material = currentMaterial;
        probe.position = (Vector2)muzzle.position + direction * cellWorld.CellSize * 0.75f;
        probe.velocity = direction * speed + inheritedVelocity;
        probe.initialLife = Mathf.Max(0.05f, nozzle.lifetime) * Random.Range(0.85f, 1.15f)
            * (isWater ? waterLifetimeMultiplier : 1f);
        probe.remainingLife = probe.initialLife;
        probe.noiseSeed = Random.Range(0f, 1000f);
        probe.radiusInCells = nozzle.radiusInCells;
        probe.unitsPerDeposit = nozzle.unitsPerDeposit;
        probe.remainingWater = Mathf.Max(1, nozzle.unitsPerDeposit * waterAmountMultiplier);
        probe.ignitionRadiusInCells = nozzle.ignitionRadiusInCells;
        if (!isWater) DepositFlame(probe, probe.position);
    }

    private void UpdateActiveProbes(float deltaTime)
    {
        for (int i = 0; i < probes.Length; i++)
        {
            SprayProbe probe = probes[i];
            if (!probe.active) continue;
            float step = Mathf.Min(deltaTime, probe.remainingLife);
            probe.remainingLife -= step;
            Vector2 previous = probe.position;

            if (probe.material == ToolMaterial.Water)
            {
                probe.velocity += Physics2D.gravity * waterGravityScale * step;
                probe.velocity *= 1f / (1f + waterDrag * step);
            }
            else
            {
                Vector2 direction = probe.velocity.sqrMagnitude > 0.0001f
                    ? probe.velocity.normalized : Vector2.right;
                Vector2 sideways = new Vector2(-direction.y, direction.x);
                float noise = Mathf.PerlinNoise(probe.noiseSeed, Time.time * flameTurbulenceSpeed) * 2f - 1f;
                probe.velocity += (Vector2.up * flameLift + sideways * noise * flameTurbulence) * step;
                probe.velocity *= 1f / (1f + flameDrag * step);
            }

            Vector2 next = previous + probe.velocity * step;
            bool hit = TryHitObstacle(previous, next, out Vector2 hitPosition);
            if (hit) next = hitPosition;
            DepositAlongSegment(probe, previous, next);
            probe.position = next;
            if (hit || probe.remainingLife <= 0f ||
                (probe.material == ToolMaterial.Water && probe.remainingWater <= 0))
            {
                if (probe.material == ToolMaterial.Water) DepositWater(probe, next);
                probe.active = false;
            }
        }
    }

    private bool TryHitObstacle(Vector2 start, Vector2 end, out Vector2 position)
    {
        position = end;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;
        int count = Physics2D.Linecast(start, end, filter, obstacleHits);
        float nearest = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = obstacleHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) ||
                (playerBody != null && hit.rigidbody == playerBody) || hit.distance >= nearest) continue;
            nearest = hit.distance;
            position = hit.point - (end - start).normalized * cellWorld.CellSize * 0.55f;
            found = true;
        }
        return found;
    }

    private void DepositAlongSegment(SprayProbe probe, Vector2 start, Vector2 end)
    {
        float spacing = Mathf.Max(cellWorld.CellSize * 0.5f, 0.01f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / spacing));
        for (int i = 1; i <= steps; i++)
        {
            Vector2 sample = Vector2.Lerp(start, end, i / (float)steps);
            if (probe.material == ToolMaterial.Flame) DepositFlame(probe, sample);
            // Water remains airborne until contact, except when cooling an existing fire.
            else if (probe.remainingWater > 0 && cellWorld.GetFireEnergy(sample) > 0)
                DepositWater(probe, sample);
        }
    }

    private void DepositWater(SprayProbe probe, Vector2 position)
    {
        if (probe.remainingWater <= 0) return;
        position += Random.insideUnitCircle * cellWorld.CellSize * waterDepositJitterInCells;
        // A drop carries a fixed payload. Frame rate and travel distance cannot create water.
        int remaining = probe.remainingWater;
        int radius = Mathf.Max(0, probe.radiusInCells);
        if (!cellWorld.TryWorldToCell(position, out Vector2Int center)) return;
        for (int y = -radius; y <= radius && remaining > 0; y++)
        for (int x = -radius; x <= radius && remaining > 0; x++)
        {
            if (x * x + y * y > radius * radius) continue;
            remaining -= cellWorld.ApplyWaterImpact(cellWorld.CellToWorld(center.x + x, center.y + y), remaining);
        }
        probe.remainingWater = remaining;
    }

    private void DepositFlame(SprayProbe probe, Vector2 position)
    {
        position += Random.insideUnitCircle * cellWorld.CellSize * flameDepositJitterInCells;
        int radius = Mathf.Max(probe.radiusInCells, probe.ignitionRadiusInCells);
        if (leaveUnburnedFuelResidue && Random.value < unburnedFuelResidueChance)
            cellWorld.AddFuel(position, probe.radiusInCells, unburnedFuelResidueAmount);
        cellWorld.Ignite(position, radius, 3);
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
        return new Vector2(direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine);
    }
}
