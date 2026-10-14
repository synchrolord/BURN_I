using UnityEngine;

public partial class PlayerVitals2D
{
    
    [Header("Poison")]
    [SerializeField, Range(1, 8)] private int maximumPoisonLevel = 4;
    [SerializeField, Min(1f)] private float basePoisonCapacity = 100f;
    [SerializeField, Min(0f)] private float poisonCapacityScaling = 0.5f;
    
    [SerializeField, Min(0f)] private float basePoisonDamagePerSecond = 1.5f;
    [SerializeField, Range(1f, 2f)] private float poisonDamageScaling = 1.5f;
    [SerializeField, Min(0.05f)] private float poisonDamageInterval = 0.5f;
    
    
    [SerializeField, Min(0f)] private float currentPoison;

    private float poisonDamageTimer;
    
    
    public int PoisonLevel => CalculatePoisonLevel();
    public float TotalPoison => currentPoison;
    public float MaximumTotalPoison => GetPoisonThreshold(maximumPoisonLevel);
    public bool HasPoison => currentPoison > 0.01f;
    public bool IsPoisoned => PoisonLevel > 0;
    public bool IsPoisonSaturated => currentPoison >= MaximumTotalPoison - 0.01f;

    
    
    public float CurrentPoison
    {
        get
        {
            int level = PoisonLevel;

            if (level >= maximumPoisonLevel)
                return MaxPoison;

            return currentPoison - GetPoisonThreshold(level);
        }
    }
    public float MaxPoison
    {
        get
        {
            int barLevel = Mathf.Min(
                PoisonLevel,
                maximumPoisonLevel - 1);

            return GetPoisonLevelCapacity(barLevel);
        }
    }
    
    
    private float GetPoisonLevelCapacity(int level)
    {
        return basePoisonCapacity *
               (1f + poisonCapacityScaling * level);
    }
    private float GetPoisonThreshold(int level)
    {
        float threshold = 0f;

        for (int i = 0; i < level; i++)
        {
            threshold += GetPoisonLevelCapacity(i);
        }

        return threshold;
    }
    private int CalculatePoisonLevel()
    {
        int level = 0;

        while (level < maximumPoisonLevel &&
               currentPoison >= GetPoisonThreshold(level + 1))
        {
            level++;
        }

        return level;
    }
    
    
    
    public float AddPoison(float amount)
    {
        if (IsDead || amount <= 0f || IsPoisonSaturated)
            return 0f;

        float resistance =
            Mathf.Clamp01(CurrentStage.poisonResistance);

        float previousPoison = currentPoison;

        currentPoison = Mathf.Min(
            MaximumTotalPoison,
            currentPoison + amount * (1f - resistance));

        return currentPoison - previousPoison;
    }

    public float CleansePoison(float amount)
    {
        if (!HasPoison || amount <= 0f)
            return 0f;

        float previousPoison = currentPoison;
        int previousLevel = PoisonLevel;

        currentPoison = Mathf.Max(
            0f,
            currentPoison - amount);

        // Crossing below level one completely removes the remaining exposure.
        if (previousLevel > 0 && PoisonLevel == 0)
        {
            currentPoison = 0f;
        }

        return previousPoison - currentPoison;
    }
    private void UpdatePoisonDamage(float seconds)
    {
        if (IsDead || !IsPoisoned)
        {
            poisonDamageTimer = 0f;
            return;
        }

        poisonDamageTimer -= seconds;

        if (poisonDamageTimer > 0f)
            return;

        poisonDamageTimer = poisonDamageInterval;

        float levelStrength = PoisonLevel;

        if (PoisonLevel < maximumPoisonLevel)
        {
            levelStrength += CurrentPoison / MaxPoison;
        }

        float damagePerSecond =
            basePoisonDamagePerSecond *
            Mathf.Pow(levelStrength, poisonDamageScaling);

        ApplyDamage(
            damagePerSecond * poisonDamageInterval);
    }
    
    
    
    private void ResetPoisonForSpawn()
    {
        currentPoison /= 2f;
        poisonDamageTimer = 0f;
    }
    
}
