
using UnityEngine;

   
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ResourcePickup2D : MonoBehaviour {
        private enum PickupType
        {
            Water,
            Fuel,
        }
        
        [Header("Fuel Explosion")]
        [SerializeField] private CellWorldRoomManager roomManager;
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField, Range(0, 8)] private int explosionRadius = 3;
        [SerializeField, Range(1, 8)] private int fuelPerCell = 2;
        [SerializeField, Min(0.1f)] private float explosionLife = 0.5f;
        
        
        [SerializeField] private PickupType pickupType = PickupType.Fuel;
        [SerializeField, Min(1)] private int amount = 50;
        private bool isCollected = false;
        
        
        
        private void Awake()
        {
            if (roomManager == null)
            {
                roomManager =
                    FindFirstObjectByType<CellWorldRoomManager>();
            }
        }
        
        
        
        private void Update()
        {
            if (isCollected ||
                pickupType != PickupType.Fuel ||
                roomManager == null)
            {
                return;
            }

            CellWorld2D world = roomManager.CurrentWorld;

            if (world == null ||
                world.GetFireEnergy(transform.position) == 0)
            {
                return;
            }

            Explode(world);
        }
        public void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;
            //capture pressure tool from parent to get fuel and water values adn update them
            PressureTool2D pressuretool = other.GetComponentInParent<PressureTool2D>();
            if (pressuretool == null) return;

            if (!TryRefill(pressuretool))
            {
                return;
            }

            isCollected=true;
            Destroy(gameObject);
        }





        public bool TryRefill(PressureTool2D pressureTool)
        {
            if (amount<=0) return false;

            // fill up tank with appropriate fuel type while checking overfilling
            if (pickupType == PickupType.Water)
            {
                if (pressureTool.CurrentWater>=pressureTool.MaximumWater) return false;
                pressureTool.AddWaterResource(amount);
                
            }
            else
            {
                if (pressureTool.CurrentFuel>=pressureTool.MaximumFuel) return false;
                pressureTool.AddFuelResource(amount);
            }
            return true;
        }
        
        private void Explode(CellWorld2D world)
        {
            isCollected = true;

            Vector2 position = transform.position;

            //add fuel to ignite
            world.AddFuel(
                position,
                explosionRadius,
                fuelPerCell);

            world.Ignite(
                position,
                explosionRadius,
                3);

            if (explosionPrefab != null)
            {
                GameObject explosion = Instantiate(
                    explosionPrefab,
                    position,
                    Quaternion.identity);

                Destroy(explosion, explosionLife);
            }

            Destroy(gameObject);
        }
        
}
