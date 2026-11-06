using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class CameraFollow2D : MonoBehaviour
{
    
    // field for player object to attach camera too
    [SerializeField]
    private Transform target;

    [SerializeField]
    private Vector2 offset;
    [SerializeField] private bool roomCamera;
    [SerializeField, Min(0f)] private float transitionTime = 0.15f;
    private Vector3 transitionVelocity;
    private CameraZone2D previousZone;
    private Camera view;
    private PixelPerfectCamera pixelCamera;
    private bool wasPixelPerfect;
    private bool screenZoom;
    private float normalSize;
    private float zoomVelocity;

    private void Awake()
    {
        view = GetComponent<Camera>();
        pixelCamera = GetComponent<PixelPerfectCamera>();
        if (view != null) normalSize = view.orthographicSize;
    }

    public void UseRooms(bool value) => roomCamera = value;
    
    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 destination = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z
        );

        CameraZone2D selected = null;
        if (roomCamera)
        foreach (CameraZone2D zone in CameraZone2D.Active)
        {
            if (zone != null && zone.Contains(target.position) &&
                (selected == null || zone.Priority > selected.Priority ||
                    (zone.Priority == selected.Priority && zone.Area < selected.Area))) selected = zone;
        }

        bool hasZoom = selected != null && selected.ViewHeight > 0f && view != null;
        if (hasZoom && !screenZoom)
        {
            normalSize = view.orthographicSize;
            wasPixelPerfect = pixelCamera != null && pixelCamera.enabled;
            //screens own the zoom, otherwise Pixel Perfect replaces it before rendering
            if (wasPixelPerfect) pixelCamera.enabled = false;
        }
        else if (!hasZoom && screenZoom)
        {
            view.orthographicSize = normalSize;
            if (wasPixelPerfect && pixelCamera != null) pixelCamera.enabled = true;
            zoomVelocity = 0f;
        }
        screenZoom = hasZoom;
        if (hasZoom)
            view.orthographicSize = Vector2.Distance(transform.position, target.position) > 60f || transitionTime <= 0f
                ? selected.ViewHeight * 0.5f : Mathf.SmoothDamp(view.orthographicSize,
                    selected.ViewHeight * 0.5f, ref zoomVelocity, transitionTime);

        if (selected != null)
        {
            Vector2 position = selected.CameraPosition(target.position,
                view != null ? view.orthographicSize * 2f : 22.5f,
                view != null ? view.aspect : 16f / 9f);
            destination.x = position.x;
            destination.y = position.y;
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

    private void OnDisable()
    {
        if (!screenZoom) return;
        if (view != null) view.orthographicSize = normalSize;
        if (wasPixelPerfect && pixelCamera != null) pixelCamera.enabled = true;
        screenZoom = false;
    }
}
