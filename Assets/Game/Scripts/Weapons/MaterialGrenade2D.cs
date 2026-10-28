using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class MaterialGrenade2D : MonoBehaviour
{
    public enum GrenadeMaterial
    {
        Water,
        Fuel,
        Acid,
        FireBomb
    }

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float fuseSeconds = 1.5f;

    [Header("Material burst")]
    [SerializeField, Range(0, 12)] private int minimumRadius = 2;
    [SerializeField, Range(0, 12)] private int maximumRadius = 5;
    [SerializeField, Range(1, 32)] private int minimumAmount = 2;
    [SerializeField, Range(1, 32)] private int maximumAmount = 8;
    [SerializeField, Range(1, 3)] private byte firePower = 3;

    [Header("Visual")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField, Min(0.05f)] private float explosionLife = 0.5f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private CellWorld2D world;
    private GrenadeMaterial material;
    private float charge;
    private float fuseLeft;
    private bool launched;


    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }


    public void Launch(
        Vector2 velocity,
        float throwCharge,
        GrenadeMaterial grenadeMaterial,
        CellWorld2D targetWorld,
        Collider2D[] ownerColliders)
    {
        world = targetWorld;
        material = grenadeMaterial;
        charge = Mathf.Clamp01(throwCharge);
        fuseLeft = fuseSeconds;
        launched = true;

        body.linearVelocity = velocity;
        body.angularVelocity = -velocity.x * 30f;

        //do not let the grenade bounce off the player who threw it
        Collider2D grenadeCollider = GetComponent<Collider2D>();
        if (grenadeCollider != null && ownerColliders != null)
        {
            foreach (Collider2D ownerCollider in ownerColliders)
            {
                if (ownerCollider != null)
                    Physics2D.IgnoreCollision(
                        grenadeCollider,
                        ownerCollider);
            }
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = material == GrenadeMaterial.Water
                ? new Color(0.25f, 0.65f, 1f)
                : new Color(1f, 0.45f, 0.1f);
        }
    }


    private void Update()
    {
        if (!launched) return;

        fuseLeft -= Time.deltaTime;
        if (fuseLeft <= 0f)
            Burst();
    }


    private void Burst()
    {
        int radius = Mathf.RoundToInt(Mathf.Lerp(
            minimumRadius,
            maximumRadius,
            charge));

        int amount = Mathf.RoundToInt(Mathf.Lerp(
            minimumAmount,
            maximumAmount,
            charge));

        if (world != null && world.IsInitialized)
        {
            Vector2 position = transform.position;

            switch (material)
            {
                case GrenadeMaterial.Water:
                    world.AddWater(position, radius, amount);
                    break;

                case GrenadeMaterial.Fuel:
                    world.AddFuel(position, radius, amount);
                    break;

                case GrenadeMaterial.Acid:
                    world.AddAcid(position, radius, amount);
                    break;

                case GrenadeMaterial.FireBomb:
                    //the fuel remains after the first flame like napalm
                    world.AddFuel(position, radius, amount);
                    world.Ignite(position, radius, firePower);
                    break;
            }
        }

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(
                explosionPrefab,
                transform.position,
                Quaternion.identity);

            Destroy(explosion, explosionLife);
        }

        Destroy(gameObject);
    }
}
