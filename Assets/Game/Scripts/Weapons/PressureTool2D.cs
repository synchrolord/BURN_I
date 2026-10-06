using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public sealed class PressureTool2D : MonoBehaviour
{
    public enum SprayType { Water, Flame }

    [System.Serializable]
    private class NozzleSettings
    {
        public string label = "Jet";
        [Range(0, 70)] public float spreadAngleDegrees = 16;
        [Min(0.1f)] public float shotSpeed = 6.5f;
        [Min(0.05f)] public float shotLifetimeSeconds = 0.55f;
        [Min(1)] public int waterUnitsPerShot = 1;
        [Min(1)] public float shotsPerSecond = 26;
        [Min(0)] public int supplyCostPerShot = 1;
        [Range(0, 4)] public int ignitionRadiusCells = 1;
        
    }

    
    
    [Header("Starting mode and nozzles")]
    [SerializeField] private SprayType currentSprayType = SprayType.Water;
    [SerializeField, Range(0, 2)] private int nozzleMode = 1;

    // Nozzle settings.
    [SerializeField] private NozzleSettings jet = new NozzleSettings();
    [SerializeField] private NozzleSettings pilot = new NozzleSettings
    { label = "Pilot",
        spreadAngleDegrees = 4,
        shotSpeed = 3.2f,
        shotLifetimeSeconds = 0.28f,
        shotsPerSecond = 10,
        ignitionRadiusCells = 0 };
    [SerializeField] private NozzleSettings wide = new NozzleSettings
    { label = "Wide",
        spreadAngleDegrees = 48,
        shotSpeed = 5,
        shotLifetimeSeconds = 0.4f,
        shotsPerSecond = 34,
        supplyCostPerShot = 2 };

    
    // A shot is one moving portion of water or flame. A cell is one square of the world grid.
    internal class SprayShot
    {
        public SprayType sprayType;
        public Vector2 position;
        public Vector2 velocity;
        public float remainingLifetimeSeconds;
        public float initialLifetimeSeconds;
        public int remainingWaterUnits;
        public int ignitionRadiusCells;
    }

    
    [Header("References")]
    [SerializeField] private CellWorld2D cellWorld;
    [SerializeField] private Transform muzzle;
    [SerializeField] private PlayerMotor2D playerMotor;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private PlayerVitals2D playerVitals;
    // Water and flame tuning.
    [Header("Water")]
    [SerializeField, Min(1)] private float waterSpeedMultiplier = 4;
    [SerializeField, Min(0.1f)] private float waterLifetimeMultiplier = 1.4f;
    [SerializeField, Min(0.1f)] private float waterShotRateMultiplier = 2;
    [SerializeField, Min(1)] private int waterUnitsMultiplier = 4;
    [SerializeField, Min(0)] private float waterGravityScale = 1.4f;

    [Header("Flame")]
    [SerializeField, Min(0)] private float flameLift = 3;
    [SerializeField, Min(0)] private float flameDrag = 1.8f;

    [Header("Appearance and performance")]
    [SerializeField, Range(32, 512)] private int maximumShotCount = 192;
    [SerializeField, Min(0.02f)] private float waterShotWidth = 0.12f;
    [SerializeField, Min(0.02f)] private float flameShotWidth = 0.45f;
    [SerializeField] private int spraySortingOffset = 2;
    [SerializeField] private bool showDebugOverlay = true;

    [Header("Resources")]
    [SerializeField] private bool unlimitedResourcesForTesting = true;
    [SerializeField, Min(1)] private int maximumWater = 300;
    [SerializeField, Min(0)] private int currentWater = 300;
    [SerializeField, Min(1)] private int maximumFuel = 300;
    [SerializeField, Min(0)] private int currentFuel = 300;
    
    public string CurrentNozzleLabel => GetCurrentNozzleSettings().label;

    private readonly List<SprayShot> activeShots = new List<SprayShot>();
    private Collider2D playerCollider;
    private PressureSprayVisuals sprayVisuals;
    private InputAction sprayAction;
    private bool isSprayHeld;
    private float shotsToLaunch;

    //readable variables for other classes
    public bool IsSpraying => isSprayHeld;
    public int ActiveShotCount => activeShots.Count;
    public SprayType CurrentSprayType => currentSprayType;
    public int CurrentWater => currentWater;
    public int MaximumWater => maximumWater;
    public int CurrentFuel => currentFuel;
    public int MaximumFuel => maximumFuel;

    private void Awake()
    {
        
        
        if (playerVitals == null)
            playerVitals = GetComponent<PlayerVitals2D>();
        //find player's components
        if (playerMotor == null) playerMotor = GetComponent<PlayerMotor2D>();
        playerCollider = GetComponent<Collider2D>();
        
        //initiate Tool variables 
        maximumShotCount = Mathf.Clamp(maximumShotCount, 32, 512);
        currentWater = Mathf.Clamp(currentWater, 0, maximumWater);
        currentFuel = Mathf.Clamp(currentFuel, 0, maximumFuel);
        sprayVisuals = new PressureSprayVisuals(transform, cellWorld, maximumShotCount, spraySortingOffset);
    }

    
    private void Update()
    {
        
        if (cellWorld == null || !cellWorld.IsInitialized || muzzle == null)
        {
            if (playerMotor != null) playerMotor.SetUsingTool(false);
            return;
        }
        
        
        // Cancel held spray input immediately when the player dies.
        if (playerVitals != null && playerVitals.IsDead)
            SetSpraying(false);
        

        //update current shots/create new shots
        MoveShots(Time.deltaTime);
        LaunchShots(Time.deltaTime);
        sprayVisuals.Draw(activeShots, waterShotWidth, flameShotWidth);
    }

    //pressing left mouse button
    public void OnSpray(InputValue value)
    {

        InputAction action = GetComponent<PlayerInput>().actions.FindAction("Spray");
        if (sprayAction != action)
        {
             
        }
        SetSpraying(value.isPressed);
    }
    private void ReleaseSpray(InputAction.CallbackContext context) => SetSpraying(false);
    
    
    //start of a spray
    public void SetSpraying(bool pressed)
    {
        
        
        if (playerVitals != null && playerVitals.IsDead)
            pressed = false;
        
        
        if (pressed && !isSprayHeld)//new spray
        {
            shotsToLaunch = 1; // The first shot is immediate.
        }
        isSprayHeld = pressed;
        if (!pressed) shotsToLaunch = 0;
        if (!pressed && playerMotor != null)
        {
            playerMotor.SetUsingTool(false);//change payer state
        }
           
    }
    
    public void ToggleSprayType() =>
        currentSprayType = currentSprayType == SprayType.Water
        ? SprayType.Flame
        : SprayType.Water;
    
    
    
    //pressing "Q"
    public void OnSwitchMaterial(InputValue value)
    { if (value.isPressed) ToggleSprayType(); }
    
    

    // switch materials funcs for CellWorld2D
    public void UseWater() => currentSprayType = SprayType.Water;
    public void UseFlame() => currentSprayType = SprayType.Flame;

    
    
    // Switch nozzles with the mouse wheel.
    public void OnNozzleScroll(InputValue value)
    {
        //increment between nozzles wit mouse wheel.
        float scroll = value.Get<Vector2>().y;
        if (scroll != 0)
            nozzleMode =
                (nozzleMode + (scroll > 0 ? 1 : 2)) % 3;
    }
    private NozzleSettings GetCurrentNozzleSettings()
    {
        if (nozzleMode == 0) return pilot;
        if (nozzleMode == 2) return wide;
        return jet;
    }

    
    
    private void LaunchShots(float elapsedSeconds)
    {
        //initialize settings for this shot
        NozzleSettings nozzleSettings = GetCurrentNozzleSettings();
        int supplyCostPerShot = Mathf.Max(0, nozzleSettings.supplyCostPerShot);
        
        //check if player is allowed to shoot
        bool canSpray = isSprayHeld && HasSupply(supplyCostPerShot);
        if (playerMotor != null) playerMotor.SetUsingTool(canSpray);
        if (!canSpray) return;

        //check current matterial type and change setting to water if needed
        bool isWater = currentSprayType == SprayType.Water;
        float shotsPerSecond = nozzleSettings.shotsPerSecond * (isWater ? waterShotRateMultiplier : 1);
        shotsToLaunch = Mathf.Min(shotsToLaunch + shotsPerSecond * elapsedSeconds, 12);

        
      
        Vector2 shotStartPosition = muzzle.position;
        
        //detect if Tool is inside wall
        if (playerCollider != null)
            
            //vector returned stops at the wall to prevent shooting inside wall.
            shotStartPosition = cellWorld.TraceShotPath(playerCollider.bounds.center,
                muzzle.position, obstacleMask, transform).endPosition;

        
        //start shooting if shot queue has fuel available
        while (shotsToLaunch >= 1 && activeShots.Count < maximumShotCount && HasSupply(supplyCostPerShot))
        {
            // choose random angle within chosen range, and make it relative to muzzle not world
            float angleDegrees = Random.Range(-nozzleSettings.spreadAngleDegrees / 2, nozzleSettings.spreadAngleDegrees / 2);
            Vector2 shotDirection = Quaternion.Euler(0, 0, angleDegrees) * muzzle.right;
            float shotLifetimeSeconds = Mathf.Max(0.05f, nozzleSettings.shotLifetimeSeconds * (isWater ? waterLifetimeMultiplier : 1));
            
            
            
            //create new single SprayShot inside activeShots list.
            activeShots.Add(new SprayShot
            {
                sprayType = currentSprayType,
                position = shotStartPosition,
                velocity = shotDirection * nozzleSettings.shotSpeed * (isWater ? waterSpeedMultiplier : 1),
                remainingLifetimeSeconds = shotLifetimeSeconds,
                initialLifetimeSeconds = shotLifetimeSeconds,
                remainingWaterUnits = Mathf.Max(1, nozzleSettings.waterUnitsPerShot * waterUnitsMultiplier),
                ignitionRadiusCells = nozzleSettings.ignitionRadiusCells
            });

            
            
            //subtract fuel from player if not in debugMode and shorten shot queue.
            if (!unlimitedResourcesForTesting)
            {
                if (isWater) currentWater -= supplyCostPerShot;
                else currentFuel -= supplyCostPerShot;
            }
            shotsToLaunch--;
        }
    }

    
    private bool HasSupply(int supplyCostPerShot)
    {
        //Prevent player from shooting if they dont have enough fuel for next shot
        int availableSupply = currentSprayType == SprayType.Water ? currentWater : currentFuel;
        return unlimitedResourcesForTesting || availableSupply >= supplyCostPerShot;
    }

    
    private void MoveShots(float elapsedSeconds)
    {
        // Update evey shot
        for (int i = activeShots.Count - 1; i >= 0; i--)
        {
            SprayShot shot = activeShots[i];
            float stepSeconds = Mathf.Min(elapsedSeconds, shot.remainingLifetimeSeconds);
            bool isWater = shot.sprayType == SprayType.Water;
            if (isWater) shot.velocity += Physics2D.gravity * (waterGravityScale * stepSeconds);
            else
            {
                shot.velocity += Vector2.up * (flameLift * stepSeconds);
                shot.velocity /= 1 + flameDrag * stepSeconds;
            }

            
            //ensure shot is confined within cellWorld
            Vector2 intendedEndPosition = shot.position + (shot.velocity * stepSeconds);
            CellWorld2D.ShotPath path = cellWorld.TraceShotPath(shot.position, intendedEndPosition, obstacleMask, transform);
            shot.position = path.endPosition;
            shot.remainingLifetimeSeconds -= stepSeconds;
            
            

            if (isWater)
            {
                shot.remainingWaterUnits = cellWorld.ApplyWaterShot(
                    path,
                    shot.remainingWaterUnits,
                    shot.remainingLifetimeSeconds <= 0);
            }
            
            else cellWorld.ApplyFlameShot(path, shot.ignitionRadiusCells);

            //kill shot
            if (path.hitObstacle || shot.remainingLifetimeSeconds <= 0 || (isWater && shot.remainingWaterUnits <= 0))
                activeShots.RemoveAt(i);
        }
    }

    
    
    // Refill the player's supplies.
    public void AddWaterResource(int amount) => currentWater = Mathf.Clamp(currentWater + Mathf.Max(0, amount), 0, maximumWater);
    public void AddFuelResource(int amount) => currentFuel = Mathf.Clamp(currentFuel + Mathf.Max(0, amount), 0, maximumFuel);
    public void RefillAllResources() { currentWater = maximumWater; currentFuel = maximumFuel; }


    private void OnDisable()
    {
        SetSpraying(false);
        if (sprayAction != null) sprayAction.canceled -= ReleaseSpray;
        sprayAction = null;
        activeShots.Clear();
        if (sprayVisuals != null) sprayVisuals.Clear();
    }

    private void OnDestroy()
    {
        if (sprayVisuals != null) sprayVisuals.Destroy();
    }

    private void OnGUI()
    {
        if (!showDebugOverlay) return;
        string supply = unlimitedResourcesForTesting ? "Infinite supplies" : $"Water: {currentWater}/{maximumWater} | Fuel: {currentFuel}/{maximumFuel}";
        GUI.Box(new Rect(12, 80, 420, 48), $"{currentSprayType} | {GetCurrentNozzleSettings().label} | Q: switch | Scroll: nozzle\n{supply}");
    }
 
    
    
    public void SetWorld(CellWorld2D world)
    {
        //nothing to change if we are still in the same room
        if (cellWorld == world) return;

        cellWorld = world;

        //remove flying shots belonging to the previous room
        activeShots.Clear();

        if (sprayVisuals != null)
        {
            sprayVisuals.Clear();
            sprayVisuals.SetWorld(world, spraySortingOffset);
        }

        //continue immediately if the player is holding spray
        shotsToLaunch = isSprayHeld ? 1f : 0f;

        if (playerMotor != null)
            playerMotor.SetUsingTool(false);
    }
    
    
    
    
}
