using System.Collections.Generic;
using UnityEngine;

//place one light inside a cellworld
public sealed class CellLight2D : MonoBehaviour
{
    internal static readonly List<CellLight2D> ActiveLights =
        new List<CellLight2D>();
    private static float nextFlickerOffset;

    [SerializeField] private CellWorld2D world;
    [SerializeField, Range(1, 64)] private int lightLevel = 28;

    [SerializeField, Range(0f, 1f)]
    private float flickerAmount = 0.15f;

    [SerializeField, Min(0f)]
    private float flickerSpeed = 4f;

    private float flickerOffset;

    internal int LightLevel
    {
        get
        {
            //smooth random flicker that only makes the light smaller
            float flicker = Mathf.PerlinNoise(
                flickerOffset,
                Time.time * flickerSpeed);

            return Mathf.RoundToInt(Mathf.Lerp(
                lightLevel * (1f - flickerAmount),
                lightLevel,
                flicker));
        }
    }


    private void OnEnable()
    {
        flickerOffset = nextFlickerOffset;
        nextFlickerOffset += 3.7f;

        if (!ActiveLights.Contains(this))
            ActiveLights.Add(this);
    }


    private void OnDisable()
    {
        ActiveLights.Remove(this);
    }


    internal bool TryGetCell(
        CellWorld2D targetWorld,
        out Vector2Int cell)
    {
        cell = default;

        if (targetWorld == null ||
            (world != null && world != targetWorld))
        {
            return false;
        }

        return targetWorld.TryWorldToCell(
            transform.position,
            out cell);
    }


    private void OnDrawGizmosSelected()
    {
        float size = world != null
            ? world.CellSize
            : 0.125f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            lightLevel * size);
    }
}
