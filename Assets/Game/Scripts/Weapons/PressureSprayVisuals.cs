using System.Collections.Generic;
using UnityEngine;

// Plain helper, not a component. It draws the tool's drops and never moves them.
internal sealed class PressureSprayVisuals
{
    private readonly ParticleSystem drawing;
    private readonly ParticleSystem.Particle[] particles;

    public PressureSprayVisuals(Transform parent, CellWorld2D world, int capacity, int sortingOffset)
    {
        particles = new ParticleSystem.Particle[capacity];
        GameObject visual = new GameObject("Pressure spray (runtime)");
        visual.transform.SetParent(parent, false);
        drawing = visual.AddComponent<ParticleSystem>();
        drawing.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = drawing.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = capacity;
        var emission = drawing.emission;
        emission.enabled = false;
        ParticleSystemRenderer renderer = drawing.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Resources.Load<Material>("Effects/PressureSpray");
        if (renderer.sharedMaterial == null)
            Debug.LogWarning("Missing Resources/Effects/PressureSpray material.", parent);
        if (world != null)
        {
            SpriteRenderer worldRenderer = world.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = worldRenderer.sortingLayerID;
            renderer.sortingOrder = worldRenderer.sortingOrder + sortingOffset;
        }
        drawing.Pause();
    }

    public void Draw(List<PressureTool2D.Drop> drops, float waterWidth, float flameWidth)
    {
        for (int i = 0; i < drops.Count; i++)
        {
            PressureTool2D.Drop drop = drops[i];
            bool water = drop.material == PressureTool2D.ToolMaterial.Water;
            float age = 1 - drop.life / drop.startingLife;
            float width = water ? waterWidth : flameWidth * (0.65f + age);
            Color color = water ? new Color(0.2f, 0.72f, 1, 0.9f)
                : Color.Lerp(new Color(1, 0.9f, 0.22f), new Color(1, 0.18f, 0.025f), age);
            color.a *= 1 - age;
            particles[i] = new ParticleSystem.Particle
            {
                position = drop.position,
                startSize3D = new Vector3(width, water ? width * 2 : width * 2.3f, width),
                rotation = 90 - Mathf.Atan2(drop.velocity.y, drop.velocity.x) * Mathf.Rad2Deg,
                startColor = color,
                startLifetime = 1,
                remainingLifetime = 1
            };
        }
        drawing.SetParticles(particles, drops.Count);
    }

    public void Clear()
    {
        if (drawing != null) drawing.Clear();
    }

    public void Destroy()
    {
        if (drawing != null) Object.Destroy(drawing.gameObject);
    }
}
