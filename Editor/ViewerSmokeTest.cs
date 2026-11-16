using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Liberty3D.ProductViewer.Editor
{
    /// <summary>Exercise actual transitions in Play mode, without modifying authoring data.</summary>
    public static class ViewerSmokeTest
    {
        static ProductViewer viewer;
        static int step;
        static double next, deadline;
        static string report;
        [MenuItem("Tools/Liberty 3D/Run interaction smoke test (Play mode) %#j")]
        public static void Run()
        {
            viewer = UnityEngine.Object.FindFirstObjectByType<ProductViewer>();
            if (!EditorApplication.isPlaying || !viewer) { Debug.LogWarning("Enter Play mode in ProductViewer_Demo first."); return; }
            step = 0; report = "Liberty3D Play mode smoke test\n"; deadline = EditorApplication.timeSinceStartup + 30;
            next = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        static void Assert(bool condition, string label)
        {
            if (!condition) throw new Exception(label + " failed: " + viewer.DebugSnapshot);
            report += "PASS " + label + "\n";
        }
        static void Tick()
        {
            try
            {
                if (!viewer || !EditorApplication.isPlaying) throw new Exception("Play mode stopped.");
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Transition timeout.");
                if (EditorApplication.timeSinceStartup < next || viewer.Busy) return;
                switch (step++)
                {
                    case 0: viewer.OpenProduct(viewer.catalog.products[0]); break;
                    case 1: Assert(viewer.State == ViewerState.Assembled && viewer.AutoRotating, "open + auto rotation"); viewer.ToggleExplosion(); break;
                    case 2: Assert(viewer.State == ViewerState.Exploded && !viewer.AutoRotating, "explode stops rotation"); viewer.RequestPart(UnityEngine.Object.FindFirstObjectByType<ProductRig>().parts[2]); viewer.ConfirmPart(); break;
                    case 3: Assert(viewer.State == ViewerState.Detail && viewer.SelectedPart.partId == "shaft", "confirmed isolation"); viewer.ReturnOverview(); break;
                    case 4: Assert(viewer.State == ViewerState.Exploded && !viewer.SelectedPart, "return to exploded overview"); viewer.ToggleExplosion(); break;
                    case 5: Assert(viewer.State == ViewerState.Assembled && !viewer.AutoRotating, "toggle reassembly"); ViewerLanguage.Set(true); Assert(ViewerLanguage.English, "English locale"); viewer.ResetView(); break;
                    case 6: Assert(viewer.AutoRotating && viewer.State == ViewerState.Assembled, "reset resumes rotation"); ViewerLanguage.Set(false); viewer.Home(); break;
                    case 7: Assert(viewer.State == ViewerState.Catalog, "home preserves catalog"); Finish(); return;
                }
                next = EditorApplication.timeSinceStartup + .85;
            }
            catch (Exception e) { report += "FAIL " + e.Message; Finish(); Debug.LogException(e); }
        }
        static void Finish()
        {
            EditorApplication.update -= Tick; File.WriteAllText("Liberty3D-smoke-test.txt", report); Debug.Log(report);
        }
    }
}
