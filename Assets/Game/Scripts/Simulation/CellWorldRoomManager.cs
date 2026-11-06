using UnityEngine;

//choose the active room before the worlds update
[DefaultExecutionOrder(-100)]

public sealed class CellWorldRoomManager : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private CellWorld2D[] worlds = new CellWorld2D[0];
    [SerializeField] private PressureTool2D pressureTool;
    
    [SerializeField]
    private PlayerLiquidInteraction2D playerLiquidInteraction;
    public CellWorld2D CurrentWorld { get; private set; }

    public void RegisterWorld(CellWorld2D world)
    {
        if (world == null || System.Array.IndexOf(worlds, world) >= 0) return;
        int index = worlds.Length;
        System.Array.Resize(ref worlds, index + 1);
        worlds[index] = world;
        world.SetActivityTarget(player);
        world.SetRoomActive(false);
    }

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
        
        
        //change the active cellWorld
        if (pressureTool != null)
            pressureTool.SetWorld(CurrentWorld);

        if (playerLiquidInteraction != null)
        {
            playerLiquidInteraction.SetWorld(CurrentWorld);
        }
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

        if (playerLiquidInteraction != null)
        {
            playerLiquidInteraction.SetWorld(null);
        }
    }
    
    
    
    
}
