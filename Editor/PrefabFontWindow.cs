using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Liberty3D.ProductViewer.Editor
{
    public class PrefabFontWindow : EditorWindow
    {
        [SerializeField] DefaultAsset folder;
        [SerializeField] Font replacement;
        [SerializeField] bool changeFont = true, changeColor, targetedColor = true, preserveAlpha = true;
        [SerializeField] Color sourceColor = Color.white, destinationColor = new Color(.13f, .2f, .26f, 1);
        [SerializeField] float colorTolerance = .005f;
        Color[] detectedColors = Array.Empty<Color>();
        int[] detectedCounts = Array.Empty<int>();
        Vector2 scroll;
        string report = "";

        [MenuItem("Tools/Liberty 3D/UI/Remplacer la police des prefabs")]
        public static void Open() { GetWindow<PrefabFontWindow>("Police et couleurs"); }
        void OnEnable()
        {
            if (!folder) folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Liberty3D_UI");
        }
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Modifie uniquement la police et/ou la couleur des composants UI Text, y compris inactifs. Conserve Bold/Italic, taille et mise en page. Sous-dossiers inclus. TextMeshPro non modifie.", MessageType.Info);
            folder = (DefaultAsset)EditorGUILayout.ObjectField("Dossier de prefabs", folder, typeof(DefaultAsset), false);
            changeFont = EditorGUILayout.ToggleLeft("Changer la police", changeFont);
            using (new EditorGUI.DisabledScope(!changeFont))
                replacement = (Font)EditorGUILayout.ObjectField("Nouvelle police", replacement, typeof(Font), false);
            changeColor = EditorGUILayout.ToggleLeft("Changer la couleur des textes", changeColor);
            if (changeColor)
            {
                targetedColor = EditorGUILayout.Toggle("Limiter a une couleur", targetedColor);
                if (targetedColor)
                {
                    sourceColor = EditorGUILayout.ColorField("Couleur a rechercher", sourceColor);
                    colorTolerance = EditorGUILayout.Slider(new GUIContent("Tolerance RGB", "Ecart maximal par canal, de 0 a 1. 0 = correspondance exacte. Alpha ignore pour la recherche."), colorTolerance, 0, .1f);
                }
                destinationColor = EditorGUILayout.ColorField("Nouvelle couleur", destinationColor);
                preserveAlpha = EditorGUILayout.Toggle("Conserver l'opacite", preserveAlpha);
                EditorGUILayout.HelpBox(targetedColor ? "Seuls les textes proches de la couleur recherchee sont recolores. Utilise Relever les couleurs pour choisir une couleur existante." : "Tous les textes du dossier seront recolores, y compris ceux des boutons.", MessageType.None);
            }
            string path = folder ? AssetDatabase.GetAssetPath(folder) : "";
            bool valid = (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)) && AssetDatabase.IsValidFolder(path);
            bool editing = PrefabStageUtility.GetCurrentPrefabStage() != null;
            if (editing) EditorGUILayout.HelpBox("Enregistre et ferme le Prefab Mode avant d'appliquer, pour eviter un conflit avec les modifications ouvertes.", MessageType.Warning);
            if (changeColor)
            {
                using (new EditorGUI.DisabledScope(!valid || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Relever les couleurs existantes")) ReadColors(path);
                for (int i = 0; i < detectedColors.Length; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ColorField(detectedColors[i], GUILayout.Width(50));
                        if (GUILayout.Button("Choisir #" + ColorUtility.ToHtmlStringRGBA(detectedColors[i]) + " (" + detectedCounts[i] + " textes)"))
                        { sourceColor = detectedColors[i]; targetedColor = true; }
                    }
                }
            }
            using (new EditorGUI.DisabledScope(!valid || (!changeFont && !changeColor) || (changeFont && !replacement) || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Analyser les prefabs")) Run(path, false);
                using (new EditorGUI.DisabledScope(editing))
                    if (GUILayout.Button("Appliquer les changements (avec sauvegarde)")) Run(path, true);
            }
            EditorGUILayout.HelpBox("Pour conserver un rendu normal / gras / italique, choisis de preference la variante Regular. Les styles sont conserves, mais leur dessin depend de la nouvelle police. Les prefabs lies a d'autres fabricants dans ce dossier seront aussi concernes.", MessageType.None);
            EditorGUILayout.LabelField(report, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }
        void Run(string folderPath, bool apply)
        {
            try
            {
                var paths = AssetDatabase.FindAssets("t:Prefab", new[] {folderPath})
                    .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p).ToArray();
                var candidates = paths.Where(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)
                    .GetComponentsInChildren<Text>(true).Any(NeedsChange)).ToArray();
                string backup = "";
                if (apply && candidates.Length > 0)
                {
                    // Back up ALL candidates before saving any asset, including inherited/variant content.
                    backup = Path.Combine("Liberty3D_Backups", "Fonts-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
                    foreach (string path in candidates)
                    {
                        string saved = Path.Combine(backup, path);
                        Directory.CreateDirectory(Path.GetDirectoryName(saved));
                        File.Copy(path, saved);
                        if (File.Exists(path + ".meta")) File.Copy(path + ".meta", saved + ".meta");
                    }
                }
                int total = 0;
                report = apply ? "Remplacement termine\n" : "Apercu des changements\n";
                foreach (string path in candidates)
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        int count = root.GetComponentsInChildren<Text>(true).Count(NeedsChange);
                        if (apply && count > 0)
                        {
                            if (changeFont) ReplaceFonts(root, replacement);
                            if (changeColor) ReplaceColors(root, sourceColor, destinationColor, targetedColor, colorTolerance, preserveAlpha);
                            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                            if (!success) throw new IOException("Impossible d'enregistrer : " + path);
                        }
                        total += count;
                        if (count > 0) report += count + " texte(s) : " + path + "\n";
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                report += "\n" + total + " texte(s) " + (apply ? "remplace(s)." : "a remplacer.");
                if (!string.IsNullOrEmpty(backup)) report += "\nSauvegarde : " + Path.GetFullPath(backup);
                if (apply) Debug.Log("[Liberty3D] " + report);
            }
            catch (Exception exception) { report += "\nOperation interrompue : " + exception.Message + "\nLes originaux sauvegardes sont dans Liberty3D_Backups/Fonts-..."; Debug.LogException(exception); }
        }

        string[] PrefabPaths(string folderPath) => AssetDatabase.FindAssets("t:Prefab", new[] {folderPath})
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).ToArray();

        void ReadColors(string folderPath)
        {
            var groups = PrefabPaths(folderPath).SelectMany(p => AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponentsInChildren<Text>(true))
                .GroupBy(t => t.color).OrderByDescending(g => g.Count()).ToArray();
            detectedColors = groups.Select(g => g.Key).ToArray();
            detectedCounts = groups.Select(g => g.Count()).ToArray();
        }

        bool NeedsChange(Text text) => (changeFont && text.font != replacement) ||
            (changeColor && (!targetedColor || MatchesColor(text.color, sourceColor, colorTolerance)) &&
             !text.color.Equals(ResultColor(text.color, destinationColor, preserveAlpha)));

        public static bool MatchesColor(Color value, Color source, float tolerance)
        {
            tolerance = Mathf.Max(0, tolerance);
            return Mathf.Abs(value.r - source.r) <= tolerance && Mathf.Abs(value.g - source.g) <= tolerance && Mathf.Abs(value.b - source.b) <= tolerance;
        }
        static Color ResultColor(Color original, Color replacement, bool keepAlpha)
        { if (keepAlpha) replacement.a = original.a; return replacement; }

        public static int ReplaceColors(GameObject root, Color source, Color replacement, bool targeted = true, float tolerance = .005f, bool keepAlpha = true)
        {
            if (!root) throw new ArgumentException("Le prefab est requis.");
            int count = 0;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (targeted && !MatchesColor(text.color, source, tolerance)) continue;
                var result = ResultColor(text.color, replacement, keepAlpha);
                if (text.color.Equals(result)) continue;
                text.color = result; count++;
                EditorUtility.SetDirty(text);
                if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
            return count;
        }

        public static int ReplaceFonts(GameObject root, Font font)
        {
            if (!root || !font) throw new ArgumentException("Le prefab et la police sont requis.");
            int count = 0;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.font == font) continue;
                text.font = font; count++;
                EditorUtility.SetDirty(text);
                if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
            return count;
        }
    }
}
