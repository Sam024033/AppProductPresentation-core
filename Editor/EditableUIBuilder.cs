using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Liberty3D.ProductViewer.Editor
{
    [InitializeOnLoad]
    public static class EditableUIBuilder
    {
        public const string Root = "Assets/Liberty3D_UI";
        const string Request = "Liberty3D-ui-prefabs.request";
        static GameObject buttonPrefab, framePrefab;
        static EditableUIBuilder() { EditorApplication.update += CheckRequest; }
        static void CheckRequest()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(Request);
            try { Build(); } catch (Exception error) { Debug.LogException(error); File.WriteAllText("Liberty3D-ui-error.txt", error.ToString()); }
        }
        [MenuItem("Tools/Liberty 3D/UI/Create editable prefabs (preserve existing)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode before creating UI assets."); return; }
            Directory.CreateDirectory(Root + "/Shared"); Directory.CreateDirectory(Root + "/Screens"); AssetDatabase.Refresh();
            buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Shared/ActionButton.prefab") ?? MakeButton();
            framePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Shared/PanelFrame.prefab") ?? MakeFrame();
            var card = Get<UIProductCard>("Shared/ProductCard") ?? MakeCard();
            var label = Get<UIPartLabelView>("Shared/PartLabel") ?? MakeLabel();
            var information = Get<UIInformationView>("Shared/InformationPanel") ?? MakeInformation();
            var guide = Get<UIGuideView>("Shared/GuidedTourPanel") ?? MakeGuide();
            var welcome = Get<UIWelcomeView>("Screens/Welcome") ?? MakeWelcome();
            var catalog = Get<UICatalogView>("Screens/Catalog") ?? MakeCatalog(card);
            var product = Get<UIProductView>("Screens/Product") ?? MakeProduct(information, guide, label);
            UpgradePartsMenu();
            product = Get<UIProductView>("Screens/Product");
            var dialog = Get<UIDialogView>("Screens/ModalWindow") ?? MakeDialog();
            var document = Get<UIDocumentView>("Screens/DocumentReader") ?? MakeDocument();
            var root = Get<ViewerUIRoot>("ViewerUI") ?? MakeRoot(welcome, catalog, product, dialog, document);
            foreach (var guid in AssetDatabase.FindAssets("t:ViewerCatalog"))
            {
                var settings = AssetDatabase.LoadAssetAtPath<ViewerCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (settings.uiPrefab) continue;
                settings.uiPrefab = root; EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
            CreatePreview(root);
            File.WriteAllText("Liberty3D-ui-result.txt", "Editable nested UI prefabs ready: " + Root + "\nExisting UI prefabs were preserved.\n");
            Debug.Log("[Liberty3D] Editable UI prefabs ready in " + Root + ". Open UI_Preview.unity or edit the prefabs directly.");
        }
        [MenuItem("Tools/Liberty 3D/UI/Open preview scene")]
        public static void OpenPreview()
        {
            if(EditorApplication.isPlaying) { Debug.LogWarning("Exit Play mode to edit the preview scene."); return; }
            if(!File.Exists(Root+"/UI_Preview.unity")) Build();
            if(File.Exists(Root+"/UI_Preview.unity") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(Root+"/UI_Preview.unity");
        }
        static T Get<T>(string path) where T : Component => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + path + ".prefab")?.GetComponent<T>();
        static T Save<T>(GameObject go, string path) where T : Component
        {
            var asset = PrefabUtility.SaveAsPrefabAsset(go, Root + "/" + path + ".prefab");
            UnityEngine.Object.DestroyImmediate(go); return asset.GetComponent<T>();
        }
        static RectTransform Rect(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(1440, 900); return rt;
        }
        static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = new Vector2(.5f, .5f); rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }
        static void Fill(RectTransform rt) => Stretch(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        static T Instance<T>(T prefab, Transform parent, string name = null) where T : Component
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
            if (name != null) go.name = name; return go.GetComponent<T>();
        }
        static UIActionButton Button(Transform parent, string name, string text, UnityEngine.Rect rect)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, parent); go.name = name;
            ViewerUI.Place((RectTransform)go.transform, rect); var view = go.GetComponent<UIActionButton>(); view.label.text = text; return view;
        }
        static RectTransform Frame(Transform parent, string name, UnityEngine.Rect rect)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(framePrefab, parent); go.name = name;
            ViewerUI.Place((RectTransform)go.transform, rect); return (RectTransform)go.transform;
        }
        static Text Text(Transform parent, string name, string value, UnityEngine.Rect rect, int size = 20)
        {
            var text = ViewerUI.Text(parent, value, rect, size); text.gameObject.name = name; return text;
        }
        static void Translate(Text text, string fr, string en)
        {
            var value = text.gameObject.AddComponent<UIStaticText>(); value.target = text; value.french = fr; value.english = en; text.text = fr;
        }
        static ScrollRect Scroll(Transform parent, string name, UnityEngine.Rect rect, out Text text)
        {
            var viewport = ViewerUI.Box(parent, name, rect, new Color(0, 0, 0, .001f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.viewport = viewport;
            text = Text(viewport, "Body", "Texte de démonstration. Modifiez la police, la couleur et les espacements dans ce prefab.", new UnityEngine.Rect(0, 0, rect.width, rect.height), 19);
            var rt = text.rectTransform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0, 1); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0, rect.height);
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = rt; return scroll;
        }
        static void Responsive(RectTransform rt, Vector2 narrowMin, Vector2 narrowMax, Vector2 narrowPivot, Vector2 narrowPosition, Vector2 narrowSize)
        {
            var rule = rt.gameObject.AddComponent<UIResponsiveRect>(); rule.wide = UIRectPose.Read(rt);
            rule.narrow = new UIRectPose {anchorMin=narrowMin, anchorMax=narrowMax, pivot=narrowPivot, position=narrowPosition, size=narrowSize};
        }
        static GameObject MakeButton()
        {
            var root = ViewerUI.Box(null, "ActionButton", new UnityEngine.Rect(0, 0, 190, 48), new Color(.11f, .16f, .21f), true);
            root.GetComponent<Image>().type = Image.Type.Sliced;
            var component = root.gameObject.AddComponent<UIActionButton>(); component.button = root.gameObject.AddComponent<Button>(); component.button.targetGraphic = root.GetComponent<Image>();
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.padding = new RectOffset(12, 12, 6, 6); layout.spacing = 10; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
            var icon = ViewerUI.Box(root, "Icon (assign a Sprite)", new UnityEngine.Rect(0, 0, 24, 24), Color.white); component.icon = icon.GetComponent<Image>(); component.icon.preserveAspect = true;
            var iconLayout = icon.gameObject.AddComponent<LayoutElement>(); iconLayout.preferredWidth = iconLayout.preferredHeight = 24; iconLayout.flexibleWidth = 0;
            component.label = Text(root, "Label", "Bouton", new UnityEngine.Rect(0, 0, 140, 36), 18); component.label.alignment = TextAnchor.MiddleCenter;
            var textLayout = component.label.gameObject.AddComponent<LayoutElement>(); textLayout.flexibleWidth = 1; textLayout.minWidth = 0;
            var element = root.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 48;
            var asset = Save<UIActionButton>(root.gameObject, "Shared/ActionButton"); return asset.gameObject;
        }
        static GameObject MakeFrame()
        {
            var root = ViewerUI.Box(null, "PanelFrame", new UnityEngine.Rect(0, 0, 330, 400), ViewerUI.Surface, true);
            root.GetComponent<Image>().type = Image.Type.Sliced;
            return Save<Image>(root.gameObject, "Shared/PanelFrame").gameObject;
        }
        static UIWelcomeView MakeWelcome()
        {
            var root = Rect("Welcome"); var view = root.gameObject.AddComponent<UIWelcomeView>();
            Text(root, "Eyebrow", "PRODUCT EXPLORER", new UnityEngine.Rect(36, 200, 420, 30), 16).color = new Color(.15f,.82f,.72f);
            view.heading = Text(root, "Welcome title (catalog)", "La précision, sous tous les angles.", new UnityEngine.Rect(36, 280, 1000, 180), 54);
            Stretch(view.heading.rectTransform, new Vector2(0,.45f), new Vector2(1,.7f), new Vector2(36,0), new Vector2(-36,0));
            var sub = Text(root, "Subtitle", "", new UnityEngine.Rect(36,560,1000,80),22); Translate(sub, "Explorez le produit. Comprenez chaque détail.", "Explore the product. Understand every detail.");
            Stretch(sub.rectTransform,new Vector2(0,.25f),new Vector2(1,.38f),new Vector2(36,0),new Vector2(-36,0));
            view.explore = Button(root, "Explore", "Toucher pour explorer", new UnityEngine.Rect(36,750,350,64));
            var rt=(RectTransform)view.explore.transform; rt.anchorMin=rt.anchorMax=Vector2.zero; rt.pivot=Vector2.zero; rt.anchoredPosition=new Vector2(36,85);
            return Save<UIWelcomeView>(root.gameObject,"Screens/Welcome");
        }
        static UIProductCard MakeCard()
        {
            var root=Frame(null,"ProductCard",new UnityEngine.Rect(0,0,340,320)); var view=root.gameObject.AddComponent<UIProductCard>();
            view.thumbnail=ViewerUI.Box(root,"Thumbnail",new UnityEngine.Rect(14,14,312,180),Color.white).GetComponent<Image>(); view.thumbnail.preserveAspect=true;
            Stretch(view.thumbnail.rectTransform,new Vector2(0,.38f),Vector2.one,new Vector2(14,0),new Vector2(-14,-14));
            view.title=Text(root,"Product title","Nom du produit",new UnityEngine.Rect(18,200,304,50),23);
            Stretch(view.title.rectTransform,new Vector2(0,.18f),new Vector2(1,.36f),new Vector2(18,0),new Vector2(-18,0));
            view.explore=Button(root,"Explore","Explorer",new UnityEngine.Rect(18,264,304,42));
            Stretch((RectTransform)view.explore.transform,Vector2.zero,new Vector2(1,0),new Vector2(18,14),new Vector2(-18,56));
            return Save<UIProductCard>(root.gameObject,"Shared/ProductCard");
        }
        static UICatalogView MakeCatalog(UIProductCard card)
        {
            var root=Rect("Catalog"); var view=root.gameObject.AddComponent<UICatalogView>(); view.cardPrefab=card;
            var title=Text(root,"Title","",new UnityEngine.Rect(28,100,1100,65),38); Translate(title,"Choisissez un produit","Choose a product");
            Stretch(title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(28,-170),new Vector2(-28,-100));
            var viewport=ViewerUI.Box(root,"Products viewport",new UnityEngine.Rect(24,220,1392,650),Color.clear,true); Stretch(viewport,Vector2.zero,Vector2.one,new Vector2(24,24),new Vector2(-24,-220));
            viewport.gameObject.AddComponent<RectMask2D>(); var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.viewport=viewport; scroll.movementType=ScrollRect.MovementType.Clamped;
            view.cards=Rect("Cards",viewport); Stretch(view.cards,new Vector2(0,1),Vector2.one,Vector2.zero,Vector2.zero); view.cards.pivot=new Vector2(0,1);
            view.grid=view.cards.gameObject.AddComponent<GridLayoutGroup>(); view.grid.cellSize=new Vector2(340,320); view.grid.spacing=new Vector2(18,18); view.grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount; view.grid.constraintCount=3;
            view.cards.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize; scroll.content=view.cards;
            view.previewCard=Instance(card,view.cards,"Preview card (runtime replaces with catalog)").gameObject;
            return Save<UICatalogView>(root.gameObject,"Screens/Catalog");
        }
        static UIInformationView MakeInformation()
        {
            var root=Frame(null,"Information panel",new UnityEngine.Rect(214,218,330,400)); var view=root.gameObject.AddComponent<UIInformationView>();
            var layout=root.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding=new RectOffset(20,20,20,20); layout.spacing=14; layout.childControlWidth=layout.childControlHeight=true; layout.childForceExpandHeight=false;
            root.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            view.title=Text(root,"Title","Produit / composant",new UnityEngine.Rect(0,0,290,52),24);
            var scroll=Scroll(root,"Description viewport",new UnityEngine.Rect(0,0,290,200),out view.description);
            view.descriptionViewport=scroll.gameObject.AddComponent<LayoutElement>(); view.descriptionViewport.preferredHeight=200;
            view.images=Button(root,"Images","Voir plus d’images",new UnityEngine.Rect(0,0,290,48));
            view.website=Button(root,"Website","Site web",new UnityEngine.Rect(0,0,290,48));
            view.document=Button(root,"Document","Plus d’informations · PDF",new UnityEngine.Rect(0,0,290,48));
            Responsive(root,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(18,86),new Vector2(-36,400));
            return Save<UIInformationView>(root.gameObject,"Shared/InformationPanel");
        }
        static UIGuideView MakeGuide()
        {
            var root=Frame(null,"Guided tour",new UnityEngine.Rect(0,0,470,200)); var view=root.gameObject.AddComponent<UIGuideView>();
            root.anchorMin=root.anchorMax=new Vector2(1,0); root.pivot=new Vector2(1,0); root.anchoredPosition=new Vector2(-18,86);
            view.title=Text(root,"Step title","1/3  Visite guidée",new UnityEngine.Rect(18,18,434,40),23);
            Stretch(view.title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(18,-58),new Vector2(-18,-18));
            var scroll=Scroll(root,"Step description",new UnityEngine.Rect(18,64,434,66),out view.description); Stretch((RectTransform)scroll.transform,Vector2.zero,Vector2.one,new Vector2(18,70),new Vector2(-18,-64));
            view.close=Button(root,"Close","Quitter",new UnityEngine.Rect(18,145,110,40));
            view.previous=Button(root,"Previous","←",new UnityEngine.Rect(300,145,60,40)); view.next=Button(root,"Next","→",new UnityEngine.Rect(380,145,72,40));
            Responsive(root,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(18,86),new Vector2(-36,200));
            Stretch((RectTransform)view.next.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-90,15),new Vector2(-18,55));
            Stretch((RectTransform)view.previous.transform,new Vector2(1,0),new Vector2(1,0),new Vector2(-170,15),new Vector2(-110,55));
            return Save<UIGuideView>(root.gameObject,"Shared/GuidedTourPanel");
        }
        static UIPartLabelView MakeLabel()
        {
            var root=Rect("PartLabel"); var view=root.gameObject.AddComponent<UIPartLabelView>(); view.group=root.gameObject.AddComponent<CanvasGroup>();
            view.leader=ViewerUI.Box(root,"Leader",new UnityEngine.Rect(500,330,100,2),new Color(.15f,.82f,.72f));
            view.badge=Frame(root,"Badge",new UnityEngine.Rect(600,308,196,44));
            view.marker=view.badge.gameObject.AddComponent<PartLabel>();
            view.title=Text(view.badge,"Label","Nom du composant",new UnityEngine.Rect(10,6,176,32),16); Fill(view.title.rectTransform); view.title.alignment=TextAnchor.MiddleCenter;
            return Save<UIPartLabelView>(root.gameObject,"Shared/PartLabel");
        }
        static UIProductView MakeProduct(UIInformationView information,UIGuideView guide,UIPartLabelView label)
        {
            var root=Rect("Product"); var view=root.gameObject.AddComponent<UIProductView>();
            view.labels=Rect("Labels",root); Fill(view.labels); view.labelPrefab=label; view.previewLabel=Instance(label,view.labels,"Preview label").gameObject; Fill((RectTransform)view.previewLabel.transform);
            var menu=Rect("Navigation",root); ViewerUI.Place(menu,new UnityEngine.Rect(18,96,176,350));
            var layout=menu.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=8; layout.childControlWidth=layout.childControlHeight=true; layout.childForceExpandHeight=false;
            view.home=Button(menu,"Home","Accueil",new UnityEngine.Rect(0,0,176,48)); view.reset=Button(menu,"Reset","Réinitialiser",new UnityEngine.Rect(0,0,176,48));
            view.explosion=Button(menu,"Explosion","Vue éclatée",new UnityEngine.Rect(0,0,176,48)); view.information=Button(menu,"Information","Masquer les infos",new UnityEngine.Rect(0,0,176,48));
            view.tour=Button(menu,"Tour","Visite guidée",new UnityEngine.Rect(0,0,176,48)); view.legal=Button(menu,"Legal","Mentions légales",new UnityEngine.Rect(0,0,176,48));
            Responsive(menu,new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(18,-96),new Vector2(150,350));
            view.title=Text(root,"Product title","Module technique 07",new UnityEngine.Rect(228,94,1000,65),32);
            Stretch(view.title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(228,-159),new Vector2(-24,-94));
            Responsive(view.title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(.5f,.5f),new Vector2(82,-126),new Vector2(-212,65));
            view.state=Text(root,"State","VUE D’ENSEMBLE",new UnityEngine.Rect(228,165,400,28),14);
            Responsive(view.state.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(0,1),new Vector2(188,-165),new Vector2(280,32));
            view.informationPanel=Instance(information,root,"Information panel");
            view.overview=Button(root,"Overview","← Revenir à la vue d’ensemble",new UnityEngine.Rect(0,0,400,48));
            Stretch((RectTransform)view.overview.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-200,20),new Vector2(200,68));
            Responsive((RectTransform)view.overview.transform,Vector2.zero,new Vector2(1,0),new Vector2(.5f,.5f),new Vector2(0,44),new Vector2(-36,48));
            view.guide=Instance(guide,root); view.guide.gameObject.SetActive(false);
            return Save<UIProductView>(root.gameObject,"Screens/Product");
        }
        // Add missing controls only: retain all existing screen styles and overrides.
        static void UpgradePartsMenu()
        {
            var menuPrefab = Get<UIPartsMenuView>("Screens/PartsMenu") ?? MakePartsMenu();
            var path = Root + "/Screens/Product.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<UIProductView>();
                bool changed = false;
                if (!view.parts)
                {
                    view.parts = Button(view.home.transform.parent,"Parts","Choisir une pièce",new UnityEngine.Rect(0,0,176,48));
                    view.parts.transform.SetSiblingIndex(view.explosion.transform.GetSiblingIndex()+1);
                    changed = true;
                }
                if (!view.partsMenu)
                {
                    view.partsMenu = Instance(menuPrefab,root.transform,"Parts menu");
                    Fill((RectTransform)view.partsMenu.transform);
                    view.partsMenu.gameObject.SetActive(false); changed = true;
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static UIPartsMenuView MakePartsMenu()
        {
            var root=ViewerUI.Box(null,"PartsMenu",new UnityEngine.Rect(0,0,1440,900),new Color(0,0,0,.7f),true);
            var view=root.gameObject.AddComponent<UIPartsMenuView>();
            var frame=Frame(root,"Frame",new UnityEngine.Rect(220,96,470,690));
            Responsive(frame,Vector2.zero,Vector2.one,new Vector2(.5f,.5f),Vector2.zero,new Vector2(-32,-100));
            view.title=Text(frame,"Title","Choisir une pièce",new UnityEngine.Rect(24,20,420,55),26);
            Stretch(view.title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(24,-75),new Vector2(-24,-20));
            var viewport=ViewerUI.Box(frame,"Viewport",new UnityEngine.Rect(24,88,422,510),new Color(0,0,0,.001f),true);
            Stretch(viewport,Vector2.zero,Vector2.one,new Vector2(24,90),new Vector2(-24,-88));
            viewport.gameObject.AddComponent<RectMask2D>();
            view.scroll=viewport.gameObject.AddComponent<ScrollRect>(); view.scroll.horizontal=false; view.scroll.movementType=ScrollRect.MovementType.Clamped; view.scroll.viewport=viewport;
            view.content=Rect("Rows",viewport); view.content.anchorMin=new Vector2(0,1); view.content.anchorMax=Vector2.one; view.content.pivot=new Vector2(.5f,1); view.content.anchoredPosition=Vector2.zero; view.content.sizeDelta=Vector2.zero;
            var layout=view.content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing=10; layout.childControlWidth=layout.childControlHeight=true; layout.childForceExpandWidth=true; layout.childForceExpandHeight=false;
            view.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            view.scroll.content=view.content; view.rowPrefab=buttonPrefab.GetComponent<UIActionButton>();
            view.close=Button(frame,"Close","Retour",new UnityEngine.Rect(24,610,422,48));
            Stretch((RectTransform)view.close.transform,Vector2.zero,new Vector2(1,0),new Vector2(24,24),new Vector2(-24,72));
            return Save<UIPartsMenuView>(root.gameObject,"Screens/PartsMenu");
        }
        static UIDialogView MakeDialog()
        {
            var root=ViewerUI.Box(null,"ModalWindow",new UnityEngine.Rect(0,0,1440,900),new Color(0,0,0,.86f),true); var view=root.gameObject.AddComponent<UIDialogView>();
            var frame=Frame(root,"Frame",new UnityEngine.Rect(0,0,800,660)); Stretch(frame,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-400,-330),new Vector2(400,330));
            Responsive(frame,Vector2.zero,Vector2.one,new Vector2(.5f,.5f),Vector2.zero,new Vector2(-32,-100));
            view.title=Text(frame,"Title","Fenêtre commune",new UnityEngine.Rect(24,24,752,56),28); Stretch(view.title.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(24,-80),new Vector2(-24,-24));
            var scroll=Scroll(frame,"Text area",new UnityEngine.Rect(24,100,752,460),out view.description); Stretch((RectTransform)scroll.transform,Vector2.zero,Vector2.one,new Vector2(24,105),new Vector2(-24,-100)); view.textArea=scroll.gameObject;
            var media=Rect("Media area",frame); Stretch(media,Vector2.zero,Vector2.one,new Vector2(24,105),new Vector2(-24,-100)); view.mediaArea=media.gameObject;
            view.image=ViewerUI.Box(media,"Photo",new UnityEngine.Rect(0,0,752,440),Color.white).GetComponent<Image>(); Fill(view.image.rectTransform); view.image.preserveAspect=true;
            view.page=Text(frame,"Page","1 / 3",new UnityEngine.Rect(0,0,100,38),18); Stretch(view.page.rectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-50,70),new Vector2(50,105));
            var actions=Rect("Actions",frame); Stretch(actions,Vector2.zero,new Vector2(1,0),new Vector2(24,24),new Vector2(-24,76));
            var layout=actions.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing=12; layout.childControlWidth=layout.childControlHeight=true; layout.childForceExpandWidth=true;
            view.no=Button(actions,"No","Non",new UnityEngine.Rect(0,0,180,48)); view.yes=Button(actions,"Yes","Oui",new UnityEngine.Rect(0,0,180,48));
            view.previous=Button(actions,"Previous","←",new UnityEngine.Rect(0,0,80,48)); view.next=Button(actions,"Next","→",new UnityEngine.Rect(0,0,80,48)); view.close=Button(actions,"Close","Retour",new UnityEngine.Rect(0,0,180,48));
            view.mediaArea.SetActive(false); view.previous.gameObject.SetActive(false); view.next.gameObject.SetActive(false); view.page.gameObject.SetActive(false);
            return Save<UIDialogView>(root.gameObject,"Screens/ModalWindow");
        }
        static UIDocumentView MakeDocument()
        {
            var root=ViewerUI.Box(null,"DocumentReader",new UnityEngine.Rect(0,0,1440,900),new Color(.03f,.045f,.07f),true); var view=root.gameObject.AddComponent<UIDocumentView>();
            view.back=Button(root,"Back","← Retour",new UnityEngine.Rect(20,18,150,48));
            view.viewport=ViewerUI.Box(root,"PDF viewport",new UnityEngine.Rect(16,88,1408,720),Color.black,true); Stretch(view.viewport,Vector2.zero,Vector2.one,new Vector2(16,88),new Vector2(-16,-88));
            view.viewport.gameObject.AddComponent<RectMask2D>(); var scroll=view.viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=view.viewport; scroll.movementType=ScrollRect.MovementType.Clamped;
            view.image=ViewerUI.RawImage(view.viewport,null,new UnityEngine.Rect(0,0,1408,720)); scroll.content=view.image.rectTransform;
            var toolbar=Rect("Toolbar",root); Stretch(toolbar,Vector2.zero,new Vector2(1,0),new Vector2(20,18),new Vector2(-20,66)); view.toolbar=toolbar.gameObject;
            var layout=toolbar.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing=12; layout.childControlHeight=layout.childControlWidth=true; layout.childForceExpandWidth=true;
            view.previous=Button(toolbar,"Previous","←",new UnityEngine.Rect(0,0,80,48)); view.next=Button(toolbar,"Next","→",new UnityEngine.Rect(0,0,80,48));
            view.page=Text(toolbar,"Page","1 / 1",new UnityEngine.Rect(0,0,120,48),20); view.page.alignment=TextAnchor.MiddleCenter;
            view.zoomOut=Button(toolbar,"Zoom out","−",new UnityEngine.Rect(0,0,80,48)); view.zoomIn=Button(toolbar,"Zoom in","+",new UnityEngine.Rect(0,0,80,48));
            view.message=Text(root,"Status","Document PDF",new UnityEngine.Rect(200,20,1100,50),19); Stretch(view.message.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(190,-70),new Vector2(-20,-20));
            view.openExternal=Button(root,"Open original (editor only)","Ouvrir le PDF original",new UnityEngine.Rect(0,0,340,52)); Stretch((RectTransform)view.openExternal.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-170,-26),new Vector2(170,26));
            return Save<UIDocumentView>(root.gameObject,"Screens/DocumentReader");
        }
        static ViewerUIRoot MakeRoot(UIWelcomeView welcome,UICatalogView catalog,UIProductView product,UIDialogView dialog,UIDocumentView document)
        {
            var go=new GameObject("Viewer Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); var root=go.AddComponent<ViewerUIRoot>(); root.canvas=go.GetComponent<Canvas>(); root.canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1440,900); scaler.matchWidthOrHeight=1;
            root.safeArea=Rect("SafeArea",go.transform); Fill(root.safeArea);
            root.welcome=Instance(welcome,root.safeArea); Fill((RectTransform)root.welcome.transform);
            root.catalog=Instance(catalog,root.safeArea); Fill((RectTransform)root.catalog.transform); root.catalog.gameObject.SetActive(false);
            root.product=Instance(product,root.safeArea); Fill((RectTransform)root.product.transform); root.product.gameObject.SetActive(false);
            var header=Rect("Header",root.safeArea); Stretch(header,new Vector2(0,1),Vector2.one,new Vector2(0,-72),Vector2.zero);
            root.manufacturer=Text(header,"Manufacturer (catalog)","LIBERTY 3D",new UnityEngine.Rect(24,22,600,28),18); root.manufacturer.color=new Color(.15f,.82f,.72f);
            Stretch(root.manufacturer.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(24,-50),new Vector2(-135,-22));
            root.logo=ViewerUI.Box(header,"Logo (catalog)",new UnityEngine.Rect(650,14,72,40),Color.white).GetComponent<Image>(); root.logo.preserveAspect=true; root.logo.gameObject.SetActive(false);
            Stretch(root.logo.rectTransform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(-36,-54),new Vector2(36,-14));
            root.language=Button(header,"Language","FR / EN",new UnityEngine.Rect(1324,16,94,42)); Stretch((RectTransform)root.language.transform,new Vector2(1,1),Vector2.one,new Vector2(-116,-58),new Vector2(-22,-16));
            var divider=ViewerUI.Box(header,"Divider",new UnityEngine.Rect(24,70,1392,1),new Color(.2f,.3f,.36f)); Stretch(divider,Vector2.zero,new Vector2(1,0),new Vector2(24,0),new Vector2(-24,1));
            root.dialog=Instance(dialog,root.safeArea); Fill((RectTransform)root.dialog.transform); root.dialog.gameObject.SetActive(false);
            root.document=Instance(document,root.safeArea); Fill((RectTransform)root.document.transform); root.document.gameObject.SetActive(false);
            return Save<ViewerUIRoot>(go,"ViewerUI");
        }
        static void CreatePreview(ViewerUIRoot prefab)
        {
            string path=Root+"/UI_Preview.unity"; if(File.Exists(path)) return;
            string prefabPath=AssetDatabase.GetAssetPath(prefab);
            var active=SceneManager.GetActiveScene();
            bool untitled=string.IsNullOrEmpty(active.path);
            if(untitled && !Application.isBatchMode) { Debug.LogWarning("Save your current scene, then run Create editable prefabs again to create the preview scene. UI prefabs are already available."); return; }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,untitled ? NewSceneMode.Single : NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),scene);
            var preview=new GameObject("UI Preview — choose screen in Inspector").AddComponent<UIEditorPreview>(); preview.ui=root.GetComponent<ViewerUIRoot>();
            EditorSceneManager.SaveScene(scene,path);
            if(!untitled) { EditorSceneManager.CloseScene(scene,true); if(active.IsValid()) SceneManager.SetActiveScene(active); }
        }
    }
}
