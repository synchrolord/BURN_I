using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GrenadeThrower2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerAim2D playerAim;
    [SerializeField] private PressureTool2D pressureTool;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private MaterialGrenade2D grenadePrefab;

    [Header("Throw")]
    [SerializeField, Min(0.1f)] private float minimumSpeed = 5f;
    [SerializeField, Min(0.1f)] private float maximumSpeed = 15f;
    [SerializeField, Min(0f)] private float upwardBoost = 2f;
    [SerializeField, Min(0.1f)] private float chargeSpeed = 0.8f;

    [Header("Testing")]
    [SerializeField] private bool showChargeBar = true;

    private Collider2D[] playerColliders;
    private bool charging;
    private float chargeTime;

    public bool IsCharging => charging;
    public float Charge => Mathf.PingPong(
        chargeTime * chargeSpeed,
        1f);


    private void Awake()
    {
        if (playerAim == null)
            playerAim = GetComponent<PlayerAim2D>();

        if (pressureTool == null)
            pressureTool = GetComponent<PressureTool2D>();

        playerColliders = GetComponentsInChildren<Collider2D>();
    }


    private void Update()
    {
        if (charging)
            chargeTime += Time.deltaTime;
    }


    //right mouse starts charging and releasing it throws
    public void OnThrow(InputValue value)
    {
        if (value.isPressed)
        {
            charging = true;
            chargeTime = 0f;
            return;
        }

        if (!charging) return;

        charging = false;
        ThrowGrenade();
    }


    private void ThrowGrenade()
    {
        if (grenadePrefab == null ||
            throwPoint == null ||
            playerAim == null ||
            pressureTool == null ||
            pressureTool.CurrentWorld == null)
        {
            return;
        }

        float throwCharge = Charge;
        float speed = Mathf.Lerp(
            minimumSpeed,
            maximumSpeed,
            throwCharge);

        Vector2 velocity =
            playerAim.AimDirection * speed +
            Vector2.up * upwardBoost;

        MaterialGrenade2D grenade = Instantiate(
            grenadePrefab,
            throwPoint.position,
            Quaternion.identity);

        MaterialGrenade2D.GrenadeMaterial material =
            pressureTool.CurrentSprayType ==
            PressureTool2D.SprayType.Water
                ? MaterialGrenade2D.GrenadeMaterial.Water
                : MaterialGrenade2D.GrenadeMaterial.FireBomb;

        grenade.Launch(
            velocity,
            throwCharge,
            material,
            pressureTool.CurrentWorld,
            playerColliders);
    }


    private void OnDisable()
    {
        charging = false;
        chargeTime = 0f;
    }


    private void OnGUI()
    {
        if (!showChargeBar || !charging) return;

        Rect background = new Rect(
            Screen.width * 0.5f - 80f,
            Screen.height - 60f,
            160f,
            18f);

        GUI.Box(background, GUIContent.none);

        Rect fill = new Rect(
            background.x + 3f,
            background.y + 3f,
            (background.width - 6f) * Charge,
            background.height - 6f);

        Color oldColor = GUI.color;
        GUI.color = Color.yellow;
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }
}
