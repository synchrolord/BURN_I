using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ResourceHUD : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PressureTool2D pressureTool;

    [Header("Water")]
    [SerializeField] private Image waterFill;
    [SerializeField] private TMP_Text waterText;

    [Header("Fuel")]
    [SerializeField] private Image fuelFill;
    [SerializeField] private TMP_Text fuelText;

    [Header("Selected tool")]
    [SerializeField] private TMP_Text modeText;
    

    [Header("Health")]
    
    [SerializeField] private PlayerVitals2D playerVitals;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthText;
    
    [Header("Poison")]
    [SerializeField] private Image poisonFill;
    [SerializeField] private TMP_Text poisonText;
    
    [Header("Animation")]
    [SerializeField] private Animator animator;

    [SerializeField, Min(0f)]
    private float attackAnimationSeconds = 0.4f;

    
    
    
    
    
    
    
    private void Start()
    {
        
        
        if (
            waterFill == null || 
            fuelFill == null || 
            pressureTool == null ||
            
            waterText == null || 
            fuelText == null ||
            modeText == null||
            
            
            playerVitals == null ||
            healthFill == null || 
            healthText == null||
            
            
            poisonFill == null ||
            poisonText == null 
            
            ) 
        {
            Debug.LogError(
                "ResourceHUD: assign all references in the Inspector.",
                this);

            enabled = false;
        }
    }

    private void LateUpdate()
    {
        RefreshBar(
            waterFill,
            waterText,
            "Water",
            pressureTool.CurrentWater,
            pressureTool.MaximumWater);

        RefreshBar(
            fuelFill,
            fuelText,
            "Fuel",
            pressureTool.CurrentFuel,
            pressureTool.MaximumFuel);
        
        
        RefreshBar(
            healthFill,
            healthText,
            "Health",
            playerVitals.CurrentHealth,
            playerVitals.MaxHealth);
        
        
        RefreshBar(
            poisonFill,
            poisonText,
            "Poison",
            playerVitals.CurrentPoison,
            playerVitals.MaxPoison
        );
        
        modeText.text =
            $"{pressureTool.CurrentSprayType} | " +
            pressureTool.CurrentNozzleLabel;
    }
    
    

    private void RefreshBar(
        Image fill,
        TMP_Text label,
        string resourceName,
        float current,
        float maximum)
    {
        float fraction = 0f;

        if (maximum > 0f)
            fraction = current / maximum;

        fill.fillAmount = Mathf.Clamp01(fraction);

        label.text =
            $"{resourceName}: {Mathf.CeilToInt(current)} / " +
            $"{Mathf.CeilToInt(maximum)}";
    }
}