using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class InventorySelectionBuilder
{
    public static void BuildMac()
    {
        var scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes,s=>s.enabled),s=>s.path);
        var report = BuildPipeline.BuildPlayer(scenes,"Builds/InventorySelection/SiliconSandbox.app",
            BuildTarget.StandaloneOSX,BuildOptions.Development);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Inventory selection build failed.");
        Debug.Log("INVENTORY_SELECTION_MAC_BUILD_PASS");
    }
}
