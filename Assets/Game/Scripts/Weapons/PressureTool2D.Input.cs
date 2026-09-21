using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class PressureTool2D
{
    // PlayerInput calls this because the action is named "Spray".
    public void OnSpray(InputValue value) => SetSpraying(value.isPressed);

    // Public entry points also work with UI buttons or future touch controls.
    public void SetSpraying(bool pressed)
    {
        sprayHeld = pressed;
        emissionBudget = pressed ? Mathf.Max(emissionBudget, 1f) : 0f;
        if (!pressed && playerMotor != null) playerMotor.SetUsingTool(false);
    }

    public void ToggleMaterial()
    {
        currentMaterial = currentMaterial == ToolMaterial.Water
            ? ToolMaterial.Flame : ToolMaterial.Water;
    }

    public void UseWater() => currentMaterial = ToolMaterial.Water;
    public void UseFlame() => currentMaterial = ToolMaterial.Flame;

    // Compatibility for older copies of the input asset.
    public void OnSwitch_matterials(InputValue value) => OnSwitchMaterial(value);

    // PlayerInput calls this because the action is named "SwitchMaterial".
    public void OnSwitchMaterial(InputValue value)
    {
        if (!value.isPressed)
        {
            return;
        }

        ToggleMaterial();
    }

    // Bind this action to Mouse/scroll. Mouse scroll is a Vector2.
    public void OnNozzleScroll(InputValue value)
    {
        float scrollY = value.Get<Vector2>().y;

        if (Mathf.Abs(scrollY) < 0.01f)
        {
            return;
        }

        CycleNozzle(scrollY > 0f ? 1 : -1);
    }

    public void OnNozzleNext(InputValue value)
    {
        if (value.isPressed)
        {
            CycleNozzle(1);
        }
    }

    public void OnNozzlePrevious(InputValue value)
    {
        if (value.isPressed)
        {
            CycleNozzle(-1);
        }
    }

    private void CycleNozzle(int direction)
    {
        currentNozzleIndex += direction;

        if (currentNozzleIndex > 2)
        {
            currentNozzleIndex = 0;
        }
        else if (currentNozzleIndex < 0)
        {
            currentNozzleIndex = 2;
        }


    }
}
