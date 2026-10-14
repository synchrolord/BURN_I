using UnityEngine;

public partial class CellWorld2D
{
  
    private void SimulateSmokeAt(int cellX, int cellY)
    {
        SimulateGasAt(
            smoke, movedSmoke,
            cellX, cellY,
            smokeRisePerTick, smokeSidewaysChance, smokeDecayEveryTicks);
    }


    private void AddSmokeAtCell(int cellX, int cellY, int amount)
    {
        AddGasAtCell(
            smoke, movedSmoke,
            cellX, cellY,
            amount);
    }
}
