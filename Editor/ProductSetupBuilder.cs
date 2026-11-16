using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Liberty3D.ProductViewer.Editor
{
    public static class ProductSetupBuilder
    {
        public static string Validate(GameObject source, IList<Transform> parts, IList<Transform> accessories)
        {
            if (!source) return "Choisis le modele source.";
            if (source.GetComponentInChildren<ProductRig>(true)) return "Ce modele possede deja un ProductRig. Edite ses pieces et poses dans le prefab configure, ou utilise le modele source.";
            if (parts.Count == 0) return "Ajoute au moins une piece a explorer.";
            var all = parts.Concat(accessories).ToArray();
            if (all.Any(t => !t || t == source.transform || !t.IsChildOf(source.transform))) return "Chaque element doit etre un enfant du modele source (pas sa racine).";
            if (all.Distinct().Count() != all.Length) return "Un objet figure plusieurs fois dans les listes.";
            if (parts.Any(p => parts.Any(q => p != q && p.IsChildOf(q)))) return "Deux pieces explorables ne peuvent pas etre imbriquees. Choisis le parent ou ses enfants.";
            if (accessories.Any(a => parts.Any(p => p.IsChildOf(a)))) return "Un groupe secondaire ne peut pas contenir une piece explorable.";
            if (accessories.Any(a => accessories.Any(b => a != b && a.IsChildOf(b)))) return "Deux groupes secondaires sont imbriques. Garde seulement leur parent.";
            if (parts.Any(p => !p.GetComponentInChildren<MeshRenderer>(true))) return "Chaque piece doit contenir au moins un MeshRenderer.";
            if (source.GetComponentInChildren<SkinnedMeshRenderer>(true)) return "Cet outil prepare des assemblages rigides (MeshRenderer), pas des modeles skinnes.";
            if (source.GetComponentInChildren<LODGroup>(true)) return "Retire les LODGroup de ta copie de preparation avant de generer : leurs references doivent etre reconstruites par piece.";
            if (source.GetComponentInChildren<Animator>(true) || source.GetComponentInChildren<Animation>(true)) return "Utilise une copie sans Animator/Animation : les positions des pieces seront pilotees par le viewer.";
            foreach (var t in source.GetComponentsInChildren<Transform>(true))
            {
                var s = t.localScale;
                if (s.x <= 0 || s.y <= 0 || s.z <= 0 || !Mathf.Approximately(s.x, s.y) || !Mathf.Approximately(s.y, s.z))
                    return "Applique les echelles negatives/non uniformes dans le logiciel 3D avant la preparation : " + t.name;
            }
            return null;
        }

        public static ProductRig Build(GameObject source, IList<Transform> selected, IList<Transform> secondary,
            string destination, bool meshColliders, bool suggestExplosion)
        {
            string error = Validate(source, selected, secondary);
            if (error != null) throw new ArgumentException(error);
            if (!destination.StartsWith("Assets/", StringComparison.Ordinal) || !destination.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Enregistre un prefab dans Assets.");
            if (AssetDatabase.LoadMainAssetAtPath(destination)) throw new ArgumentException("Choisis un nouveau fichier : le generateur ne remplace pas les prefabs existants.");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(destination));
                SceneManager.MoveGameObjectToScene(root, scene);
                var rig = root.AddComponent<ProductRig>();
                var clone = Object.Instantiate(source, root.transform);
                clone.name = source.name;
                if (PrefabUtility.IsPartOfPrefabInstance(clone))
                    PrefabUtility.UnpackPrefabInstance(clone, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = source.transform.localRotation;
                clone.transform.localScale = source.transform.localScale;
                var originals = source.GetComponentsInChildren<Transform>(true);
                var copies = clone.GetComponentsInChildren<Transform>(true);
                var map = new Dictionary<Transform, Transform>();
                for (int i = 0; i < originals.Length; i++) map.Add(originals[i], copies[i]);
                // Strip only viewer authoring and physics components from the disposable copy.
                foreach (var p in clone.GetComponentsInChildren<ProductPart>(true)) Object.DestroyImmediate(p);
                foreach (var a in clone.GetComponentsInChildren<ProductAccessory>(true)) Object.DestroyImmediate(a);
                foreach (var c in clone.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
                foreach (var t in copies) { t.gameObject.isStatic = false; t.gameObject.layer = 0; }
                var ancillary = new GameObject("Secondary_AssembledOnly"); ancillary.transform.SetParent(root.transform, false);
                // Explicit exclusions can be children of a selected part (bolts inside a casing group).
                foreach (var t in secondary) map[t].SetParent(ancillary.transform, true);
                var built = new List<ProductPart>();
                foreach (var original in selected)
                {
                    var geometry = map[original];
                    var renderers = geometry.GetComponentsInChildren<MeshRenderer>(true);
                    if (renderers.Length == 0) throw new ArgumentException("La piece ne contient plus de geometrie apres exclusion : " + original.name);
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    var go = new GameObject(original.name); go.transform.SetParent(root.transform, false);
                    go.transform.position = bounds.center; geometry.SetParent(go.transform, true);
                    var part = go.AddComponent<ProductPart>();
                    part.partId = "part-" + (built.Count + 1).ToString("00");
                    part.information.title = new ProductText(original.name, original.name);
                    part.labelAnchor = Vector3.zero;
                    part.labelOffset = new Vector2(built.Count % 2 == 0 ? 110 : -230, 40);
                    if (meshColliders)
                    {
                        foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
                            if (filter.sharedMesh) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    }
                    else
                    {
                        var box = go.AddComponent<BoxCollider>(); box.center = Vector3.zero; box.size = bounds.size;
                    }
                    if (!go.GetComponentInChildren<Collider>(true)) throw new ArgumentException("Aucun mesh pour creer un collider : " + original.name);
                    built.Add(part);
                }
                // Every remaining mesh is secondary, including objects not explicitly listed.
                clone.transform.SetParent(ancillary.transform, true);
                foreach (var c in ancillary.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                if (ancillary.GetComponentInChildren<Renderer>(true)) ancillary.AddComponent<ProductAccessory>();
                else Object.DestroyImmediate(ancillary);
                rig.Collect();
                // Centre the complete assembly, not only the individually selectable pieces.
                var full = root.GetComponentsInChildren<Renderer>(true);
                var total = full[0].bounds; foreach (var r in full) total.Encapsulate(r.bounds);
                foreach (Transform child in root.transform) child.position -= total.center;
                foreach (var part in rig.parts) { part.CaptureAssembled(); part.CaptureExploded(); }
                if (suggestExplosion) rig.SuggestExplosion(Mathf.Max(.02f, total.size.magnitude * .35f));
                var asset = PrefabUtility.SaveAsPrefabAsset(root, destination);
                if (!asset) throw new InvalidOperationException("Unity n'a pas pu enregistrer le prefab.");
                return asset.GetComponent<ProductRig>();
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
