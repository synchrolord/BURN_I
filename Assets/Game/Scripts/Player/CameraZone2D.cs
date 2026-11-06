using System.Collections.Generic;
using UnityEngine;

//camera regions need no physics collider
public sealed class CameraZone2D : MonoBehaviour
{
    public static readonly List<CameraZone2D> Active = new List<CameraZone2D>();
    [SerializeField] private Vector2 size = new Vector2(32f, 18f);
    public float Area => size.x * size.y;
    public void SetSize(Vector2 value) => size = value;
    public bool Contains(Vector2 point)
    {
        Vector2 local = transform.InverseTransformPoint(point);
        return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f;
    }
    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    private void OnDisable() => Active.Remove(this);
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, size);
    }
}
