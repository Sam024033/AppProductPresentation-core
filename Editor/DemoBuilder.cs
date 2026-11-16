using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

namespace Liberty3D.ProductViewer.Editor
{
    [InitializeOnLoad]
    public static class DemoBuilder
    {
        public const string Root = "Assets/Liberty3D_Demo";
        const string Request = "Liberty3D-setup.request";
        static DemoBuilder() { EditorApplication.delayCall += Bootstrap; EditorApplication.delayCall += RepairDemoThumbnail; }
        [MenuItem("Tools/Liberty 3D/Repair demo thumbnail")]
        public static void RepairDemoThumbnail()
        {
            var product = AssetDatabase.LoadAssetAtPath<ProductDefinition>(Root + "/Product_Module07.asset");
            if (!product || product.thumbnail) return;
            string path = Root + "/Media/Module07.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) return;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport();
            var camera = Camera.main;
            product.thumbnail = camera && product.prefab ? CreateThumbnail(product, camera) : AssetDatabase.LoadAssetAtPath<Sprite>(path);
            product.information.images = new[] {product.thumbnail};
            EditorUtility.SetDirty(product); AssetDatabase.SaveAssets();
        }
        static void Bootstrap()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Bootstrap; return; }
            if (SceneManager.GetActiveScene().isDirty) { Debug.LogWarning("[Liberty3D] Save the current scene, then use Tools > Liberty 3D > Create demo."); return; }
            try { CreateDemo(); File.Delete(Request); }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText("Liberty3D-setup-error.txt", e.ToString()); }
        }

        [MenuItem("Tools/Liberty 3D/Create demo %#l")]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlaying) return;
            if (AssetDatabase.LoadAssetAtPath<ViewerCatalog>(Root + "/Catalog.asset"))
            {
                Debug.Log("[Liberty3D] Demo already exists; existing data has been preserved.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Localization");
            Directory.CreateDirectory(Root + "/Media"); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = ScriptableObject.CreateInstance<ViewerCatalog>();
            AssetDatabase.CreateAsset(catalog, Root + "/Catalog.asset");
            var definition = ScriptableObject.CreateInstance<ProductDefinition>();
            definition.productId = "demo-module-07";
            definition.information.title = new ProductText("Module technique 07", "Technical module 07");
            definition.information.description = new ProductText(
                "Un assemblage de démonstration composé de sept éléments indépendants.\n\nExplorez sa structure, ouvrez la vue éclatée puis sélectionnez un composant pour découvrir sa fiche.\n\nMATÉRIAUX\nAluminium · Acier · Polymère\n\nCette géométrie simplifiée sert à valider les interactions. Elle sera remplacée par votre produit.",
                "A demonstration assembly made of seven independent components.\n\nExplore its structure, open the exploded view, then select a component to discover its information.\n\nMATERIALS\nAluminium · Steel · Polymer\n\nThis simplified geometry validates the interactions. Replace it with your own product.");
            definition.guidedSteps = new[] {
                new GuidedStep {title = new ProductText("Le produit", "The product"), description = new ProductText("Faites tourner le module pour découvrir ses différentes faces.", "Rotate the module to discover its different sides.")},
                new GuidedStep {title = new ProductText("Sa structure", "Its structure"), description = new ProductText("Sept composants, reliés à leurs étiquettes. Chaque élément est sélectionnable.", "Seven components linked to their labels. Each element can be selected."), exploded = true},
                new GuidedStep {title = new ProductText("Le cœur du système", "The heart of the system"), description = new ProductText("L’axe est isolé pour faciliter son inspection.", "The shaft is isolated for closer inspection."), exploded = true, partId = "shaft"}
            };
            AssetDatabase.CreateAsset(definition, Root + "/Product_Module07.asset");
            var root = new GameObject("Module07"); var rig = root.AddComponent<ProductRig>();
            var steel = Material("Steel", new Color(.56f, .65f, .72f), .7f);
            var dark = Material("Housing", new Color(.15f, .21f, .28f), .65f);
            var accent = Material("Teal", new Color(.08f, .64f, .58f), .45f);
            var orange = Material("Copper", new Color(.88f, .47f, .19f), .65f);
            rig.parts = new[] {
                Part(root.transform, "housing", "Caisson", "Housing", PrimitiveType.Cube, new Vector3(0,-.2f,0), new Vector3(2.3f,1.65f,1.8f), new Vector3(-3,-.4f,0), dark),
                Part(root.transform, "cover", "Couvercle", "Cover", PrimitiveType.Cube, new Vector3(0,.72f,0), new Vector3(2.45f,.18f,1.95f), new Vector3(-1,2.4f,0), steel),
                Part(root.transform, "shaft", "Axe central", "Central shaft", PrimitiveType.Cylinder, new Vector3(0,0,0), new Vector3(.24f,.8f,.24f), new Vector3(0,.15f,0), steel),
                Part(root.transform, "rotor", "Rotor", "Rotor", PrimitiveType.Cylinder, new Vector3(0,.28f,0), new Vector3(.85f,.13f,.85f), new Vector3(1.5f,1.5f,0), accent),
                Part(root.transform, "seal", "Joint interne", "Internal seal", PrimitiveType.Cylinder, new Vector3(0,-.55f,0), new Vector3(.68f,.07f,.68f), new Vector3(.8f,-1.6f,0), orange),
                Part(root.transform, "connector", "Raccord", "Connector", PrimitiveType.Cylinder, new Vector3(1.35f,-.25f,0), new Vector3(.46f,.3f,.46f), new Vector3(3,.1f,0), accent, new Vector3(0,0,90)),
                Part(root.transform, "sensor", "Capteur", "Sensor", PrimitiveType.Cube, new Vector3(-1.3f,.35f,0), new Vector3(.35f,.55f,.55f), new Vector3(-2,1.2f,-1.3f), orange)
            };
            rig.parts[0].labelOffset = new Vector2(-190, -70);
            rig.parts[1].labelOffset = new Vector2(-160, 65);
            rig.parts[2].labelOffset = new Vector2(40, -80);
            rig.parts[3].labelOffset = new Vector2(65, 50);
            rig.parts[4].labelOffset = new Vector2(50, -55);
            rig.parts[5].labelOffset = new Vector2(90, -65);
            rig.parts[6].labelOffset = new Vector2(-150, 35);
            SetupLocalization(catalog, definition, rig);
            definition.prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Module07.prefab").GetComponent<ProductRig>();
            UnityEngine.Object.DestroyImmediate(root);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera"; var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 38; camera.transform.position = new Vector3(0, 0, -9); camera.backgroundColor = catalog.background;
            camera.clearFlags = CameraClearFlags.SolidColor;
            Light("Key light", new Vector3(35, -30, 0), Color.white, 2.5f);
            Light("Rim light", new Vector3(15, 145, 0), new Color(.45f, .78f, 1), 1.6f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.5f, .55f, .62f);
            definition.thumbnail = CreateThumbnail(definition, camera);
            definition.information.images = new[] { definition.thumbnail };
            // The optional bundled PDF is real, offline, and shared across both locales.
            definition.information.sharedPdf = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Media/Demo_Document.pdf.bytes");
            catalog.products = new[] { definition };
            var viewer = new GameObject("Product Viewer").AddComponent<ProductViewer>(); viewer.catalog = catalog; viewer.viewCamera = camera;
            EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, Root + "/ProductViewer_Demo.unity");
            var demoPath = Root + "/ProductViewer_Demo.unity";
            EditorBuildSettings.scenes = new[] {new EditorBuildSettingsScene(demoPath, true)}.Concat(EditorBuildSettings.scenes.Where(s => s.path != demoPath)).ToArray();
            Selection.activeObject = viewer.gameObject;
            File.WriteAllText("Liberty3D-setup-result.txt", "Demo created successfully. Scene: " + demoPath + "\n" + DateTime.UtcNow.ToString("O"));
            Debug.Log("[Liberty3D] Demo ready. Press Play. Tools > Liberty 3D contains authoring and validation tools.");
            ViewerValidator.Validate();
        }
        static Material Material(string name, Color color, float metallic)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name = name;
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", .45f);
            AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat"); return material;
        }
        static ProductPart Part(Transform parent, string id, string fr, string en, PrimitiveType primitive, Vector3 position, Vector3 size, Vector3 exploded, Material material, Vector3 euler = default)
        {
            var go = new GameObject(id); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localEulerAngles = euler;
            var part = go.AddComponent<ProductPart>(); part.partId = id;
            part.information.title = new ProductText(fr, en);
            part.information.description = new ProductText("COMPOSANT DE DÉMONSTRATION\n\n" + fr + " du module technique.\n\nCette fiche est personnalisable dans Unity : description, caractéristiques, photos et document PDF.\n\nRéférence : DEMO-" + id.ToUpperInvariant(), "DEMONSTRATION COMPONENT\n\n" + en + " of the technical module.\n\nCustomize this information in Unity: description, specifications, photographs and PDF document.\n\nReference: DEMO-" + id.ToUpperInvariant());
            part.assembledPosition = position; part.assembledEuler = euler; part.explodedPosition = exploded; part.explodedEuler = euler;
            var mesh = GameObject.CreatePrimitive(primitive); mesh.name = "Geometry · replace with CAD"; mesh.transform.SetParent(go.transform, false);
            mesh.transform.localScale = size; mesh.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }
        static void Light(string name, Vector3 euler, Color color, float intensity)
        {
            var go = new GameObject(name); go.transform.rotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>(); light.type = LightType.Directional; light.color = color; light.intensity = intensity;
        }
        static Sprite CreateThumbnail(ProductDefinition definition, Camera camera)
        {
            var model = UnityEngine.Object.Instantiate(definition.prefab);
            model.transform.rotation = Quaternion.Euler(18, -30, 0);
            var render = RenderTexture.GetTemporary(640, 420, 24);
            var previous = RenderTexture.active; var target = camera.targetTexture; float aspect = camera.aspect;
            camera.targetTexture = render; camera.aspect = 640f / 420;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = render });
            else camera.Render();
            RenderTexture.active = render;
            var texture = new Texture2D(640, 420, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 640, 420), 0, 0); texture.Apply();
            string path = Root + "/Media/Module07.png"; File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = target; camera.aspect = aspect; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render);
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(model.gameObject);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static void SetupLocalization(ViewerCatalog catalog, ProductDefinition product, ProductRig rig)
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, Root + "/Localization/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }
            foreach (string code in new[] { "fr", "en" })
            {
                if (LocalizationEditorSettings.GetLocale(code) != null) continue;
                var locale = Locale.CreateLocale(code); AssetDatabase.CreateAsset(locale, Root + "/Localization/" + code + ".asset"); LocalizationEditorSettings.AddLocale(locale);
            }
            var locales = new[] { LocalizationEditorSettings.GetLocale("fr"), LocalizationEditorSettings.GetLocale("en") };
            var collection = LocalizationEditorSettings.CreateStringTableCollection("LibertyDemo", Root + "/Localization", locales);
            collection.SetPreloadTableFlag(true);
            AddText(collection, "brand.welcome", catalog.welcome); AddText(collection, "brand.legal", catalog.legal);
            AddText(collection, "product.title", product.information.title); AddText(collection, "product.description", product.information.description);
            foreach (var part in rig.parts) { AddText(collection, part.partId + ".title", part.information.title); AddText(collection, part.partId + ".description", part.information.description); }
            for (int i = 0; i < product.guidedSteps.Length; i++) { AddText(collection, "guide." + i + ".title", product.guidedSteps[i].title); AddText(collection, "guide." + i + ".description", product.guidedSteps[i].description); }
            EditorUtility.SetDirty(settings);
        }
        static void AddText(StringTableCollection collection, string key, ProductText text)
        {
            var fr = collection.GetTable("fr") as StringTable; var en = collection.GetTable("en") as StringTable;
            fr.AddEntry(key, text.french); en.AddEntry(key, text.english);
            text.localized = new LocalizedString(collection.TableCollectionName, key);
            EditorUtility.SetDirty(fr); EditorUtility.SetDirty(en); EditorUtility.SetDirty(collection.SharedData);
        }

        [MenuItem("Tools/Liberty 3D/Suggest explosion for selected rig")]
        static void SuggestExplosion()
        {
            var rig = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<ProductRig>() : null;
            if (!rig) { Debug.LogWarning("Select a ProductRig in the scene or prefab editor."); return; }
            Undo.RecordObjects(rig.parts, "Suggest explosion"); rig.SuggestExplosion();
            foreach (var part in rig.parts) EditorUtility.SetDirty(part);
            Debug.Log("[Liberty3D] Suggested poses stored. Use Preview exploded pose, adjust, then capture poses.");
        }
        [MenuItem("Tools/Liberty 3D/Import PDF as offline document")]
        static void ImportPdf()
        {
            string source = EditorUtility.OpenFilePanel("Import PDF", "", "pdf"); if (string.IsNullOrEmpty(source)) return;
            string destination = EditorUtility.SaveFilePanelInProject("Save offline PDF", Path.GetFileName(source), "bytes", "Choose the manufacturer's media folder.");
            if (string.IsNullOrEmpty(destination)) return;
            File.Copy(source, destination, true); AssetDatabase.ImportAsset(destination); Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(destination);
        }
    }
}
