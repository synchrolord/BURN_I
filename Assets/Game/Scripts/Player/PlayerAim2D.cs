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

    private Vector2 aimScreenPosition;
    
    public Vector2 AimDirection { get; private set; }

    
    
    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
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
        Vector2 playerPosition = transform.position;

        AimDirection = (Vector2)mouseWorldPosition - playerPosition;

        //if mouse is almost exactly over player
        if (AimDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }
        
        AimDirection.Normalize();

        // Rotate the weapon.
        if (toolPivot != null)
        {
            toolPivot.right = AimDirection;
        }

        // Rotate the torch.
        if (torchPivot != null)
        {
            torchPivot.right = AimDirection;
        }
    }
}