using System;
using UnityEngine;
[CreateAssetMenu(fileName = "Infection Profile", menuName = "Infection Profile")]

public class InfectionProfile : ScriptableObject{

    public const int InfectionCount =6;

    [Serializable]
    
    //create custom infection stage 
    public class InfectionStage
    {
        public string name = "Healthy";
        
        //multipliers on players stats
        [Min(0.1f)] public float healthMultiplier = 1f;
        [Min(0.1f)] public float visionMultiplier = 1f;
        [Range(0f, 1f)] public float aggroMultiplier = 1f;
        [Min(0.1f)] public float movementMultiplier = 1f;
        [Min(0.1f)] public float fireDamageMultiplier = 1f;
        [Range(0f, 1f)] public float poisonResistance;
        
        //custom sprites for every infection state while using the same animator
        public AnimatorOverrideController animationOverride;
        public Color placeHolderTint = Color.white;
        
    }

    public InfectionStage[] stages = CreateDefaultStages();


    private static readonly InfectionStage[] fallbackStages = CreateDefaultStages();


    public InfectionStage GetStage(int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, InfectionCount - 1);
        if (stages == null || safeIndex >= stages.Length || stages[safeIndex] == null)
            return fallbackStages[safeIndex];
        return stages[safeIndex];
    }
    
    public static InfectionStage GetDefaultStage(int index) =>
        fallbackStages[Mathf.Clamp(index, 0, InfectionCount - 1)];

    
    //create all custom infection stages
    public static InfectionStage[] CreateDefaultStages()
    {
        string[] names = { "Healthy", "Exposed", "Rooted", "Overgrown", "Lost", "Consumed" };
        float[] aggro = { 1f, 0.95f, 0.88f, 0.78f, 0.65f, 0.05f };
        float[] poisonResistances = { 0f, 0.05f, 0.12f, 0.22f, 0.35f, 1f };
        float[] health = { 1f, 0.98f, 0.95f, 0.9f, 0.82f, 0.4f };
        float[] vision = { 1f, 0.97f, 0.93f, 0.88f, 0.8f, 0.35f };
        float[] movement = { 1f, 0.98f, 0.96f, 0.93f, 0.88f, 0.4f };
        float[] fireDamage = { 1f, 1.05f, 1.1f, 1.2f, 1.35f, 2f };
        var result = new InfectionStage[InfectionCount];
        
        
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new InfectionStage()
            {
                name = names[i],
                healthMultiplier = health[i],
                visionMultiplier = vision[i],
                aggroMultiplier = aggro[i],
                movementMultiplier = movement[i],
                fireDamageMultiplier = fireDamage[i],
                poisonResistance = poisonResistances[i],
                placeHolderTint = Color.Lerp(Color.white, new Color(0.55f, 0.85f, 0.45f), i / 4f)
                
            };
          
        }        return result;

    }
    
    
}
