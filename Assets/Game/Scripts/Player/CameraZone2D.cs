using System.Collections.Generic;
using UnityEngine;

//camera regions need no physics collider
public sealed class CameraZone2D : MonoBehaviour
{
    public enum Mode { Static, Follow }
    public static readonly List<CameraZone2D> Active = new List<CameraZone2D>();
    [SerializeField] private Vector2 size = new Vector2(32f, 18f);
    [SerializeField] private BoxCollider2D area;
    [SerializeField] private Mode mode;
    [Tooltip("Visible height in world units. Higher values zoom out. Zero keeps the normal camera zoom.")]
    [SerializeField, Min(0f)] private float viewHeight;
    [SerializeField] private Vector2 followOffset = new Vector2(0f, 2f);
    [SerializeField] private int priority;
    public float Area => Bounds.size.x * Bounds.size.y;
    public int Priority => priority;
    public float ViewHeight => viewHeight;
    public Bounds Bounds => area != null ? new Bounds(
        area.transform.TransformPoint(area.offset),
        Vector3.Scale(area.size, area.transform.lossyScale)) :
        new Bounds(transform.position, Vector3.Scale(size, transform.lossyScale));

    public void SetSize(Vector2 value) => size = value;
    public bool Contains(Vector2 point)
    {
        Vector2 local = area != null ? (Vector2)area.transform.InverseTransformPoint(point) - area.offset :
            (Vector2)transform.InverseTransformPoint(point);
        Vector2 dimensions = area != null ? area.size : size;
        return Mathf.Abs(local.x) <= dimensions.x * 0.5f && Mathf.Abs(local.y) <= dimensions.y * 0.5f;
    }

    public void Configure(BoxCollider2D trigger, Mode movement, float height)
    {
        area = trigger;
        area.isTrigger = true;
        mode = movement;
        viewHeight = height;
        priority = 10;
    }

    public Vector2 CameraPosition(Vector2 player, float height, float aspect)
    {
        if (mode == Mode.Static) return transform.position;
        Bounds bounds = Bounds;
        Vector2 position = player + followOffset;
        float halfHeight = height * 0.5f;
        float halfWidth = halfHeight * aspect;
        //keep the whole view inside the area; centre an axis if the view is too large
        position.x = bounds.size.x <= halfWidth * 2f ? bounds.center.x :
            Mathf.Clamp(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        position.y = bounds.size.y <= halfHeight * 2f ? bounds.center.y :
            Mathf.Clamp(position.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);
        return position;
    }
    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    private void OnDisable() => Active.Remove(this);
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Bounds.center, Bounds.size);
        if (viewHeight <= 0f) return;
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(viewHeight * aspect, viewHeight, 0f));
    }
}
