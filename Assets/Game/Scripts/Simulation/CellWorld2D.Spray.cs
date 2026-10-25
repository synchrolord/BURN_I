using System.Collections.Generic;
using UnityEngine;

public partial class CellWorld2D
{
    // One traveled segment
    public struct ShotPath
    {
        public Vector2 startPosition;
        public Vector2 endPosition;
        public Vector2 insideSurfacePosition;
        public bool hitObstacle;
    }
    //use Raycast to detect collision of shots with environment
    private readonly List<RaycastHit2D> shotHits = new List<RaycastHit2D>(16);

    
    //tace path of shots.
    public ShotPath TraceShotPath(Vector2 startPosition, Vector2 endPosition, LayerMask obstacleLayers, Transform shooter)
    {
        //construct a path struct for referencing
        ShotPath path = new ShotPath
        {
            startPosition = startPosition,
            endPosition = endPosition
        };
        
        //set the obstacle layer as collision for materials
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(obstacleLayers);
        filter.useTriggers = false;
        Physics2D.Linecast(startPosition, endPosition, filter, shotHits);

        
        // 
        foreach (RaycastHit2D hit in shotHits)
        {
            //ignore if it is the players collider
            if (shooter != null && hit.collider.transform.IsChildOf(shooter)) continue;
            path.hitObstacle = true;
            path.endPosition = hit.point + hit.normal * (cellSize * 0.55f);
            path.insideSurfacePosition = hit.point - hit.normal * (cellSize * 0.5f);
            break;
        }

        //stop shots at CellWorld walls that do not have Unity colliders
        Vector2 direction = (path.endPosition - startPosition).normalized;
        int sampleCount = GetPathSampleCount(path);

        for (int i = 1; i <= sampleCount; i++)
        {
            Vector2 point = Vector2.Lerp(
                startPosition,
                path.endPosition,
                i / (float)sampleCount);

            if (!TryWorldToCell(point, out Vector2Int cell) ||
                !solid[Index(cell.x, cell.y)])
            {
                continue;
            }

            path.hitObstacle = true;
            path.endPosition = point - direction * (cellSize * 0.55f);
            path.insideSurfacePosition = point;
            break;
        }

        return path;
    }

    
    
    
    //
    public int ApplyWaterShot(ShotPath path, int remainingWaterUnits, bool lifetimeExpired)
    {
        
        int sampleCount = GetPathSampleCount(path);
        for (int i = 1; i <= sampleCount && remainingWaterUnits > 0; i++)
        {
            //use water if it overlaps fire
            Vector2 point = Vector2.Lerp(path.startPosition, path.endPosition, i / (float)sampleCount);
            if (GetFireEnergy(point) > 0)
                remainingWaterUnits -= ApplyWaterImpact(point, remainingWaterUnits);
        }

        //wet plants at the wall contact before depositing leftover water
        if (path.hitObstacle) remainingWaterUnits -= ApplyPlantWaterImpact(path.insideSurfacePosition, remainingWaterUnits);
        //try to deposit the remaining water when the shot hits something or expires
        if (path.hitObstacle || lifetimeExpired)
        {
            ApplyWaterImpact(path.endPosition, remainingWaterUnits);
            return 0;
        }
        return remainingWaterUnits;
    }

    //ignite cells along the flame's path and plants at the contact surface
    public void ApplyFlameShot(ShotPath path, int ignitionRadiusCells)
    {
        int sampleCount = GetPathSampleCount(path);
        for (int i = 1; i <= sampleCount; i++)
        {
            Vector2 point = Vector2.Lerp(path.startPosition, path.endPosition, i / (float)sampleCount);
            Ignite(point, ignitionRadiusCells);
        }
        if (path.hitObstacle)
        {
            Ignite(path.insideSurfacePosition, ignitionRadiusCells);
        }
    }

    //sample the traveled segment at intervals no larger than half a cell
    private int GetPathSampleCount(ShotPath path)
    {
        return Mathf.Max(
            1,
            
            Mathf.CeilToInt(Vector2.Distance(path.startPosition, path.endPosition) / (cellSize * 0.5f)));
    }
}
