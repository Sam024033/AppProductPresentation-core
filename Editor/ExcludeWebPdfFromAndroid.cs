#if UNITY_EDITOR && UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;
namespace Liberty3D.ProductViewer.Editor
{
    // PDF.js is used only by WebGL; Android uses its native PDF renderer.
    // The callback edits only the generated Gradle project, never Assets/StreamingAssets.
    internal sealed class ExcludeWebPdfFromAndroid : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var generatedAssets = Path.GetFullPath(Path.Combine(path, "src", "main", "assets"));
            var generatedPdf = Path.GetFullPath(Path.Combine(generatedAssets, "LibertyPdf"));
            var expected = generatedAssets.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar + "LibertyPdf";
            if (!string.Equals(generatedPdf, expected, System.StringComparison.OrdinalIgnoreCase))
                throw new System.InvalidOperationException("Unexpected generated Android PDF path.");
            if (Directory.Exists(generatedPdf)) { Directory.Delete(generatedPdf, true); Debug.Log("Liberty 3D: Web PDF.js files excluded from generated Android package."); }
        }
    }
}
#endif
