    using System.Collections.Generic;
using UnityEngine;

// Plain helper, not a component. It draws the tool's shots and never moves them.
internal sealed class PressureSprayVisuals
{
    
    private readonly ParticleSystem particleSystem;
    private readonly ParticleSystem.Particle[] particleBuffer;

    public PressureSprayVisuals(Transform toolTransform, CellWorld2D cellWorld, int maximumShotCount, int sortingOrderOffset)
    {
        //allocate space for visuals
        particleBuffer = new ParticleSystem.Particle[maximumShotCount];
        
        //create new particle gameObject under tool to use for visuals
        GameObject particleObject = new GameObject("Pressure spray (runtime)");
        particleObject.transform.SetParent(toolTransform, false);
        
        //create particleSystem component and clear it
        particleSystem = particleObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        //configure particleSystem
        var main = particleSystem.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maximumShotCount;
        
        
        var emission = particleSystem.emission;
        emission.enabled = false;
        
        
        //initialise Renderer
        ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = Resources.Load<Material>("Effects/PressureSpray");
        if (particleRenderer.sharedMaterial == null)
            Debug.LogWarning("Missing Resources/Effects/PressureSpray sprayType.", toolTransform);
        
        
        //get Renderer from cellWorld2D
        if (cellWorld != null)
        {
            SpriteRenderer worldRenderer = cellWorld.GetComponent<SpriteRenderer>();
            particleRenderer.sortingLayerID = worldRenderer.sortingLayerID;
            particleRenderer.sortingOrder = worldRenderer.sortingOrder + sortingOrderOffset;
        }
        particleSystem.Pause();
    }

    
    //handle drawing all  particles
    public void Draw(List<PressureTool2D.SprayShot> activeShots, float waterShotWidth, float flameShotWidth)
    {
        //Draw every shot in activeShot arraylist
        for (int i = 0; i < activeShots.Count; i++)
        {   
            //store variables for current shot
            PressureTool2D.SprayShot shot = activeShots[i];
            bool isWater = shot.sprayType == PressureTool2D.SprayType.Water;
            float lifetimeProgress = 1 - shot.remainingLifetimeSeconds / shot.initialLifetimeSeconds;
            float width = isWater ? waterShotWidth : flameShotWidth * (0.65f + lifetimeProgress);
            
            //decide color based on material
            Color color = isWater ? new Color(0.2f, 0.72f, 1, 0.9f)
                : Color.Lerp(new Color(1, 0.9f, 0.22f), new Color(1, 0.18f, 0.025f), lifetimeProgress);
            color.a *= 1 - lifetimeProgress;
            float directionAngleDegrees = Mathf.Atan2(shot.velocity.y, shot.velocity.x) * Mathf.Rad2Deg;
            
            // create ParticleSystem.Particle and store in buffer with shot's properties  
            particleBuffer[i] = new ParticleSystem.Particle
            {
                position = shot.position,
                startSize3D = new Vector3(width, isWater ? width * 2 : width * 2.3f, width),
                rotation = 90 - directionAngleDegrees,
                startColor = color,
                startLifetime = 1,
                remainingLifetime = 1
            };
        }
        //push particle buffer to particleSystem
        particleSystem.SetParticles(particleBuffer, activeShots.Count);
    }

    
    public void Clear()
    {
        if (particleSystem != null) particleSystem.Clear();
    }
    public void Destroy()
    {
        if (particleSystem != null) Object.Destroy(particleSystem.gameObject);
    }
}
