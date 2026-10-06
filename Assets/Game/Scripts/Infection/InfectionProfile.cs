using System;
using UnityEngine;
[CreateAssetMenu(fileName = "Infection Profile", menuName = "Infection Profile")]

public class InfectionProfile : ScriptableObject{

    public const int InfectionCount =5;

    [Serializable]
    
    //create custom infection stage 
    public class InfectionStage
    {
        public string name = "Healthy";
        
        //multipliers on players stats
        [Min(0.1f)] public float healthMultiplier = 1f;
        [Min(0.1f)] public float visionMultiplier = 1f;
        [Min(0.1f)] public float aggroMultiplier = 1f;
        [Min(0.1f)] public float movementMultiplier = 1f;
        [Min(0.1f)] public float fireDamageMultiplier = 1f;
        
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
        string[] names = { "Healthy", "Exposed", "Rooted", "Overgrown", "Lost" };
        var result = new InfectionStage[InfectionCount];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new InfectionStage()
            {
                name = names[i],
                healthMultiplier = 1f - i * 0.05f,
                visionMultiplier = 1f - i * 0.10f,
                aggroMultiplier = 1f - i * 0.15f,
                movementMultiplier = 1f - i * 0.025f,
                fireDamageMultiplier = 1f + i * 0.20f,
                placeHolderTint = Color.Lerp(Color.white, new Color(0.55f, 0.85f, 0.45f), i / 4f)
            };
          
        }        return result;

    }
    
    
}
