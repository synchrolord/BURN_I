#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in diagnostic snapshot. It never polls uninitialized worlds in edit mode.</summary>
public static class CellWorldVisibilityProbe
{
    [MenuItem("Tools/BURN/Write Simulation Diagnostics")]
    public static void WriteReport()
    {
        var text = new StringBuilder();
        text.AppendLine($"Playing={EditorApplication.isPlaying} Paused={EditorApplication.isPaused}");
        foreach (CellWorld2D world in Resources.FindObjectsOfTypeAll<CellWorld2D>())
        {
            if (!world.gameObject.scene.IsValid()) continue;
            text.AppendLine($"WORLD {world.name} scene={world.gameObject.scene.path} initialized={world.IsInitialized}");
            text.AppendLine($"Size={world.Width}x{world.Height} cell={world.CellSize} water={world.GetTotalWaterUnits()} fuel={world.GetTotalFuelUnits()} awake={world.GetActiveChunkCount()}");
            foreach (SpriteRenderer renderer in world.GetComponentsInChildren<SpriteRenderer>(true))
                text.AppendLine($"RENDER {renderer.name} enabled={renderer.enabled} layer={renderer.gameObject.layer} sorting={renderer.sortingLayerName}:{renderer.sortingOrder} bounds={renderer.bounds} visible={renderer.isVisible}");
        }
        foreach (PressureTool2D tool in Resources.FindObjectsOfTypeAll<PressureTool2D>())
        {
            if (!tool.gameObject.scene.IsValid()) continue;
            text.AppendLine($"TOOL {tool.name} material={tool.CurrentMaterial} held={tool.IsSpraying} airborne={tool.ActiveProbeCount} water={tool.CurrentWater} fuel={tool.CurrentFuel}");
        }
        foreach (Camera camera in Camera.allCameras)
            text.AppendLine($"CAMERA {camera.name} pos={camera.transform.position} culling={camera.cullingMask}");
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "cellworld-visibility.txt");
        File.WriteAllText(path, text.ToString());
        Debug.Log("BURN diagnostics written to " + path);
    }
}
#endif
