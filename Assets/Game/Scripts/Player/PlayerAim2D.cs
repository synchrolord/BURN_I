    using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerAim2D : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField]
    private Camera worldCamera;

    [Header("Things That Follow The Mouse")]
    [SerializeField]
    private Transform toolPivot;

    [SerializeField]
    private Transform torchPivot;
    [SerializeField] private PlayerMotor2D playerMotor;
    [SerializeField, Range(10f, 89f)] private float aimLimit = 80f;

    private float facing = 1f;
    private Vector3 toolPosition;
    private Vector3 torchPosition;
    
    
    private Vector2 aimScreenPosition;
    
    public Vector2 AimDirection { get; private set; }

    
    
    private void Awake()
    {
        if (worldCamera == null) { worldCamera = Camera.main; }
        if (playerMotor == null) { playerMotor = GetComponent<PlayerMotor2D>(); }
        //save initial positions for flipping
        if (toolPivot != null) {toolPosition = toolPivot.localPosition; }
        if (torchPivot != null) { torchPosition = torchPivot.localPosition; }
    }
    
    
    //store mouse position as vector
    public void OnAim(InputValue value)
    {
        aimScreenPosition = value.Get<Vector2>();
    }

    private void Update()
    {
        if (worldCamera == null)
        {
            return;
        }

        
       //world camera is positioned at -10z
        float distanceFromCamera =
            -worldCamera.transform.position.z;

        Vector3 mouseWorldPosition =
            worldCamera.ScreenToWorldPoint(
                new Vector3(
                    aimScreenPosition.x,
                    aimScreenPosition.y,
                    distanceFromCamera
                )
            );

        //calculate aim relative to the player.
        //calculate aim relative to the player
        Vector2 playerPosition = transform.position;
        Vector2 direction =
            (Vector2)mouseWorldPosition - playerPosition;

//do nothing if the mouse is exactly over the player
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();

//remember which side of the player the mouse is on
        if (Mathf.Abs(direction.x) > 0.05f)
        {
            facing = Mathf.Sign(direction.x);
        }

        if (playerMotor != null)
        {
            playerMotor.Face(facing);
        }

//get the angle as if the player was facing right
        float angle = Mathf.Atan2(
            direction.y,
            Mathf.Abs(direction.x)
        ) * Mathf.Rad2Deg;

//stop the tool from rotating behind the player
        angle = Mathf.Clamp(angle, -aimLimit, aimLimit);

//create the limited direction, then mirror it when facing left
        Vector2 limitedDirection =
            Quaternion.Euler(0f, 0f, angle) * Vector2.right;

        limitedDirection.x *= facing;

//assign the complete vector back to the property
        AimDirection = limitedDirection;

        if (toolPivot != null)
        {
            //move tool to the direction player faces
            Vector3 position = toolPosition;
            position.x = Mathf.Abs(toolPosition.x) * facing;
            toolPivot.localPosition = position;
            
            toolPivot.right = AimDirection;


        }

        if (torchPivot != null)
        {
            Vector3 position = torchPosition;
            position.x = Mathf.Abs(torchPosition.x) * facing;
            torchPivot.localPosition = position;

            torchPivot.right = AimDirection;
        }
    }
}