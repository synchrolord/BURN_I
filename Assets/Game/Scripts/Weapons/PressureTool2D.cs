using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class PressureTool2D : MonoBehaviour
{
    public enum ToolMaterial { Water, Flame }

    [System.Serializable]

    //allow different weapon nozzles
    private class NozzleSettings
    {
        public string label = "Jet";
        [Range(0, 70)] public float angleDegrees = 16;
        [Min(0.1f)] public float speed = 6.5f;
        [Min(0.05f)] public float lifetime = 0.55f;
        [Range(0, 4)] public int radiusInCells;
        [Min(1)] public int unitsPerDeposit = 1;
        [Min(1)] public float probesPerSecond = 26;
        [Min(0)] public int resourceCostPerProbe = 1;
        [Range(0, 3)] public int ignitionRadiusInCells = 1;
    }

    [Header("Starting mode and nozzles")]
    [SerializeField] private ToolMaterial currentMaterial = ToolMaterial.Water;
    [SerializeField, Range(0, 2)] private int currentNozzleIndex = 1;

    //nozzle types
    [SerializeField] private NozzleSettings jet = new NozzleSettings();

    [SerializeField] private NozzleSettings pilot = new NozzleSettings
    { label = "Pilot",
        angleDegrees = 4,
        speed = 3.2f,
        lifetime = 0.28f,
        probesPerSecond = 10,
        ignitionRadiusInCells = 0 };

    [SerializeField] private NozzleSettings wide = new NozzleSettings
    { label = "Wide",
        angleDegrees = 48,
        speed = 5,
        lifetime = 0.4f,
        probesPerSecond = 34,
        radiusInCells = 1,
        resourceCostPerProbe = 2 };

    //weapon shoots "drops" of water or fire
    internal class Drop
    {
        public ToolMaterial material;
        public Vector2 position;
        public Vector2 velocity;
        public float life;
        public float startingLife;
        public int water;
        public int fireRadius;
    }

    //gameObjects required
    [Header("References")]
    [SerializeField] private CellWorld2D cellWorld;
    [SerializeField] private Transform muzzle;
    [SerializeField] private PlayerMotor2D playerMotor;
    [SerializeField] private LayerMask obstacleMask;

    //material settings
    [Header("Water")]
    [SerializeField, Min(1)] private float waterSpeedMultiplier = 4;
    [SerializeField, Min(0.1f)] private float waterLifetimeMultiplier = 1.4f;
    [SerializeField, Min(0.1f)] private float waterEmissionMultiplier = 2;
    [SerializeField, Min(1)] private int waterAmountMultiplier = 4;
    [SerializeField, Min(0)] private float waterGravityScale = 1.4f;

    [Header("Flame")]
    [SerializeField, Min(0)] private float flameLift = 3;
    [SerializeField, Min(0)] private float flameDrag = 1.8f;

    [Header("Appearance and performance")]
    [SerializeField, Range(32, 512)] private int maximumProbeCount = 192;
    [SerializeField, Min(0.02f)] private float waterDropWidth = 0.12f;
    [SerializeField, Min(0.02f)] private float flameWidth = 0.45f;
    [SerializeField] private int spraySortingOffset = 2;
    [SerializeField] private bool showDebugOverlay = true;

    [Header("Resources")]
    [SerializeField] private bool unlimitedResourcesForTesting = true;
    [SerializeField, Min(1)] private int maximumWater = 300;
    [SerializeField, Min(0)] private int currentWater = 300;
    [SerializeField, Min(1)] private int maximumFuel = 300;
    [SerializeField, Min(0)] private int currentFuel = 300;

    // Like Java's ArrayList: this contains only drops that are still flying.
    private readonly List<Drop> drops = new List<Drop>();
    private PressureSprayVisuals drawing;
    private InputAction sprayAction;
    private bool sprayHeld;
    private float launchBudget;

    //getters
    public bool IsSpraying => sprayHeld;
    public int ActiveProbeCount => drops.Count;
    public ToolMaterial CurrentMaterial => currentMaterial;
    public int CurrentWater => currentWater;
    public int MaximumWater => maximumWater;
    public int CurrentFuel => currentFuel;
    public int MaximumFuel => maximumFuel;

    private void Awake()
    {
        if (playerMotor == null) playerMotor = GetComponent<PlayerMotor2D>();
        maximumProbeCount = Mathf.Clamp(maximumProbeCount, 32, 512);
        currentWater = Mathf.Clamp(currentWater, 0, maximumWater);
        currentFuel = Mathf.Clamp(currentFuel, 0, maximumFuel);
        drawing = new PressureSprayVisuals(transform, cellWorld, maximumProbeCount, spraySortingOffset);
    }

    private void Update()
    {
        if (cellWorld == null || !cellWorld.IsInitialized || muzzle == null)
        {
            if (playerMotor != null) playerMotor.SetUsingTool(false);
            return;
        }

        MoveDrops(Time.deltaTime);
        LaunchDrops(Time.deltaTime);
        drawing.Draw(drops, waterDropWidth, flameWidth);
    }

    public void OnSpray(InputValue value)
    {

        InputAction action = GetComponent<PlayerInput>().actions.FindAction("Spray");
        if (sprayAction != action)
        {
            if (sprayAction != null) sprayAction.canceled -= ReleaseSpray;
            sprayAction = action;
            if (sprayAction != null) sprayAction.canceled += ReleaseSpray;
        }
        SetSpraying(value.isPressed);
    }
    private void ReleaseSpray(InputAction.CallbackContext context) => SetSpraying(false);

    public void SetSpraying(bool pressed)
    {
        if (pressed && !sprayHeld)
        {
            launchBudget = 1; // The first shot is immediate.
        }
        sprayHeld = pressed;
        if (!pressed) launchBudget = 0;
        if (!pressed && playerMotor != null) playerMotor.SetUsingTool(false);
    }

    //button material switching
    public void ToggleMaterial() =>
        currentMaterial = currentMaterial == ToolMaterial.Water
        ? ToolMaterial.Flame
        : ToolMaterial.Water;
    public void OnSwitchMaterial(InputValue value)
    { if (value.isPressed) ToggleMaterial(); }
    public void OnSwitch_matterials(InputValue value) => OnSwitchMaterial(value);

    //manual material switching
    public void UseWater() => currentMaterial = ToolMaterial.Water;
    public void UseFlame() => currentMaterial = ToolMaterial.Flame;

    //switch nozzles with mouse wheel
    public void OnNozzleScroll(InputValue value)
    {
        float scroll = value.Get<Vector2>().y;
        if (scroll != 0)
            currentNozzleIndex =
                (currentNozzleIndex + (scroll > 0 ? 1 : 2)) % 3;
    }
    private NozzleSettings CurrentNozzle()
    {
        if (currentNozzleIndex == 0) return pilot;
        if (currentNozzleIndex == 2) return wide;
        return jet;
    }

    private void LaunchDrops(float seconds)
    {
        NozzleSettings nozzle = CurrentNozzle();
        int cost = Mathf.Max(0, nozzle.resourceCostPerProbe);
        bool canSpray = sprayHeld && HasSupply(cost);
        if (playerMotor != null) playerMotor.SetUsingTool(canSpray);
        if (!canSpray) return;

        bool water = currentMaterial == ToolMaterial.Water;
        float rate = nozzle.probesPerSecond * (water ? waterEmissionMultiplier : 1);
        launchBudget = Mathf.Min(launchBudget + rate * seconds, 12);

        while (launchBudget >= 1 && drops.Count < maximumProbeCount && HasSupply(cost))
        {
            // Fire with a random angle within the nozzle's spread.
            float angle = Random.Range(-nozzle.angleDegrees / 2, nozzle.angleDegrees / 2);
            Vector2 direction = Quaternion.Euler(0, 0, angle) * muzzle.right;
            float lifetime = Mathf.Max(0.05f, nozzle.lifetime * (water ? waterLifetimeMultiplier : 1));
            drops.Add(new Drop
            {
                material = currentMaterial,
                position = muzzle.position,
                velocity = direction * nozzle.speed * (water ? waterSpeedMultiplier : 1),
                life = lifetime,
                startingLife = lifetime,
                water = Mathf.Max(1, nozzle.unitsPerDeposit * waterAmountMultiplier),
                fireRadius = Mathf.Max(nozzle.radiusInCells, nozzle.ignitionRadiusInCells)
            });

            if (!unlimitedResourcesForTesting)
            {
                if (water) currentWater -= cost;
                else currentFuel -= cost;
            }
            launchBudget--;
        }
    }

    private bool HasSupply(int cost)
    {
        int amount = currentMaterial == ToolMaterial.Water ? currentWater : currentFuel;
        return unlimitedResourcesForTesting || amount >= cost;
    }

    private void MoveDrops(float seconds)
    {
        // Work backwards so removing a finished drop cannot skip the next one.
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            Drop drop = drops[i];
            float step = Mathf.Min(seconds, drop.life);
            bool water = drop.material == ToolMaterial.Water;
            if (water) drop.velocity += Physics2D.gravity * waterGravityScale * step;
            else
            {
                drop.velocity += Vector2.up * flameLift * step;
                drop.velocity /= 1 + flameDrag * step;
            }

            Vector2 end = drop.position + drop.velocity * step;
            CellWorld2D.SprayPath path = cellWorld.TraceSpray(drop.position, end, obstacleMask, transform);
            drop.position = path.end;
            drop.life -= step;

            if (water) drop.water = cellWorld.ApplyWaterSpray(path, drop.water, drop.life <= 0);
            else cellWorld.ApplyFlameSpray(path, drop.fireRadius);

            if (path.blocked || drop.life <= 0 || (water && drop.water <= 0))
                drops.RemoveAt(i);
        }
    }

    //add Resources to player
    public void AddWaterResource(int amount) => currentWater = Mathf.Clamp(currentWater + Mathf.Max(0, amount), 0, maximumWater);
    public void AddFuelResource(int amount) => currentFuel = Mathf.Clamp(currentFuel + Mathf.Max(0, amount), 0, maximumFuel);
    public void RefillAllResources() { currentWater = maximumWater; currentFuel = maximumFuel; }

    private void OnDisable()
    {
        SetSpraying(false);
        if (sprayAction != null) sprayAction.canceled -= ReleaseSpray;
        sprayAction = null;
        drops.Clear();
        if (drawing != null) drawing.Clear();
    }

    private void OnDestroy()
    {
        if (drawing != null) drawing.Destroy();
    }

    private void OnGUI()
    {
        if (!showDebugOverlay) return;
        string supply = unlimitedResourcesForTesting ? "Infinite supplies" : $"Water: {currentWater}/{maximumWater} | Fuel: {currentFuel}/{maximumFuel}";
        GUI.Box(new Rect(12, 80, 420, 48), $"{currentMaterial} | {CurrentNozzle().label} | Q: switch | Scroll: nozzle\n{supply}");
    }
}
