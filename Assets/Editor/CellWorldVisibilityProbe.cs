#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class CellWorldVisibilityProbe
{
    private static double next;
    private static int remaining = 20;
    static CellWorldVisibilityProbe()
    {
        next = EditorApplication.timeSinceStartup + 2;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += mode => { remaining = 20; next = EditorApplication.timeSinceStartup + 2; };
    }
    private static object Field(object o, string n) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(o);
    private static void Tick()
    {
        if (remaining <= 0 || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 2;
        remaining--;
        var text = new StringBuilder();
        text.AppendLine("Playing=" + EditorApplication.isPlaying + " Paused=" + EditorApplication.isPaused);
        foreach (CellWorld2D w in Resources.FindObjectsOfTypeAll<CellWorld2D>())
        {
            if (!w.gameObject.scene.IsValid()) continue;
            text.AppendLine("WORLD " + w.name + " scene=" + w.gameObject.scene.path + " enabled=" + w.enabled + " active=" + w.gameObject.activeInHierarchy + " pos=" + w.transform.position + " scale=" + w.transform.lossyScale);
            text.AppendLine("Size=" + w.Width + "x" + w.Height + " cell=" + w.CellSize + " water=" + w.GetTotalWaterUnits() + " fuel=" + w.GetTotalFuelUnits() + " awake=" + w.GetActiveChunkCount());
            foreach (string n in new[] { "managedExternally", "paused", "solidMask", "simulateAllChunks", "textureNeedsRefresh", "tickNumber" }) text.AppendLine(n + "=" + Field(w,n));
            foreach (string n in new[] { "fire", "smoke" }) { var b = Field(w,n) as byte[]; int count=0; if(b!=null) foreach(byte v in b) if(v>0) count++; text.AppendLine(n + " cells="+count); }
            var rr = w.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i=0; i<rr.Length && i<12; i++)
            {
                var r=rr[i]; var m=r.sharedMaterial;
                text.AppendLine("RENDER " + r.name + " enabled="+r.enabled+" forceOff="+r.forceRenderingOff+" active="+r.gameObject.activeInHierarchy+" layer="+r.gameObject.layer+" sort="+r.sortingLayerName+":"+r.sortingOrder+" color="+r.color+" sprite="+(r.sprite?r.sprite.name:"null")+" material="+(m?m.name:"null")+" shader="+(m&&m.shader?m.shader.name:"null")+" bounds="+r.bounds+" visible="+r.isVisible);
            }
        }
        foreach (Camera c in Camera.allCameras) text.AppendLine("CAMERA " + c.name+" pos="+c.transform.position+" ortho="+c.orthographicSize+" culling="+c.cullingMask+" clips="+c.nearClipPlane+","+c.farClipPlane);
        foreach (PressureTool2D p in Resources.FindObjectsOfTypeAll<PressureTool2D>()) if(p.gameObject.scene.IsValid()) text.AppendLine("TOOL "+p.name+" pos="+p.transform.position+" world="+Field(p,"cellWorld"));
        File.WriteAllText("D:/Project Root/Temp/cellworld-visibility.txt", text.ToString());
    }
}
#endif
