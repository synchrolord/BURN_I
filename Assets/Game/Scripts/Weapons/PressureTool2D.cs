using UnityEngine;
using UnityEngine.InputSystem;

// Read in this order: Update -> LaunchDrops -> MoveDrops -> DrawDrops.
// A drop is plain data. Unity draws the whole array with one ParticleSystem.
[RequireComponent(typeof(PlayerInput))]
public sealed class PressureTool2D : MonoBehaviour
{
    public enum ToolMaterial { Water, Flame }

    [System.Serializable]
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

    private class Drop
    {
        public ToolMaterial material;
        public Vector2 position;
        public Vector2 velocity;
        public float life; // Zero means this slot is free to reuse.
        public float startingLife;
        public int water;
        public int fireRadius;
    }

    [Header("References")]
    [SerializeField] private CellWorld2D cellWorld;
    [SerializeField] private Transform muzzle; // Its red/X arrow points out of the barrel.
    [SerializeField] private PlayerMotor2D playerMotor;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Starting mode and nozzles")]
    [SerializeField] private ToolMaterial currentMaterial = ToolMaterial.Water;
    [SerializeField, Range(0, 2)] private int currentNozzleIndex = 1;
    [SerializeField] private NozzleSettings pilot = new NozzleSettings
    { label = "Pilot", angleDegrees = 4, speed = 3.2f, lifetime = 0.28f, probesPerSecond = 10, ignitionRadiusInCells = 0 };
    [SerializeField] private NozzleSettings jet = new NozzleSettings();
    [SerializeField] private NozzleSettings wide = new NozzleSettings
    { label = "Wide", angleDegrees = 48, speed = 5, lifetime = 0.4f, probesPerSecond = 34, radiusInCells = 1, resourceCostPerProbe = 2 };

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

    private Drop[] drops;
    private ParticleSystem drawing;
    private ParticleSystem.Particle[] particles;
    private InputAction sprayAction;
    private bool sprayHeld;
    private float launchBudget;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];

    public bool IsSpraying => sprayHeld;
    public int ActiveProbeCount { get; private set; }
    public ToolMaterial CurrentMaterial => currentMaterial;
    public int CurrentWater => currentWater;
    public int MaximumWater => maximumWater;
    public int CurrentFuel => currentFuel;
    public int MaximumFuel => maximumFuel;

    private void Awake()
    {
        if (playerMotor == null) playerMotor = GetComponent<PlayerMotor2D>();
        drops = new Drop[Mathf.Clamp(maximumProbeCount, 32, 512)];
        for (int i = 0; i < drops.Length; i++) drops[i] = new Drop();
        particles = new ParticleSystem.Particle[drops.Length];
        currentWater = Mathf.Clamp(currentWater, 0, maximumWater);
        currentFuel = Mathf.Clamp(currentFuel, 0, maximumFuel);
        CreateDrawing();
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
        DrawDrops();
    }

    // PlayerInput sends messages named after actions in inputActions.inputactions.
    public void OnSpray(InputValue value)
    {
        // A canceled subscription also handles Button actions that omit a release message.
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
        if (pressed && !sprayHeld) launchBudget = 1; // The first shot is immediate.
        sprayHeld = pressed;
        if (!pressed) launchBudget = 0;
        if (!pressed && playerMotor != null) playerMotor.SetUsingTool(false);
    }
    public void ToggleMaterial() => currentMaterial = currentMaterial == ToolMaterial.Water ? ToolMaterial.Flame : ToolMaterial.Water;
    public void UseWater() => currentMaterial = ToolMaterial.Water;
    public void UseFlame() => currentMaterial = ToolMaterial.Flame;
    public void OnSwitchMaterial(InputValue value) { if (value.isPressed) ToggleMaterial(); }
    public void OnSwitch_matterials(InputValue value) => OnSwitchMaterial(value);
    public void OnNozzleScroll(InputValue value)
    {
        float scroll = value.Get<Vector2>().y;
        if (scroll != 0) currentNozzleIndex = (currentNozzleIndex + (scroll > 0 ? 1 : 2)) % 3;
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
        int amount = currentMaterial == ToolMaterial.Water ? currentWater : currentFuel;
        bool canSpray = sprayHeld && (unlimitedResourcesForTesting || amount >= nozzle.resourceCostPerProbe);
        if (playerMotor != null) playerMotor.SetUsingTool(canSpray);
        if (!canSpray) return;

        bool water = currentMaterial == ToolMaterial.Water;
        float rate = nozzle.probesPerSecond * (water ? waterEmissionMultiplier : 1);
        launchBudget = Mathf.Min(launchBudget + rate * seconds, 12);
        foreach (Drop drop in drops)
        {
            if (launchBudget < 1) break;
            if (drop.life > 0) continue;
            amount = water ? currentWater : currentFuel;
            if (!unlimitedResourcesForTesting && amount < nozzle.resourceCostPerProbe) break;
            if (!unlimitedResourcesForTesting)
            {
                if (water) currentWater -= nozzle.resourceCostPerProbe;
                else currentFuel -= nozzle.resourceCostPerProbe;
            }
            float angle = Random.Range(-nozzle.angleDegrees / 2, nozzle.angleDegrees / 2);
            Vector2 direction = Quaternion.Euler(0, 0, angle) * muzzle.right;
            drop.material = currentMaterial;
            drop.position = muzzle.position;
            drop.velocity = direction * nozzle.speed * (water ? waterSpeedMultiplier : 1);
            drop.life = Mathf.Max(0.05f, nozzle.lifetime * (water ? waterLifetimeMultiplier : 1));
            drop.startingLife = drop.life;
            drop.water = Mathf.Max(1, nozzle.unitsPerDeposit * waterAmountMultiplier);
            drop.fireRadius = Mathf.Max(nozzle.radiusInCells, nozzle.ignitionRadiusInCells);
            launchBudget--;
        }
    }

    private void MoveDrops(float seconds)
    {
        foreach (Drop drop in drops)
        {
            if (drop.life <= 0) continue;
            float step = Mathf.Min(seconds, drop.life);
            bool water = drop.material == ToolMaterial.Water;
            if (water) drop.velocity += Physics2D.gravity * waterGravityScale * step;
            else
            {
                drop.velocity += Vector2.up * flameLift * step;
                drop.velocity /= 1 + flameDrag * step;
            }
            Vector2 start = drop.position;
            Vector2 end = start + drop.velocity * step;
            bool hit = FindWall(start, end, out Vector2 contact, out Vector2 normal);
            if (hit) end = contact + normal * cellWorld.CellSize * 0.55f;
            PaintTrail(drop, start, end);
            if (hit)
            {
                Vector2 insideSurface = contact - normal * cellWorld.CellSize * 0.5f;
                if (water) drop.water -= cellWorld.ApplyPlantWaterImpact(insideSurface, drop.water);
                else cellWorld.Ignite(insideSurface, drop.fireRadius);
            }
            drop.life -= step;
            drop.position = end;
            if (hit || drop.life <= 0 || (water && drop.water <= 0))
            {
                if (water) cellWorld.ApplyWaterImpact(end, drop.water);
                drop.life = 0;
            }
        }
    }

    private bool FindWall(Vector2 start, Vector2 end, out Vector2 contact, out Vector2 normal)
    {
        // Check the whole travelled segment: fast water must not skip thin walls.
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleMask);
        filter.useTriggers = false;
        int count = Physics2D.Linecast(start, end, filter, hits);
        float nearest = float.PositiveInfinity;
        contact = end;
        normal = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
            nearest = hit.distance;
            contact = hit.point;
            normal = hit.normal;
        }
        return nearest < float.PositiveInfinity;
    }

    private void PaintTrail(Drop drop, Vector2 start, Vector2 end)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, end) / (cellWorld.CellSize * 0.5f)));
        for (int i = 1; i <= steps; i++)
        {
            Vector2 point = Vector2.Lerp(start, end, i / (float)steps);
            if (drop.material == ToolMaterial.Flame) cellWorld.Ignite(point, drop.fireRadius);
            else if (drop.water > 0 && cellWorld.GetFireEnergy(point) > 0)
                drop.water -= cellWorld.ApplyWaterImpact(point, drop.water);
        }
    }

    public void AddWaterResource(int amount) => currentWater = Mathf.Clamp(currentWater + Mathf.Max(0, amount), 0, maximumWater);
    public void AddFuelResource(int amount) => currentFuel = Mathf.Clamp(currentFuel + Mathf.Max(0, amount), 0, maximumFuel);
    public void RefillAllResources() { currentWater = maximumWater; currentFuel = maximumFuel; }

    private void CreateDrawing()
    {
        GameObject visual = new GameObject("Pressure spray (runtime)");
        visual.transform.SetParent(transform, false);
        drawing = visual.AddComponent<ParticleSystem>();
        drawing.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = drawing.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = drops.Length;
        var emission = drawing.emission;
        emission.enabled = false;
        ParticleSystemRenderer renderer = drawing.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Resources.Load<Material>("Effects/PressureSpray");
        if (renderer.sharedMaterial == null) Debug.LogWarning("Missing Resources/Effects/PressureSpray material.", this);
        if (cellWorld != null)
        {
            SpriteRenderer worldRenderer = cellWorld.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = worldRenderer.sortingLayerID;
            renderer.sortingOrder = worldRenderer.sortingOrder + spraySortingOffset;
        }
        drawing.Pause(); // We move the drops ourselves; the ParticleSystem only draws them.
    }

    private void DrawDrops()
    {
        ActiveProbeCount = 0;
        foreach (Drop drop in drops)
        {
            if (drop.life <= 0) continue;
            bool water = drop.material == ToolMaterial.Water;
            float age = 1 - drop.life / drop.startingLife;
            float width = water ? waterDropWidth : flameWidth * (0.65f + age);
            Color color = water ? new Color(0.2f, 0.72f, 1, 0.9f)
                : Color.Lerp(new Color(1, 0.9f, 0.22f), new Color(1, 0.18f, 0.025f), age);
            color.a *= 1 - age;
            particles[ActiveProbeCount++] = new ParticleSystem.Particle
            {
                position = drop.position,
                startSize3D = new Vector3(width, water ? width * 2 : width * 2.3f, width),
                rotation = 90 - Mathf.Atan2(drop.velocity.y, drop.velocity.x) * Mathf.Rad2Deg,
                startColor = color,
                startLifetime = 1,
                remainingLifetime = 1
            };
        }
        drawing.SetParticles(particles, ActiveProbeCount);
    }

    private void OnDisable()
    {
        SetSpraying(false);
        if (sprayAction != null) sprayAction.canceled -= ReleaseSpray;
        sprayAction = null;
        if (drops != null) foreach (Drop drop in drops) drop.life = 0;
        if (drawing != null) drawing.Clear();
        ActiveProbeCount = 0;
    }

    private void OnGUI()
    {
        if (!showDebugOverlay) return;
        string supply = unlimitedResourcesForTesting ? "Infinite supplies" : $"Water: {currentWater}/{maximumWater} | Fuel: {currentFuel}/{maximumFuel}";
        GUI.Box(new Rect(12, 80, 420, 48), $"{currentMaterial} | {CurrentNozzle().label} | Q: switch | Scroll: nozzle\n{supply}");
    }
}
