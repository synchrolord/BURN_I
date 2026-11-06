using UnityEngine;

public sealed class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset;
    [SerializeField] private bool roomCamera;
    [SerializeField, Min(0f)] private float transitionTime = 0.15f;
    private Vector3 transitionVelocity;
    private CameraZone2D previousZone;

    public void UseRooms(bool value) => roomCamera = value;

    private void LateUpdate()
    {
        if (target == null) return;
        Vector3 destination = new Vector3(target.position.x + offset.x,
            target.position.y + offset.y, transform.position.z);
        CameraZone2D selected = null;
        if (roomCamera)
        foreach (CameraZone2D zone in CameraZone2D.Active)
            if (zone != null && zone.Contains(target.position) &&
                (selected == null || zone.Area < selected.Area)) selected = zone;

        if (selected != null)
        {
            destination.x = selected.transform.position.x;
            destination.y = selected.transform.position.y;
        }
        //teleports snap immediately instead of scrolling across the entire level
        if (!roomCamera || Vector2.Distance(transform.position, destination) > 60f)
        {
            transform.position = destination;
            transitionVelocity = Vector3.zero;
        }
        else
        {
            if (selected != previousZone) transitionVelocity = Vector3.zero;
            transform.position = Vector3.SmoothDamp(transform.position, destination,
                ref transitionVelocity, transitionTime);
        }
        previousZone = selected;
    }
}
