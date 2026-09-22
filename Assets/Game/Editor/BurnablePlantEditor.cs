using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

[CustomEditor(typeof(BurnablePlantTile)), CanEditMultipleObjects]
public sealed class BurnablePlantTileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Stages 0–4: healthy → last few pixels. The tile disappears after stage 4 finishes. Keep all five sprites on the same canvas, pivot and pixels-per-unit.", MessageType.Info);
        foreach (BurnablePlantTile tile in targets)
        {
            if (!tile.HasAllStages) EditorGUILayout.HelpBox(tile.name + ": missing stages reuse the previous sprite for testing. Assign all five for the finished art.", MessageType.Warning);
            if (!tile.HasBakedMask) EditorGUILayout.HelpBox(tile.name + ": bake the healthy sprite mask before playing.", MessageType.Error);
        }
        if (GUILayout.Button("Bake / Refresh Healthy Sprite Mask"))
        {
            foreach (BurnablePlantTile tile in targets) BurnablePlantEditor.BakeMask(tile);
            AssetDatabase.SaveAssets();
        }
    }
}

public static class BurnablePlantEditor
{
    private const string Root = "Assets/Game/Tiles/BurnablePlants";
    private static readonly Regex StageName = new Regex(@"^(.*)_burn([0-4])$", RegexOptions.IgnoreCase);

    [MenuItem("Tools/BURN/Plants/Create Tiles From Selected Sprites")]
    public static void CreateFromSelection()
    {
        var sprites = new HashSet<Sprite>();
        foreach (UnityEngine.Object selected in Selection.objects)
        {
            if (selected is Sprite sprite) sprites.Add(sprite);
            else if (selected is Texture2D)
                foreach (Sprite sub in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(selected)).OfType<Sprite>()) sprites.Add(sub);
        }
        if (sprites.Count == 0)
        {
            EditorUtility.DisplayDialog("Burnable plants", "Select sprite slices or a spritesheet in the Project window first.", "OK");
            return;
        }
        Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        var groups = new SortedDictionary<string, Sprite[]>(StringComparer.Ordinal);
        foreach (Sprite sprite in sprites)
        {
            Match match = StageName.Match(sprite.name);
            string name = match.Success ? match.Groups[1].Value : sprite.name;
            int stage = match.Success ? int.Parse(match.Groups[2].Value) : 0;
            if (!groups.TryGetValue(name, out Sprite[] stages)) groups.Add(name, stages = new Sprite[5]);
            if (stages[stage] != null)
            {
                Debug.LogError("Duplicate sprite name/stage: " + sprite.name + ". Give each plant set a unique name.");
                return;
            }
            stages[stage] = sprite;
        }
        var created = new List<UnityEngine.Object>();
        foreach (var group in groups)
        {
            if (group.Value[0] == null) { Debug.LogWarning("Skipping " + group.Key + ": select its _burn0 healthy sprite too."); continue; }
            BurnablePlantTile tile = ScriptableObject.CreateInstance<BurnablePlantTile>();
            tile.SetArtwork(group.Value);
            if (!BakeMask(tile)) { UnityEngine.Object.DestroyImmediate(tile); continue; }
            string safeName = string.Concat(group.Key.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            AssetDatabase.CreateAsset(tile, AssetDatabase.GenerateUniqueAssetPath(Root + "/" + safeName + ".asset"));
            created.Add(tile);
        }
        AssetDatabase.SaveAssets();
        Selection.objects = created.ToArray();
        Debug.Log($"Created {created.Count} burnable plant tiles in {Root}. Drag these assets into the Tile Palette; paint on a BurnablePlantMap2D Tilemap.");
    }

    [MenuItem("Tools/BURN/Plants/Bake All Plant Masks")]
    public static void BakeAll()
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:BurnablePlantTile"))
            if (BakeMask(AssetDatabase.LoadAssetAtPath<BurnablePlantTile>(AssetDatabase.GUIDToAssetPath(guid)))) count++;
        AssetDatabase.SaveAssets();
        Debug.Log($"Baked {count} plant masks. Read/Write remains disabled on the imported textures.");
    }

    public static bool BakeMask(BurnablePlantTile tile)
    {
        Sprite healthy = tile.GetStageSprite(0);
        if (healthy == null) { Debug.LogError("Assign a healthy sprite to stage 0 first.", tile); return false; }
        for (int stage = 1; stage < 5; stage++)
        {
            Sprite sprite = tile.GetStageSprite(stage);
            if (sprite.rect.size != healthy.rect.size || (sprite.pivot - healthy.pivot).sqrMagnitude > 0.001f
                || !Mathf.Approximately(sprite.pixelsPerUnit, healthy.pixelsPerUnit))
            {
                Debug.LogError($"{tile.name}: stage {stage} must match the healthy sprite's canvas size, pivot and pixels per unit. Use transparent padding, including on the last few-pixel sprite.", tile);
                return false;
            }
        }
        string path = AssetDatabase.GetAssetPath(healthy);
        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                throw new InvalidOperationException("Export the plant sheet as PNG before baking its alpha mask.");
            byte[] bytes = File.ReadAllBytes(path);
            if (!ImageConversion.LoadImage(source, bytes, false)) throw new InvalidOperationException("Could not read source image.");
            if (source.width != healthy.texture.width || source.height != healthy.texture.height)
                throw new InvalidOperationException("Set the sheet's Max Size high enough to import it at its original resolution before baking.");
            // Read original pixels, not packed atlas UVs; sprites may be non-readable at runtime.
            Color32[] pixels = source.GetPixels32();
            Rect rect = healthy.rect;
            var samples = new List<Vector2>();
            for (int y = 0; y < Mathf.RoundToInt(rect.height); y++)
            for (int x = 0; x < Mathf.RoundToInt(rect.width); x++)
            {
                int px = Mathf.RoundToInt(rect.x) + x, py = Mathf.RoundToInt(rect.y) + y;
                if (px < 0 || py < 0 || px >= source.width || py >= source.height) continue;
                if (pixels[px + py * source.width].a < 32) continue;
                samples.Add((new Vector2(x + 0.5f, y + 0.5f) - healthy.pivot) / healthy.pixelsPerUnit);
            }
            if (samples.Count == 0) throw new InvalidOperationException("Healthy sprite has no visible pixels.");
            Undo.RecordObject(tile, "Bake plant mask");
            tile.SetBakedMask(samples.ToArray(), AssetDatabase.GetAssetDependencyHash(path).ToString());
            EditorUtility.SetDirty(tile);
            return true;
        }
        catch (Exception exception) { Debug.LogError($"Could not bake {healthy.name}: {exception.Message}", tile); return false; }
        finally { UnityEngine.Object.DestroyImmediate(source); }
    }

    [MenuItem("Tools/BURN/Plants/Create Burnable Plant Tilemap")]
    public static void CreateMap()
    {
        if (EditorApplication.isPlaying) return;
        Grid grid = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Grid>() : null;
        if (grid == null) grid = UnityEngine.Object.FindAnyObjectByType<Grid>();
        if (grid == null)
        {
            var gridObject = new GameObject("Grid", typeof(Grid));
            Undo.RegisterCreatedObjectUndo(gridObject, "Create plant grid");
            grid = gridObject.GetComponent<Grid>();
        }
        var go = new GameObject("Burnable Plants", typeof(Tilemap), typeof(TilemapRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Create burnable plant map");
        go.transform.SetParent(grid.transform, false);
        BurnablePlantMap2D manager = Undo.AddComponent<BurnablePlantMap2D>(go);
        manager.World = UnityEngine.Object.FindAnyObjectByType<CellWorld2D>();
        TilemapRenderer renderer = go.GetComponent<TilemapRenderer>();
        renderer.mode = TilemapRenderer.Mode.Chunk;
        // Reuse the project's sprite material and put vegetation immediately behind its fire layer.
        if (manager.World != null)
        {
            SpriteRenderer fire = manager.World.GetComponent<SpriteRenderer>();
            renderer.sortingLayerID = fire.sortingLayerID;
            renderer.sortingOrder = fire.sortingOrder - 1;
            renderer.sharedMaterial = fire.sharedMaterial;
        }
        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}

/// <summary>Catch forgotten/stale masks at build time rather than shipping non-burning plants.</summary>
public sealed class BurnablePlantBuildCheck : UnityEditor.Build.IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BurnablePlantTile"))
        {
            BurnablePlantTile tile = AssetDatabase.LoadAssetAtPath<BurnablePlantTile>(AssetDatabase.GUIDToAssetPath(guid));
            Sprite healthy = tile.GetStageSprite(0);
            if (!tile.HasBakedMask || healthy == null
                || tile.BakedSourceHash != AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(healthy)).ToString())
                throw new UnityEditor.Build.BuildFailedException("Plant mask missing/stale: " + AssetDatabase.GetAssetPath(tile) + ". Use Tools > BURN > Plants > Bake All Plant Masks.");
        }
    }
}
