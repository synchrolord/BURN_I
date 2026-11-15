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
    private float dialogueZoom = 1f;
    private float currentDialogueZoom = 1f;

    public void SetDialogueZoom(bool active) => dialogueZoom = active ? 0.85f : 1f;

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

        //dialogue uses the existing screen zoom and keeps moving while gameplay is paused
        currentDialogueZoom = Mathf.MoveTowards(currentDialogueZoom, dialogueZoom, Time.unscaledDeltaTime * 0.6f);
        bool hasScreenZoom = selected != null && selected.ViewHeight > 0f;
        bool hasZoom = view != null && (hasScreenZoom || currentDialogueZoom < 1f || dialogueZoom < 1f);
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
                ? (hasScreenZoom ? selected.ViewHeight * 0.5f : normalSize) * currentDialogueZoom
                : Mathf.SmoothDamp(view.orthographicSize,
                    (hasScreenZoom ? selected.ViewHeight * 0.5f : normalSize) * currentDialogueZoom,
                    ref zoomVelocity, transitionTime, Mathf.Infinity, Time.unscaledDeltaTime);

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
                ref transitionVelocity, transitionTime, Mathf.Infinity, Time.unscaledDeltaTime);
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
