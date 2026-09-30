using UnityEngine;

//choose the active room before the worlds update
[DefaultExecutionOrder(-100)]
public sealed class CellWorldRoomManager : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private CellWorld2D[] worlds = new CellWorld2D[0];
    [SerializeField] private PressureTool2D pressureTool;
    [SerializeField] private FireLightPool2D fireLightPool;
    public CellWorld2D CurrentWorld { get; private set; }

    private void Update()
    {
        //keep the current room while the player is inside it
        if (!ContainsPlayer(CurrentWorld))
        {
            CurrentWorld = null;

            //otherwise find a room containing the player
            foreach (CellWorld2D world in worlds)
            {
                if (!ContainsPlayer(world)) continue;

                CurrentWorld = world;
                break;
            }
        }

        //only the selected room should simulate
        foreach (CellWorld2D world in worlds)
        {
            if (world != null)
                world.SetRoomActive(world == CurrentWorld);
        }
        
        if (pressureTool != null)
            pressureTool.SetWorld(CurrentWorld);

        if (fireLightPool != null)
            fireLightPool.SetWorld(CurrentWorld);
    }

    private bool ContainsPlayer(CellWorld2D world)
    {
        if (player == null || world == null) return false;
        if (!world.isActiveAndEnabled || !world.IsInitialized) return false;

        return world.TryWorldToCell(player.position, out _);
    }

    private void OnDisable()
    {
        //release control if this manager is disabled
        foreach (CellWorld2D world in worlds)
        {
            if (world != null)
                world.SetRoomActive(true);
        }

        CurrentWorld = null;
        
        if (pressureTool != null)
            pressureTool.SetWorld(CurrentWorld);

        if (fireLightPool != null)
            fireLightPool.SetWorld(CurrentWorld);
    }
}
