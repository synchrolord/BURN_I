using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CellWorldDebugPainter : MonoBehaviour
{
    private enum PaintMode
    {
        Water,
        Fuel,
        Smoke,
        Fire
    }

    [SerializeField]
    private CellWorld2D cellWorld;

    [SerializeField]
    private Camera worldCamera;

    [Header("Brush")]
    [SerializeField, Range(0, 32)]
    private int brushRadiusInCells = 2;

    [SerializeField, Range(1, 32)]
    private int liquidUnitsPerStamp = 8;

    [SerializeField, Range(1, 32)]
    private int smokeUnitsPerStamp = 5;

    [SerializeField, Min(0.005f)]
    private float stampInterval = 0.04f;

    [SerializeField]
    private bool showHelpText = true;

    private PaintMode paintMode = PaintMode.Water;
    private float stampTimer;

    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (cellWorld == null ||
            worldCamera == null ||
            Mouse.current == null ||
            Keyboard.current == null)
        {
            return;
        }

        HandleKeyboard();

        stampTimer -= Time.unscaledDeltaTime;

        bool paint = Mouse.current.leftButton.isPressed;
        bool erase = Mouse.current.rightButton.isPressed;

        if (!paint && !erase)
        {
            stampTimer = 0f;
            return;
        }

        if (stampTimer > 0f)
        {
            return;
        }

        Vector2 worldPosition = GetMouseWorldPosition();

        if (erase)
        {
            cellWorld.EraseCircle(
                worldPosition,
                brushRadiusInCells
            );
        }
        else
        {
            Paint(worldPosition);
        }

        stampTimer = stampInterval;
    }

    private void Paint(Vector2 worldPosition)
    {
        switch (paintMode)
        {
            case PaintMode.Water:
                cellWorld.AddWater(
                    worldPosition,
                    brushRadiusInCells,
                    liquidUnitsPerStamp
                );
                break;

            case PaintMode.Fuel:
                cellWorld.AddFuel(
                    worldPosition,
                    brushRadiusInCells,
                    liquidUnitsPerStamp
                );
                break;

            case PaintMode.Smoke:
                cellWorld.AddSmoke(
                    worldPosition,
                    brushRadiusInCells,
                    smokeUnitsPerStamp
                );
                break;

            case PaintMode.Fire:
                cellWorld.Ignite(
                    worldPosition,
                    brushRadiusInCells,
                    3
                );
                break;
        }
    }

    private void HandleKeyboard()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            paintMode = PaintMode.Water;
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            paintMode = PaintMode.Fuel;
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            paintMode = PaintMode.Smoke;
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            paintMode = PaintMode.Fire;
        }

       // if (Keyboard.current.spaceKey.wasPressedThisFrame)
       // {
       //    cellWorld.TogglePaused();
       // }

        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            cellWorld.StepOnce();
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            cellWorld.ClearAllMaterials();
        }

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            cellWorld.RebakeSolidsFromPhysics();
        }

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            Debug.Log(
                $"Water: {cellWorld.GetTotalWaterUnits()} | " +
                $"Fuel: {cellWorld.GetTotalFuelUnits()} | " +
                $"Awake chunks: {cellWorld.GetActiveChunkCount()}"
            );
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector2 mouseScreenPosition =
            Mouse.current.position.ReadValue();

        float distanceToWorldPlane = Mathf.Abs(
            worldCamera.transform.position.z -
            cellWorld.transform.position.z
        );

        Vector3 screenPoint = new Vector3(
            mouseScreenPosition.x,
            mouseScreenPosition.y,
            distanceToWorldPlane
        );

        Vector3 worldPoint =
            worldCamera.ScreenToWorldPoint(screenPoint);

        return new Vector2(worldPoint.x, worldPoint.y);
    }

    private void OnGUI()
    {
        if (!showHelpText)
        {
            return;
        }

        GUI.Label(
            new Rect(12f, 12f, 700f, 60f),
            $"CELL DEBUG - Mode: {paintMode}\n" +
            "1 Water | 2 Fuel | 3 Smoke | 4 Fire | " +
            "Left Paint | Right Erase | Space Pause | " +
            "N Step | R Clear | B Rebake | F1 Stats"
        );
    }
}
