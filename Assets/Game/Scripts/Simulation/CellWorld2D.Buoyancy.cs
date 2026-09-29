using UnityEngine;

public partial class CellWorld2D
{
  
    //handle oil/water seperation
    private void SeparateOilAt(int x, int y)
    {
        
        int lower = Index(x, y), upper = Index(x, y + 1);
        
        //exit cases check
        if (solid[lower] || solid[upper] || movedFuel[lower] || movedWater[upper]) return;
        
        
        int amount = Mathf.Min(oilBuoyancyStrength, Mathf.Min(fuel[lower], water[upper]));
        if (amount <= 0) return;
        
        //drop water under fuel
        fuel[lower] -= (byte)amount;
        fuel[upper] += (byte)amount;
        water[upper] -= (byte)amount;
        water[lower] += (byte)amount;
        
        //flip flag to prevent from moving again
        movedFuel[lower] = movedFuel[upper] = true;
        movedWater[lower] = movedWater[upper] = true;
        
        //force update changed cells    
        WakeCell(x, y);
        WakeCell(x, y + 1);
        textureNeedsRefresh = true;
    }
}
