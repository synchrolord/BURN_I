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

    private void Start()
    {
        if (pressureTool == null ||
            waterFill == null || waterText == null ||
            fuelFill == null || fuelText == null ||
            modeText == null)
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

        modeText.text =
            $"{pressureTool.CurrentSprayType} | " +
            pressureTool.CurrentNozzleLabel;
    }

    private void RefreshBar(
        Image fill,
        TMP_Text label,
        string resourceName,
        int current,
        int maximum)
    {
        float fraction = 0f;

        if (maximum > 0)
        {
            fraction = (float)current / maximum;
        }

        fill.fillAmount = Mathf.Clamp01(fraction);
        label.text = $"{resourceName}: {current} / {maximum}";
    }
}