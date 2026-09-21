using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class PressureTool2D
{
    private void OnGUI()
    {
        if (!showDebugOverlay)
        {
            return;
        }

        string waterText = unlimitedResourcesForTesting
            ? "infinite"
            : $"{currentWater}/{maximumWater}";

        string fuelText = unlimitedResourcesForTesting
            ? "infinite"
            : $"{currentFuel}/{maximumFuel}";

        GUI.Box(
            new Rect(12f, 80f, 470f, 68f),
            $"{(currentMaterial == ToolMaterial.Water ? "WATER" : "FUEL / FLAME")}  |  Q: switch  |  Scroll: nozzle\n" +
            $"Material: {currentMaterial} | Nozzle: {GetCurrentNozzle().label}\n" +
            $"Water: {waterText} | Fuel: {fuelText}"
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (muzzle == null)
        {
            return;
        }

        NozzleSettings nozzle = GetCurrentNozzle();
        Vector2 origin = muzzle.position;
        Vector2 forward = muzzle.right;
        float halfAngle = nozzle.angleDegrees * 0.5f;
        float approximateRange = nozzle.speed * nozzle.lifetime *
            (currentMaterial == ToolMaterial.Water ? waterSpeedMultiplier * waterLifetimeMultiplier : 1f);

        Gizmos.color = currentMaterial == ToolMaterial.Water
            ? Color.cyan
            : new Color(1f, 0.35f, 0.05f);

        Gizmos.DrawLine(
            origin,
            origin + Rotate(forward, -halfAngle) * approximateRange
        );

        Gizmos.DrawLine(
            origin,
            origin + Rotate(forward, halfAngle) * approximateRange
        );

        Gizmos.DrawLine(
            origin,
            origin + forward * approximateRange
        );
    }
}
