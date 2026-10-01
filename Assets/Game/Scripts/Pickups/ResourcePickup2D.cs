
using UnityEngine;

   
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ResourcePickup2D : MonoBehaviour {
        private enum PickupType
        {
            Water,
            Fuel,
        }
        
        
        [SerializeField] private PickupType pickupType = PickupType.Fuel;
        [SerializeField, Min(1)] private int amount = 50;
        private bool isCollected = false;
        
        
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
        
}
