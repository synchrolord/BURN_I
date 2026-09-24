using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed partial class PressureTool2D : MonoBehaviour
{
    public enum ToolMaterial
    {
        Water,
        Flame
    }

    [System.Serializable]
    private sealed class NozzleSettings
    {
        [Tooltip("Name shown in the temporary debug display.")]
        public string label = "Jet";

        [Tooltip("Width of the emitted cone in degrees.")]
        [Range(0f, 70f)]
        public float angleDegrees = 16f;

        [Tooltip("Initial speed of each airborne spray probe in world units per second.")]
        [Min(0.1f)]
        public float speed = 6.5f;

        [Tooltip("How long each airborne probe remains alive. Approximate range is speed multiplied by lifetime.")]
        [Min(0.05f)]
        public float lifetime = 0.55f;

        [Tooltip("Random variation applied to speed. 0.2 means roughly plus or minus 20 percent.")]
        [Range(0f, 0.75f)]
        public float speedRandomness = 0.25f;

        [Tooltip("CellWorld brush radius used along the moving probe path.")]
        [Range(0, 4)]
        public int radiusInCells;

        [Tooltip("Base water payload per probe, multiplied by Water Amount Multiplier. Flame mode does not place liquid fuel by default.")]
        [Range(1, 8)]
        public int unitsPerDeposit = 1;

        [Tooltip("How many airborne probes are launched each second while spraying.")]
        [Min(1f)]
        public float probesPerSecond = 26f;

        [Tooltip("Water or fuel resource removed for each launched probe.")]
        [Min(0)]
        public int resourceCostPerProbe = 1;

        [Tooltip("Extra ignition radius used by flame mode.")]
        [Range(0, 3)]
        public int ignitionRadiusInCells = 1;
    }

    // This is not a GameObject. It is one small piece of data stored in a fixed array.
    // Old probes keep moving after the player turns, which creates the curved plume.
    private sealed class SprayProbe
    {
        public bool active;
        public ToolMaterial material;
        public Vector2 position;
        public Vector2 velocity;
        public float remainingLife;
        public float initialLife;
        public int remainingWater;
        public float noiseSeed;
        public int radiusInCells;
        public int unitsPerDeposit;
        public int ignitionRadiusInCells;
    }

    [Header("References")]
    [SerializeField]
    private CellWorld2D cellWorld;

    [Tooltip("Empty child positioned at the end of the barrel. Its local positive X axis must point out of the tool.")]
    [SerializeField]
    private Transform muzzle;

    [SerializeField]
    private PlayerMotor2D playerMotor;

    [Tooltip("Optional. Used so spray probes stop at walls instead of being deposited through them.")]
    [SerializeField]
    private LayerMask obstacleMask;

    [Header("Starting State")]
    [SerializeField]
    private ToolMaterial currentMaterial = ToolMaterial.Water;

    [Tooltip("0 = Pilot, 1 = Jet, 2 = Wide.")]
    [SerializeField, Range(0, 2)]
    private int currentNozzleIndex = 1;

    [Header("Probe Pool")]
    [Tooltip("Maximum number of airborne probes that can exist at once. This is a fixed pool and creates no objects while playing.")]
    [SerializeField, Range(32, 512)]
    private int maximumProbeCount = 192;

    [Tooltip("How much of the player's current Rigidbody2D velocity is inherited by newly emitted material.")]
    [SerializeField, Range(0f, 1f)]
    private float inheritPlayerVelocity = 0.35f;

    [Header("Water Jet")]
    [Tooltip("Multiplier applied to each nozzle's speed. Jet defaults to 26 world units/second.")]
    [SerializeField, Min(1f)] private float waterSpeedMultiplier = 4f;
    [SerializeField, Min(0.1f)] private float waterLifetimeMultiplier = 1.4f;
    [SerializeField, Min(0.1f)] private float waterEmissionMultiplier = 2f;
    [Tooltip("Water carried by each airborne drop. Deposited once on impact or expiry.")]
    [SerializeField, Min(1)] private int waterAmountMultiplier = 4;

    [Header("Spray Appearance")]
    [SerializeField, Min(0.02f)] private float waterDropWidth = 0.12f;
    [SerializeField, Min(0.02f)] private float flameWidth = 0.45f;
    [Tooltip("Order relative to the CellWorld SpriteRenderer. Adjust for your scenery.")]
    [SerializeField] private int spraySortingOffset = 2;

    [Header("Water Motion")]
    [SerializeField, Range(0f, 4f)]
    private float waterGravityScale = 1.4f;

    [SerializeField, Range(0f, 8f)]
    private float waterDrag = 0.25f;

    [SerializeField, Range(0f, 2f)]
    private float waterDepositJitterInCells = 0.10f;

    [Header("Flame Plume Motion")]
    [Tooltip("Upward acceleration applied to burning mist.")]
    [SerializeField, Range(0f, 20f)]
    private float flameLift = 3f;

    [Tooltip("How quickly the airborne flame slows down and expands into a cloud.")]
    [SerializeField, Range(0f, 10f)]
    private float flameDrag = 1.8f;

    [Tooltip("Sideways acceleration that makes flame paths wave instead of remaining straight.")]
    [SerializeField, Range(0f, 30f)]
    private float flameTurbulence = 12f;

    [SerializeField, Range(0.1f, 10f)]
    private float flameTurbulenceSpeed = 2.6f;

    [Tooltip("Random offset applied while depositing fire into CellWorld. Increase this to make a thicker, less line-like plume.")]
    [SerializeField, Range(0f, 2f)]
    private float flameDepositJitterInCells = 0.75f;

    [Tooltip("Normally off. The player flame is treated as atomized burning mist, so it does not leave large unlit oil puddles.")]
    [SerializeField]
    private bool leaveUnburnedFuelResidue;

    [SerializeField, Range(0f, 1f)]
    private float unburnedFuelResidueChance = 0.05f;

    [SerializeField, Range(1, 4)]
    private int unburnedFuelResidueAmount = 1;

    [Header("Resources")]
    [SerializeField]
    private bool unlimitedResourcesForTesting = true;

    [SerializeField, Min(1)]
    private int maximumWater = 300;

    [SerializeField, Min(0)]
    private int currentWater = 300;

    [SerializeField, Min(1)]
    private int maximumFuel = 300;

    [SerializeField, Min(0)]
    private int currentFuel = 300;

    [Header("Pilot Nozzle")]
    [SerializeField]
    private NozzleSettings pilot = new NozzleSettings
    {
        label = "Pilot",
        angleDegrees = 4f,
        speed = 3.2f,
        lifetime = 0.28f,
        speedRandomness = 0.10f,
        radiusInCells = 0,
        unitsPerDeposit = 1,
        probesPerSecond = 10f,
        resourceCostPerProbe = 1,
        ignitionRadiusInCells = 0
    };

    [Header("Jet Nozzle")]
    [SerializeField]
    private NozzleSettings jet = new NozzleSettings
    {
        label = "Jet",
        angleDegrees = 16f,
        speed = 6.5f,
        lifetime = 0.55f,
        speedRandomness = 0.25f,
        radiusInCells = 0,
        unitsPerDeposit = 1,
        probesPerSecond = 26f,
        resourceCostPerProbe = 1,
        ignitionRadiusInCells = 1
    };

    [Header("Wide Nozzle")]
    [SerializeField]
    private NozzleSettings wide = new NozzleSettings
    {
        label = "Wide",
        angleDegrees = 48f,
        speed = 5f,
        lifetime = 0.40f,
        speedRandomness = 0.30f,
        radiusInCells = 1,
        unitsPerDeposit = 1,
        probesPerSecond = 34f,
        resourceCostPerProbe = 2,
        ignitionRadiusInCells = 1
    };

    [Header("Temporary Debug Display")]
    [SerializeField]
    private bool showDebugOverlay = true;

    private Rigidbody2D playerBody;
    private SprayProbe[] probes;
    private int nextProbeSlot;
    private bool sprayHeld;
    private float emissionBudget;

    public bool IsSpraying => sprayHeld;
    public int ActiveProbeCount { get; private set; }

    public ToolMaterial CurrentMaterial => currentMaterial;
    public int CurrentWater => currentWater;
    public int MaximumWater => maximumWater;
    public int CurrentFuel => currentFuel;
    public int MaximumFuel => maximumFuel;

    private void Awake()
    {
        if (playerMotor == null)
        {
            playerMotor = GetComponent<PlayerMotor2D>();
        }

        playerBody = GetComponent<Rigidbody2D>();

        InitializeProbePool();
        InitializeSprayVisuals();

        currentWater = Mathf.Clamp(currentWater, 0, maximumWater);
        currentFuel = Mathf.Clamp(currentFuel, 0, maximumFuel);
        currentNozzleIndex = Mathf.Clamp(currentNozzleIndex, 0, 2);
    }

    private void OnEnable()
    {
        // Unity can reload scripts while playing without calling Awake again.
        // Rebuild transient probe data and its mesh together after that reload.
        if (probes != null) return;
        InitializeProbePool();
        DisposeSprayVisuals();
        InitializeSprayVisuals();
    }

    private void InitializeProbePool()
    {
        probes = new SprayProbe[Mathf.Max(32, maximumProbeCount)];
        for (int i = 0; i < probes.Length; i++) probes[i] = new SprayProbe();
        sprayHeld = false;
        emissionBudget = 0f;
        ActiveProbeCount = 0;
    }

    private void Update()
    {
        // World.Start initializes its arrays before the first simulation update.
        if (cellWorld == null || !cellWorld.IsReady || muzzle == null)
        {
            if (playerMotor != null) playerMotor.SetUsingTool(false);
            return;
        }

        float deltaTime = Time.deltaTime;
        UpdateActiveProbes(deltaTime);
        UpdateEmission(deltaTime);
        UpdateSprayVisuals();
    }

    private void OnDisable()
    {
        ClearSprayVisuals();
        sprayHeld = false;
        emissionBudget = 0f;

        if (probes != null)
        {
            for (int i = 0; i < probes.Length; i++)
            {
                probes[i].active = false;
            }
        }

        if (playerMotor != null)
        {
            playerMotor.SetUsingTool(false);
        }
    }

    private void OnDestroy()
    {
        DisposeSprayVisuals();
    }
}
