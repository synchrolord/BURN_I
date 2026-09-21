using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class PressureTool2D : MonoBehaviour
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

        [Tooltip("Water amount placed at every path sample. Flame mode does not place liquid fuel by default.")]
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

        probes = new SprayProbe[Mathf.Max(32, maximumProbeCount)];

        for (int i = 0; i < probes.Length; i++)
        {
            probes[i] = new SprayProbe();
        }

        currentWater = Mathf.Clamp(currentWater, 0, maximumWater);
        currentFuel = Mathf.Clamp(currentFuel, 0, maximumFuel);
        currentNozzleIndex = Mathf.Clamp(currentNozzleIndex, 0, 2);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        UpdateActiveProbes(deltaTime);
        UpdateEmission(deltaTime);
    }

    private void UpdateEmission(float deltaTime)
    {
        NozzleSettings nozzle = GetCurrentNozzle();
        bool hasResource = HasEnoughResource(nozzle.resourceCostPerProbe);
        bool usingTool = sprayHeld && hasResource;

        if (playerMotor != null)
        {
            playerMotor.SetUsingTool(usingTool);
        }

        if (!usingTool || cellWorld == null || muzzle == null)
        {
            return;
        }

        emissionBudget += nozzle.probesPerSecond * deltaTime;

        int probesCreatedThisFrame = 0;

        while (emissionBudget >= 1f && probesCreatedThisFrame < 24)
        {
            if (!TrySpendResource(nozzle.resourceCostPerProbe))
            {
                break;
            }

            SpawnProbe(nozzle);
            emissionBudget -= 1f;
            probesCreatedThisFrame++;
        }
    }

    private void SpawnProbe(NozzleSettings nozzle)
    {
        SprayProbe probe = probes[nextProbeSlot];
        nextProbeSlot = (nextProbeSlot + 1) % probes.Length;

        Vector2 forward = muzzle.right;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector2.right;
        }

        forward.Normalize();

        float halfAngle = nozzle.angleDegrees * 0.5f;
        float angleOffset = Random.Range(-halfAngle, halfAngle);
        Vector2 direction = Rotate(forward, angleOffset);

        float speedMultiplier = Random.Range(
            1f - nozzle.speedRandomness,
            1f + nozzle.speedRandomness
        );

        Vector2 inheritedVelocity = playerBody != null
            ? playerBody.linearVelocity * inheritPlayerVelocity
            : Vector2.zero;

        probe.active = true;
        probe.material = currentMaterial;
        probe.position = (Vector2)muzzle.position + direction * cellWorld.CellSize * 0.75f;
        probe.velocity = direction * nozzle.speed * speedMultiplier + inheritedVelocity;
        probe.remainingLife = nozzle.lifetime * Random.Range(0.85f, 1.15f);
        probe.noiseSeed = Random.Range(0f, 1000f);
        probe.radiusInCells = nozzle.radiusInCells;
        probe.unitsPerDeposit = nozzle.unitsPerDeposit;
        probe.ignitionRadiusInCells = nozzle.ignitionRadiusInCells;

        DepositAtPosition(probe, probe.position);
    }

    private void UpdateActiveProbes(float deltaTime)
    {
        if (probes == null || cellWorld == null)
        {
            return;
        }

        for (int i = 0; i < probes.Length; i++)
        {
            SprayProbe probe = probes[i];

            if (!probe.active)
            {
                continue;
            }

            probe.remainingLife -= deltaTime;

            if (probe.remainingLife <= 0f)
            {
                probe.active = false;
                continue;
            }

            Vector2 previousPosition = probe.position;

            if (probe.material == ToolMaterial.Water)
            {
                probe.velocity += Physics2D.gravity * waterGravityScale * deltaTime;
                probe.velocity *= 1f / (1f + waterDrag * deltaTime);
            }
            else
            {
                Vector2 travelDirection = probe.velocity.sqrMagnitude > 0.0001f
                    ? probe.velocity.normalized
                    : Vector2.right;

                Vector2 sideways = new Vector2(-travelDirection.y, travelDirection.x);
                float noise = Mathf.PerlinNoise(
                    probe.noiseSeed,
                    Time.time * flameTurbulenceSpeed
                ) * 2f - 1f;

                probe.velocity += Vector2.up * flameLift * deltaTime;
                probe.velocity += sideways * noise * flameTurbulence * deltaTime;
                probe.velocity *= 1f / (1f + flameDrag * deltaTime);
            }

            Vector2 nextPosition = previousPosition + probe.velocity * deltaTime;
            RaycastHit2D hit = Physics2D.Linecast(
                previousPosition,
                nextPosition,
                obstacleMask
            );

            if (hit.collider != null)
            {
                Vector2 travelDirection = probe.velocity.sqrMagnitude > 0.0001f
                    ? probe.velocity.normalized
                    : Vector2.right;

                nextPosition = hit.point - travelDirection * cellWorld.CellSize * 0.5f;
                DepositAlongSegment(probe, previousPosition, nextPosition);
                probe.position = nextPosition;
                probe.active = false;
                continue;
            }

            DepositAlongSegment(probe, previousPosition, nextPosition);
            probe.position = nextPosition;
        }
    }

    private void DepositAlongSegment(
        SprayProbe probe,
        Vector2 start,
        Vector2 end)
    {
        float distance = Vector2.Distance(start, end);
        float spacing = Mathf.Max(cellWorld.CellSize * 0.75f, 0.03f);
        int steps = Mathf.Clamp(Mathf.CeilToInt(distance / spacing), 1, 8);

        for (int step = 1; step <= steps; step++)
        {
            float amount = step / (float)steps;
            Vector2 position = Vector2.Lerp(start, end, amount);
            DepositAtPosition(probe, position);
        }
    }

    private void DepositAtPosition(SprayProbe probe, Vector2 position)
    {
        float jitterInCells = probe.material == ToolMaterial.Water
            ? waterDepositJitterInCells
            : flameDepositJitterInCells;

        if (jitterInCells > 0f)
        {
            position += Random.insideUnitCircle * cellWorld.CellSize * jitterInCells;
        }

        if (probe.material == ToolMaterial.Water)
        {
            cellWorld.AddWater(
                position,
                probe.radiusInCells,
                probe.unitsPerDeposit
            );

            return;
        }

        // The player's fuel is atomized and burned in the air. We therefore
        // create fire directly instead of dropping a large trail of liquid oil.
        int ignitionRadius = Mathf.Max(
            probe.radiusInCells,
            probe.ignitionRadiusInCells
        );

        if (leaveUnburnedFuelResidue &&
            Random.value < unburnedFuelResidueChance)
        {
            cellWorld.AddFuel(
                position,
                probe.radiusInCells,
                unburnedFuelResidueAmount
            );
        }

        cellWorld.Ignite(position, ignitionRadius, 3);
    }

    private void OnDisable()
    {
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

    // PlayerInput calls this because the action is named "Spray".
    public void OnSpray(InputValue value)
    {
        sprayHeld = value.isPressed;

        if (sprayHeld)
        {
            emissionBudget = Mathf.Max(emissionBudget, 1f);
        }
        else
        {
            emissionBudget = 0f;

            if (playerMotor != null)
            {
                playerMotor.SetUsingTool(false);
            }
        }
    }

    // PlayerInput calls this because the action is named "SwitchMaterial".
    public void OnSwitchMaterial(InputValue value)
    {
        if (!value.isPressed)
        {
            return;
        }

        currentMaterial = currentMaterial == ToolMaterial.Water
            ? ToolMaterial.Flame
            : ToolMaterial.Water;

        Debug.Log($"Pressure tool material: {currentMaterial}");
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

        Debug.Log($"Pressure tool nozzle: {GetCurrentNozzle().label}");
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);

        return new Vector2(
            direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine
        );
    }

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

    private void OnGUI()
    {
        if (!showDebugOverlay)
        {
            return;
        }

        string waterText = unlimitedResourcesForTesting
            ? "infinite"
            : $"{currentWater}/{maximumWater}";

        string fuelText = unlimitedResourcesForTesting
            ? "infinite"
            : $"{currentFuel}/{maximumFuel}";

        GUI.Box(
            new Rect(12f, 80f, 470f, 68f),
            $"PRESSURE TOOL\n" +
            $"Material: {currentMaterial} | Nozzle: {GetCurrentNozzle().label}\n" +
            $"Water: {waterText} | Fuel: {fuelText}"
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (muzzle == null)
        {
            return;
        }

        NozzleSettings nozzle = GetCurrentNozzle();
        Vector2 origin = muzzle.position;
        Vector2 forward = muzzle.right;
        float halfAngle = nozzle.angleDegrees * 0.5f;
        float approximateRange = nozzle.speed * nozzle.lifetime;

        Gizmos.color = currentMaterial == ToolMaterial.Water
            ? Color.cyan
            : new Color(1f, 0.35f, 0.05f);

        Gizmos.DrawLine(
            origin,
            origin + Rotate(forward, -halfAngle) * approximateRange
        );

        Gizmos.DrawLine(
            origin,
            origin + Rotate(forward, halfAngle) * approximateRange
        );

        Gizmos.DrawLine(
            origin,
            origin + forward * approximateRange
        );
    }
}
