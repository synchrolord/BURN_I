public partial class CellWorld2D
{
    
    private void SimulateSteamAt(int cellX, int cellY)
    {
        SimulateGasAt(
            steam, movedSteam,
            steamVelocity,
            cellX, cellY,
            steamRisePerTick, steamSidewaysChance, steamDecayEveryTicks);
    }

   
    private void AddSteamAtCell(
        int cellX,
        int cellY,
        int amount)
    {
        AddGasAtCell(
            steam,
            movedSteam,
            cellX,
            cellY,
            amount);
    }
}