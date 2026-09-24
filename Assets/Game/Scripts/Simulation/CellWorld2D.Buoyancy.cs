using UnityEngine;

public partial class CellWorld2D
{
    // Fuel is lighter than water. Swap equal amounts in two adjacent cells;
    // their total amounts stay unchanged. No searching or multi-cell displacement.
    private void SeparateOilAt(int x, int y)
    {
        int lower = Index(x, y), upper = Index(x, y + 1);
        if (solid[lower] || solid[upper] || movedFuel[lower] || movedWater[upper]) return;
        int amount = Mathf.Min(oilBuoyancyStrength, Mathf.Min(fuel[lower], water[upper]));
        if (amount <= 0) return;
        fuel[lower] -= (byte)amount;
        fuel[upper] += (byte)amount;
        water[upper] -= (byte)amount;
        water[lower] += (byte)amount;
        movedFuel[lower] = movedFuel[upper] = true;
        movedWater[lower] = movedWater[upper] = true;
        WakeCell(x, y);
        WakeCell(x, y + 1);
        textureNeedsRefresh = true;
    }
}
