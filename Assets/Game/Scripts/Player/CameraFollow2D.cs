using Unity.AppUI.UI;
using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    
    // field for player object to attach camera too
    [SerializeField]
    private Transform target;

    [SerializeField]
    private Vector2 offset;
    
    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // change camera object position  to player position + offset
        transform.position = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );
    }
}
        