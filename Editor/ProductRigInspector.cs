using UnityEditor;
using UnityEngine;

namespace Liberty3D.ProductViewer.Editor
{
    [CustomEditor(typeof(ProductRig))]
    public class ProductRigInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var rig = (ProductRig)target;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(rig)))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Dans le Prefab Mode : affiche l'eclatement, deplace les racines ProductPart avec les outils Unity, puis enregistre la pose eclatee. Les secondaires restent ici visibles ; leur fondu est applique en Play.", MessageType.Info);
                if (GUILayout.Button("Actualiser les listes de composants")) { Undo.RecordObject(rig, "Collect product components"); rig.Collect(); EditorUtility.SetDirty(rig); }
                if (rig.parts == null || rig.parts.Length == 0) return;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Afficher assemble")) Change(rig, () => rig.PreviewAssembled(), false);
                    if (GUILayout.Button("Afficher eclate")) Change(rig, () => rig.PreviewExploded(), false);
                }
                if (GUILayout.Button("Enregistrer les positions actuelles comme ECLATEES"))
                    Change(rig, () => { foreach (var p in rig.parts) if (p) p.CaptureExploded(); }, true);
                if (GUILayout.Button("Enregistrer les positions actuelles comme ASSEMBLEES"))
                    Change(rig, () => { foreach (var p in rig.parts) if (p) p.CaptureAssembled(); }, true);
                EditorGUILayout.HelpBox("Une fiche = un ProductPart. Les objets ProductAccessory n'ont ni etiquette ni entree dans le menu. Ne les imbrique pas dans les ProductPart.", MessageType.None);
            }
        }

        static void Change(ProductRig rig, System.Action action, bool data)
        {
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Product pose");
            foreach (var part in rig.parts) if (part) Undo.RecordObject(data ? (Object)part : part.transform, "Product pose");
            action();
            foreach (var part in rig.parts) if (part)
            {
                Object changed = data ? (Object)part : part.transform;
                EditorUtility.SetDirty(changed);
                if (PrefabUtility.IsPartOfPrefabInstance(changed)) PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
            }
            Undo.CollapseUndoOperations(group); SceneView.RepaintAll();
        }
    }
}
