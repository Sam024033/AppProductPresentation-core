using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Liberty3D.ProductViewer.Editor
{
    public class ProductSetupWindow : EditorWindow
    {
        [SerializeField] GameObject source;
        [SerializeField] ProductDefinition definition;
        [SerializeField] List<Transform> parts = new List<Transform>();
        [SerializeField] List<Transform> accessories = new List<Transform>();
        [SerializeField] bool preciseColliders = true, proposeExplosion = true;
        Vector2 scroll;

        [MenuItem("Tools/Liberty 3D/Configurer un modele")]
        public static void Open() { GetWindow<ProductSetupWindow>("Configurer un modele"); }

        void OnGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.HelpBox("Ouvre ton prefab source en Prefab Mode, puis depose ses objets dans les listes. Une NOUVELLE copie configuree sera creee ; le modele source reste intact.", MessageType.Info);
                var next = (GameObject)EditorGUILayout.ObjectField("Modele source", source, typeof(GameObject), true);
                if (next != source) { source = next; parts.Clear(); accessories.Clear(); }
                if (GUILayout.Button("Utiliser le prefab actuellement ouvert"))
                {
                    var stage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null) { source = stage.prefabContentsRoot; parts.Clear(); accessories.Clear(); }
                    else ShowNotification(new GUIContent("Ouvre d'abord un prefab par double-clic."));
                }
                definition = (ProductDefinition)EditorGUILayout.ObjectField("Fiche produit (optionnel)", definition, typeof(ProductDefinition), false);
                EditorGUILayout.Space();
                DrawList("Pieces a explorer", parts);
                EditorGUILayout.HelpBox("Un objet choisi = une fiche et une entree dans le menu. Ses enfants suivent la piece. Ne choisis pas a la fois un parent et son enfant.", MessageType.None);
                DrawList("Secondaires a exclure des pieces (optionnel)", accessories);
                EditorGUILayout.HelpBox("Vis/ecrous inclus dans une piece : ajoute-les ici pour les en separer. Tout le reste du modele non choisi est automatiquement secondaire : fondu en vue eclatee/detail, retour en vue reconstituee.", MessageType.None);
                preciseColliders = EditorGUILayout.Toggle(new GUIContent("Colliders precis (mesh)", "Decoche pour des boites plus legeres, mais moins precises autour des trous."), preciseColliders);
                proposeExplosion = EditorGUILayout.Toggle("Proposer un eclatement", proposeExplosion);
                EditorGUILayout.HelpBox("Assemblages rigides uniquement. Applique les echelles non uniformes/negatives dans le logiciel 3D. Verifie les materiaux URP/Lit pour le fondu. Les positions proposees restent a ajuster visuellement.", MessageType.None);
                string error = ProductSetupBuilder.Validate(source, parts, accessories);
                if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
                using (new EditorGUI.DisabledScope(error != null))
                    if (GUILayout.Button("Creer le prefab configure...", GUILayout.Height(34))) Generate();
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawList(string title, List<Transform> list)
        {
            EditorGUILayout.LabelField(title + " (" + list.Count + ")", EditorStyles.boldLabel);
            for (int i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    list[i] = (Transform)EditorGUILayout.ObjectField(list[i], typeof(Transform), true);
                    if (GUILayout.Button("X", GUILayout.Width(24))) { list.RemoveAt(i); i--; }
                }
            }
            var drop = GUILayoutUtility.GetRect(0, 38, GUILayout.ExpandWidth(true));
            GUI.Box(drop, "Glisse plusieurs objets ici");
            var evt = Event.current;
            if (drop.Contains(evt.mousePosition) && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        var t = obj is GameObject go ? go.transform : obj as Transform;
                        if (t && !list.Contains(t)) list.Add(t);
                    }
                }
                evt.Use();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ajouter la selection")) foreach (var t in Selection.transforms) if (!list.Contains(t)) list.Add(t);
                if (GUILayout.Button("Vider", GUILayout.Width(60))) list.Clear();
            }
        }

        void Generate()
        {
            string path = EditorUtility.SaveFilePanelInProject("Nouveau prefab configure", source.name + "_Viewer", "prefab", "Choisis un nouveau fichier.");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var rig = ProductSetupBuilder.Build(source, parts, accessories, path, preciseColliders, proposeExplosion);
                if (definition)
                {
                    Undo.RecordObject(definition, "Assign configured product prefab"); definition.prefab = rig;
                    EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
                }
                EditorGUIUtility.PingObject(rig.gameObject);
                EditorUtility.DisplayDialog("Prefab configure", "Prefab cree : " + path + "\n\nOuvre cette copie. Sur ProductRig, utilise les boutons de poses pour regler l'eclatement hors Play. Complete ensuite Information sur chaque ProductPart." +
                    (definition ? "\n\nLa fiche produit reference maintenant cette copie." : "\n\nGlisse cette copie dans le champ Prefab de ta fiche ProductDefinition."), "OK");
            }
            catch (System.Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("Configuration impossible", exception.Message, "OK"); }
        }
    }
}
