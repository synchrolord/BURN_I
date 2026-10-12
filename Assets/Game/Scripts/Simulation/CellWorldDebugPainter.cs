using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Optional test brush. Disable this component for ordinary player controls.
public sealed class CellWorldDebugPainter : MonoBehaviour
{
    
    
    
    
    private enum PaintMode
    {
        Water,
        Fuel,
        Acid,
        Smoke,
        Fire
    }
    [SerializeField] private CellWorld2D cellWorld;
    [SerializeField] private Camera worldCamera;
    [Header("Brush")]
    [SerializeField, Range(0, 32)] private int brushRadiusInCells = 2;
    [FormerlySerializedAs("waterUnitsPerStamp")]
    [SerializeField, Range(1, 32)] private int liquidUnitsPerStamp = 8;
    [SerializeField, Range(1, 32)] private int smokeUnitsPerStamp = 5;
    [SerializeField, Min(0.005f)] private float stampInterval = 0.04f;
    [SerializeField] private bool showHelpText = true;
    private PaintMode paintMode;
    private float stampTimer;

    private void Awake() { if (worldCamera == null) worldCamera = Camera.main; }

    //read brush input and limit how often material gets painted
    private void Update()
    {
        if (cellWorld == null || worldCamera == null || Mouse.current == null || Keyboard.current == null) return;
        HandleKeyboard();
        stampTimer -= Time.unscaledDeltaTime;
        bool paint = Mouse.current.leftButton.isPressed;
        bool erase = Mouse.current.rightButton.isPressed;
        //reset the timer so the next brush press paints immediately
        if (!paint && !erase) { stampTimer = 0f; return; }
        if (stampTimer > 0f) return;
        Vector2 position = GetMouseWorldPosition();
        if (erase) cellWorld.EraseCircle(position, brushRadiusInCells);
        else Paint(position);
        stampTimer = stampInterval;
    }

    //apply the selected material to a circle around the mouse
    private void Paint(Vector2 position)
    {
        switch (paintMode)
        {
            case PaintMode.Water: cellWorld.AddWater(position, brushRadiusInCells, liquidUnitsPerStamp); break;
            case PaintMode.Fuel: cellWorld.AddFuel(position, brushRadiusInCells, liquidUnitsPerStamp); break;
            case PaintMode.Smoke: cellWorld.AddSmoke(position, brushRadiusInCells, smokeUnitsPerStamp); break;
            case PaintMode.Fire: cellWorld.Ignite(position, brushRadiusInCells); break;
            case PaintMode.Acid: cellWorld.AddAcid(position, brushRadiusInCells, liquidUnitsPerStamp); break;
        }
    }

    //switch brush materials and handle simulation debug controls
    private void HandleKeyboard()
    {
        Keyboard keys = Keyboard.current;
        if (keys.digit1Key.wasPressedThisFrame)
            paintMode = PaintMode.Water;

        if (keys.digit2Key.wasPressedThisFrame)
            paintMode = PaintMode.Fuel;

        if (keys.digit3Key.wasPressedThisFrame)
            paintMode = PaintMode.Acid;

        if (keys.digit4Key.wasPressedThisFrame)
            paintMode = PaintMode.Smoke;

        if (keys.digit5Key.wasPressedThisFrame)
            paintMode = PaintMode.Fire;
        
        if (keys.pKey.wasPressedThisFrame) cellWorld.TogglePaused();
        if (keys.nKey.wasPressedThisFrame) cellWorld.StepOnce();
        if (keys.rKey.wasPressedThisFrame) cellWorld.ClearAllMaterials();
        if (keys.bKey.wasPressedThisFrame) cellWorld.RebakeSolidsFromPhysics();
        if (keys.f1Key.wasPressedThisFrame)
            Debug.Log(
                $"Water: {cellWorld.GetTotalWaterUnits()} | " +
                $"Fuel: {cellWorld.GetTotalFuelUnits()} | " +
                $"Acid: {cellWorld.GetTotalAcidUnits()} | " +
                $"Awake chunks: {cellWorld.GetActiveChunkCount()}");    }

    //convert the mouse's screen position to the grid's world plane
    private Vector2 GetMouseWorldPosition()
    {
        Vector2 screen = Mouse.current.position.ReadValue();
        float distance = Mathf.Abs(worldCamera.transform.position.z - cellWorld.transform.position.z);
        return worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, distance));
    }

    //show the current brush mode and available shortcuts
    private void OnGUI()
    {
        if (!showHelpText) return;
        GUI.Label(new Rect(12f, 12f, 700f, 60f), "1 Water | 2 Fuel | 3 Acid | 4 Smoke | 5 Fire | " +
"Left Paint | Right Erase | P Pause | N Step | " +
"R Clear | B Rebake | F1 Stats");
    }
}
