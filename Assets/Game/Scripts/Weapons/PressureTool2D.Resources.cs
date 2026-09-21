using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class PressureTool2D
{
    private NozzleSettings GetCurrentNozzle()
    {
        switch (currentNozzleIndex)
        {
            case 0:
                return pilot;
            case 2:
                return wide;
            default:
                return jet;
        }
    }

    private bool HasEnoughResource(int requiredAmount)
    {
        if (unlimitedResourcesForTesting)
        {
            return true;
        }

        return currentMaterial == ToolMaterial.Water
            ? currentWater >= requiredAmount
            : currentFuel >= requiredAmount;
    }

    private bool TrySpendResource(int amount)
    {
        if (unlimitedResourcesForTesting || amount <= 0)
        {
            return true;
        }

        if (currentMaterial == ToolMaterial.Water)
        {
            if (currentWater < amount)
            {
                return false;
            }

            currentWater -= amount;
            return true;
        }

        if (currentFuel < amount)
        {
            return false;
        }

        currentFuel -= amount;
        return true;
    }

    public void AddWaterResource(int amount)
    {
        currentWater = Mathf.Clamp(
            currentWater + Mathf.Max(0, amount),
            0,
            maximumWater
        );
    }

    public void AddFuelResource(int amount)
    {
        currentFuel = Mathf.Clamp(
            currentFuel + Mathf.Max(0, amount),
            0,
            maximumFuel
        );
    }

    public void RefillAllResources()
    {
        currentWater = maximumWater;
        currentFuel = maximumFuel;
    }
}
