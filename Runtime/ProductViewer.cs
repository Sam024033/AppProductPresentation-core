using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Screen = UnityEngine.Device.Screen;

namespace Liberty3D.ProductViewer
{
    public enum ViewerState { Welcome, Catalog, Assembled, Exploded, Detail }
    public class ProductViewer : MonoBehaviour
    {
        public ViewerCatalog catalog;
        public Camera viewCamera;
        [Tooltip("Zoom strength per normalized mouse-wheel tick. 0.12 is about 11% closer per tick.")]
        [Range(.01f, .5f)] public float wheelZoomSensitivity = .12f;
        [Header("Labels : distance relative a la vue initiale")]
        [Min(.01f)] public float labelNearDistance = .7f;
        [Min(.02f)] public float labelFarDistance = 1.5f;
        [Range(.3f, 2f)] public float labelNearScale = 1.2f;
        [Range(.3f, 2f)] public float labelFarScale = .75f;
        [Range(0f, 1f)] public float labelNearOpacity = 1f;
        [Range(0f, 1f)] public float labelFarOpacity = .45f;
        public ViewerState State { get; private set; } = ViewerState.Welcome;
        public bool Busy { get; private set; }
        public bool AutoRotating { get; private set; }
        public ProductPart SelectedPart => selected;
        public float ZoomFactor => zoom;
        ProductDefinition product;
        ProductRig rig;
        Transform pivot;
        ProductPart selected, pending;
        Canvas canvas;
        RectTransform safeRoot;
        ViewerUIRoot ui;
        public ViewerUIRoot Interface => ui;
        readonly List<UIProductCard> cards = new List<UIProductCard>();
        GraphicRaycaster raycaster;
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        readonly List<LabelView> labels = new List<LabelView>();
        class LabelView { public ProductPart part; public UIPartLabelView view; public RectTransform box, line; public CanvasGroup group; public Rect bounds; public bool positioned; public Vector3 initialScale; }
        bool information = true, pointerDown, dragging, blockedPointer, multiTouch;
        bool modal, gallery, legal, partsOpen;
        readonly List<UIActionButton> partRows = new List<UIActionButton>();
        ProductPart downPart;
        Vector2 downPosition, lastPosition;
        float pinchDistance;
        int activeTouchId = -1;
        float fitDistance = 8, zoom = 1, currentDistance = 8, overallRadius = 2;
        Vector3 focusLocal;
        Quaternion savedRotation;
        float savedZoom;
        bool savedInformation;
        float width, height;
        Rect lastSafe;
        int lastWidth, lastHeight, imageIndex, guideIndex = -1;
        Coroutine transition;
        PdfDocumentView pdf;
        string notice;
        bool refreshQueued;
        public string DebugSnapshot => $"state={State}; busy={Busy}; auto={AutoRotating}; part={(selected ? selected.partId : "-")}; language={(ViewerLanguage.English ? "en" : "fr")}; zoom={zoom:F2}; parts={(rig ? rig.parts.Length : 0)}";
        string T(string fr, string en) => ViewerLanguage.Pick(fr, en);

        IEnumerator Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = true;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
#endif
            if (!catalog || catalog.products == null) { Debug.LogError("Assign a manufacturer catalog to ProductViewer."); enabled = false; yield break; }
            if (!viewCamera) viewCamera = Camera.main;
            viewCamera.clearFlags = CameraClearFlags.SolidColor; viewCamera.backgroundColor = catalog.background;
            viewCamera.transform.rotation = Quaternion.identity;
            viewCamera.nearClipPlane = .03f; viewCamera.farClipPlane = 1000;
            pivot = new GameObject("Product orbit").transform;
            if (!catalog.uiPrefab) { Debug.LogError("Assign an editable ViewerUI prefab to the catalog. Tools > Liberty 3D > UI > Create editable prefabs."); enabled = false; yield break; }
            ui = Instantiate(catalog.uiPrefab); ui.name = "Viewer Canvas";
#if UNITY_ANDROID && !UNITY_EDITOR
            ConfigureInputHints(ui.product.transform, true);
#else
            ConfigureInputHints(ui.product.transform, false);
#endif
            ui.product.gameObject.AddComponent<PortraitNavigationLayout>();
            // Some editable root canvas prefabs were saved with an all-zero scale.
            // Reset only this invalid scale; keep intentional nonzero canvas settings.
            if (ui.transform.localScale.sqrMagnitude < .000001f) ui.transform.localScale = Vector3.one;
            // Existing welcome effect is a normal ParticleSystem, so render it as
            // Canvas geometry while this screen is active. Prefab assets stay editable.
            foreach (var welcomeEffect in ui.welcome.GetComponentsInChildren<ParticleSystem>(true))
                if (welcomeEffect.GetComponent<ParticleSystemRenderer>() && !welcomeEffect.GetComponent<WelcomeParticlesAdapter>())
                    welcomeEffect.gameObject.AddComponent<WelcomeParticlesAdapter>();
            canvas = ui.canvas; safeRoot = ui.safeArea;
            raycaster = canvas.GetComponent<GraphicRaycaster>();
            ui.catalog.previewCard.SetActive(false); ui.product.previewLabel.SetActive(false);
            ui.dialog.gameObject.SetActive(false); ui.document.gameObject.SetActive(false);
            if (!FindFirstObjectByType<EventSystem>())
            {
                var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            pdf = gameObject.AddComponent<PdfDocumentView>(); pdf.Configure(ui.document); pdf.Closed += () => { modal = false; RefreshUI(); };
            yield return LocalizationSettings.InitializationOperation;
            ViewerLanguage.Set(PlayerPrefs.GetInt("Liberty3D.Language", 0) == 1);
            ViewerLanguage.Changed += QueueRefresh;
            RefreshUI();
        }

        public static void ConfigureInputHints(Transform productRoot, bool touch)
        {
            if (!productRoot) return;
            var mouse = productRoot.Find("Souris");
            var gestures = productRoot.Find("Gestures");
            if (mouse) mouse.gameObject.SetActive(!touch);
            if (gestures) gestures.gameObject.SetActive(touch);
        }

        void OnDestroy()
        {
            ViewerLanguage.Changed -= QueueRefresh;
            if (pivot) Destroy(pivot.gameObject);
            if (canvas) Destroy(canvas.gameObject);
        }

        void Update()
        {
            if (!canvas) return;
            if (refreshQueued) { refreshQueued = false; RefreshUI(); }
            if (lastWidth != Screen.width || lastHeight != Screen.height || lastSafe != Screen.safeArea) RefreshUI();
            if (rig)
            {
                if (AutoRotating && !Busy && !modal) pivot.Rotate(Vector3.up, product.autoRotationSpeed * Time.deltaTime, Space.World);
                var f = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10);
                float distance = fitDistance * zoom;
                currentDistance = Mathf.Lerp(currentDistance, distance, f);
                bool showPanel = information && State != ViewerState.Exploded && guideIndex < 0;
                bool portrait = width < 800;
                float shiftX = showPanel && !portrait ? -currentDistance * .15f : 0;
                float shiftY = showPanel && portrait ? -currentDistance * .11f : 0;
                viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, new Vector3(shiftX, shiftY, -currentDistance), f);
                UpdateLabels();
            }
            HandleInput();
        }
        void QueueRefresh() { refreshQueued = true; }

        public void OpenProduct(ProductDefinition definition)
        {
            if (Busy || !definition || !definition.prefab) return;
            ClearLabels();
            if (rig) Destroy(rig.gameObject);
            product = definition; rig = Instantiate(product.prefab, pivot); rig.Initialize();
            rig.PreviewAssembled(); rig.transform.localPosition = Vector3.zero; rig.transform.localRotation = Quaternion.identity;
            selected = null; pending = null; focusLocal = Vector3.zero; zoom = 1; information = true;
            pivot.rotation = Quaternion.Euler(product.initialViewEuler); pivot.localScale = Vector3.one;
            State = ViewerState.Assembled;
            overallRadius = rig.Bounds.extents.magnitude;
            fitDistance = FitDistance(overallRadius); currentDistance = fitDistance;
            AutoRotating = false;
            RunTransition(false, null, true, true); RefreshUI();
        }

        float FitDistance(float radius)
        {
            float vertical = viewCamera.fieldOfView * Mathf.Deg2Rad * .5f;
            float horizontal = Mathf.Atan(Mathf.Tan(vertical) * Mathf.Max(.25f, viewCamera.aspect));
            float framing = 1f;
            if (product && State == ViewerState.Assembled) framing = product.assembledFraming;
            else if (product && State == ViewerState.Exploded) framing = product.explodedFraming;
            // Old catalog assets may deserialize a missing value as zero.
            if (framing <= 0f) framing = 1f;
            return Mathf.Max(.1f, Mathf.Max(.5f, radius / Mathf.Sin(Mathf.Min(vertical, horizontal)) * 1.22f) * framing);
        }

        public void ToggleExplosion()
        {
            if (Busy || !rig || State == ViewerState.Detail) return;
            guideIndex = -1; AutoRotating = false; zoom = 1;
            bool explode = State != ViewerState.Exploded;
            State = explode ? ViewerState.Exploded : ViewerState.Assembled;
            RunTransition(explode, null, false); RefreshUI();
        }
        public void ResetView()
        {
            if (Busy || !rig) return;
            guideIndex = -1; selected = null; pending = null; modal = false; zoom = 1;
            State = ViewerState.Assembled;
            RunTransition(false, null, true); RefreshUI();
        }
        public void RequestPart(ProductPart part)
        {
            if (Busy || !rig || State < ViewerState.Assembled || !part || Array.IndexOf(rig.parts, part) < 0) return;
            partsOpen = false; gallery = legal = false;
            AutoRotating = false; pending = part; modal = true; RefreshUI();
        }
        public void ConfirmPart()
        {
            if (!pending || Busy) return;
            if (State != ViewerState.Detail)
            { savedRotation = pivot.rotation; savedZoom = State == ViewerState.Exploded ? zoom : 1; savedInformation = information; }
            guideIndex = -1; AutoRotating = false;
            selected = pending; pending = null; modal = partsOpen = false; zoom = 1;
            State = ViewerState.Detail; RunTransition(true, selected, false); RefreshUI();
        }
        public void ReturnOverview()
        {
            if (Busy || State != ViewerState.Detail) return;
            guideIndex = -1; selected = null; State = ViewerState.Exploded;
            zoom = savedZoom; information = savedInformation;
            RunTransition(true, null, false, false, savedRotation); RefreshUI();
        }
        public void Home()
        {
            if (Busy) return;
            if (rig) { Destroy(rig.gameObject); rig = null; }
            selected = pending = null; modal = false; guideIndex = -1;
            AutoRotating = false; State = ViewerState.Catalog; RefreshUI();
        }
        void RunTransition(bool explode, ProductPart isolate, bool reset, bool appear = false, Quaternion? rotation = null)
        {
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(Animate(explode, isolate, reset, appear, rotation));
        }
        IEnumerator Animate(bool explode, ProductPart isolate, bool reset, bool appear, Quaternion? rotation)
        {
            Busy = true; AutoRotating = false; pointerDown = false;
            int count = rig.parts.Length;
            var starts = new Vector3[count]; var rotations = new Quaternion[count]; var opacities = new float[count];
            for (int i = 0; i < count; i++) { starts[i] = rig.parts[i].transform.localPosition; rotations[i] = rig.parts[i].transform.localRotation; opacities[i] = rig.parts[i].Opacity; }
            var accessoryStarts = new float[rig.accessories.Length];
            for (int i = 0; i < accessoryStarts.Length; i++) accessoryStarts[i] = rig.accessories[i] ? rig.accessories[i].Opacity : 1;
            var origin = rig.transform.localPosition;
            var targetFocus = Vector3.zero;
            if (isolate)
            {
                var localCenter = isolate.transform.InverseTransformPoint(isolate.Bounds.center);
                targetFocus = -(isolate.explodedPosition + Quaternion.Euler(isolate.explodedEuler) * Vector3.Scale(localCenter, isolate.transform.localScale));
            }
            var startRotation = pivot.rotation;
            var endRotation = reset ? Quaternion.Euler(product.initialViewEuler) : rotation ?? pivot.rotation;
            float duration = appear ? .38f : .65f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                for (int i = 0; i < count; i++)
                {
                    var part = rig.parts[i];
                    part.transform.localPosition = Vector3.Lerp(starts[i], explode ? part.explodedPosition : part.assembledPosition, t);
                    part.transform.localRotation = Quaternion.Slerp(rotations[i], Quaternion.Euler(explode ? part.explodedEuler : part.assembledEuler), t);
                    part.SetOpacity(Mathf.Lerp(opacities[i], isolate && part != isolate ? 0 : 1, t));
                }
                for (int i = 0; i < accessoryStarts.Length; i++)
                    if (rig.accessories[i]) rig.accessories[i].SetOpacity(Mathf.Lerp(accessoryStarts[i], rig.accessories[i].TargetOpacity(explode, isolate != null), t));
                rig.transform.localPosition = Vector3.Lerp(origin, targetFocus, t);
                pivot.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                pivot.localScale = Vector3.one * (appear ? Mathf.Lerp(.02f, 1, t) : 1);
                float radius = isolate ? isolate.Bounds.extents.magnitude : rig.Bounds.extents.magnitude;
                fitDistance = FitDistance(Mathf.Max(.1f, radius));
                yield return null;
            }
            foreach (var part in rig.parts)
            {
                part.transform.localPosition = explode ? part.explodedPosition : part.assembledPosition;
                part.transform.localRotation = Quaternion.Euler(explode ? part.explodedEuler : part.assembledEuler);
                part.SetOpacity(isolate && part != isolate ? 0 : 1);
            }
            foreach (var accessory in rig.accessories) if (accessory) accessory.SetOpacity(accessory.TargetOpacity(explode, isolate != null));
            rig.transform.localPosition = targetFocus; pivot.rotation = endRotation; pivot.localScale = Vector3.one;
            focusLocal = -targetFocus;
            overallRadius = (isolate ? isolate.Bounds : rig.Bounds).extents.magnitude;
            fitDistance = FitDistance(overallRadius);
            Busy = false; AutoRotating = reset && !isolate; transition = null;
            Debug.Log("[Liberty3D] " + DebugSnapshot);
        }

        void HandleInput()
        {
            if (modal || Busy || !rig || (pdf && pdf.IsOpen)) { pointerDown = false; pinchDistance = 0; return; }
            var touchscreen = Touchscreen.current;
            int touches = 0; Vector2 first = default, second = default; int firstId = -1;
            if (touchscreen != null) foreach (var touch in touchscreen.touches)
            {
                if (!touch.press.isPressed) continue;
                if (touches == 0) { first = touch.position.ReadValue(); firstId = touch.touchId.ReadValue(); }
                if (touches == 1) second = touch.position.ReadValue(); touches++;
            }
            if (touches >= 2)
            {
                if (!multiTouch) { blockedPointer = IsBlocked(first, out _) || IsBlocked(second, out _); }
                multiTouch = true; pointerDown = false;
                float distance = Vector2.Distance(first, second);
                if (!blockedPointer) { AutoRotating = false; if (pinchDistance > 0) SetZoom(zoom * pinchDistance / Mathf.Max(1, distance)); }
                pinchDistance = distance; return;
            }
            if (multiTouch) { if (touches == 0) { multiTouch = false; pinchDistance = 0; activeTouchId = -1; } return; }
            if (touches == 1)
            {
                if (!pointerDown) { activeTouchId = firstId; PointerBegin(first); }
                else if (firstId == activeTouchId) PointerMove(first);
                else { pointerDown = false; multiTouch = true; }
                return;
            }
            if (activeTouchId >= 0) { if (pointerDown) PointerEnd(lastPosition); activeTouchId = -1; return; }
            var mouse = Mouse.current; if (mouse == null) return;
            var position = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) PointerBegin(position);
            if (mouse.leftButton.isPressed && pointerDown) PointerMove(position);
            if (mouse.leftButton.wasReleasedThisFrame && pointerDown) PointerEnd(position);
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > .0001f && !IsBlocked(position, out _))
            {
                // Input System 1.19 defaults to normalized ticks, not the legacy Windows +/-120 range.
                if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                    (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)) scroll /= 120f;
                AutoRotating = false;
                SetZoom(zoom * Mathf.Exp(-scroll * wheelZoomSensitivity));
            }
        }
        bool IsBlocked(Vector2 position, out ProductPart labelPart)
        {
            labelPart = null; hits.Clear();
            raycaster.Raycast(new PointerEventData(EventSystem.current) { position = position }, hits);
            foreach (var hit in hits)
            {
                var label = hit.gameObject.GetComponentInParent<PartLabel>();
                if (label) { labelPart = label.part; return false; }
                return true;
            }
            return false;
        }
        ProductPart HitPart(Vector2 position)
        {
            if (State != ViewerState.Exploded) return null;
            if (Physics.Raycast(viewCamera.ScreenPointToRay(position), out var hit, 1000)) return hit.collider.GetComponentInParent<ProductPart>();
            return null;
        }
        void PointerBegin(Vector2 position)
        {
            pointerDown = true; dragging = false; downPosition = lastPosition = position;
            blockedPointer = IsBlocked(position, out downPart);
            if (!blockedPointer) { AutoRotating = false; if (!downPart) downPart = HitPart(position); }
        }
        void PointerMove(Vector2 position)
        {
            float threshold = Mathf.Max(10, Screen.height * .012f);
            if ((position - downPosition).sqrMagnitude > threshold * threshold) dragging = true;
            if (!blockedPointer && dragging)
            {
                var delta = (position - lastPosition) * (180f / Mathf.Max(400, Screen.height));
                pivot.rotation = Quaternion.AngleAxis(-delta.x, Vector3.up) * Quaternion.AngleAxis(delta.y, Vector3.right) * pivot.rotation;
            }
            lastPosition = position;
        }
        void PointerEnd(Vector2 position)
        {
            pointerDown = false;
            if (!dragging && !blockedPointer && downPart)
            {
                IsBlocked(position, out var label);
                if (label == downPart || HitPart(position) == downPart) RequestPart(downPart);
            }
            downPart = null;
        }
        void SetZoom(float value)
        {
            float min = selected ? selected.minimumZoomFactor : product.minimumZoomFactor;
            float max = selected ? selected.maximumZoomFactor : product.maximumZoomFactor;
            zoom = Mathf.Clamp(value, min, max);
        }

        void RefreshUI()
        {
            if (!canvas || !ui || languageChangePending) return;
            bool resized = lastWidth != Screen.width || lastHeight != Screen.height || lastSafe != Screen.safeArea;
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
            ui.ApplySafeArea(); Canvas.ForceUpdateCanvases();
            width = safeRoot.rect.width; height = safeRoot.rect.height;
            ui.manufacturer.text = catalog.manufacturer;
            ui.logo.gameObject.SetActive(catalog.logo != null); if (catalog.logo) ui.logo.sprite = catalog.logo;
            ui.language.Bind(ViewerLanguage.English ? "EN / FR" : "FR / EN", RequestLanguageChange);
            ui.welcome.gameObject.SetActive(State == ViewerState.Welcome);
            ui.catalog.gameObject.SetActive(State == ViewerState.Catalog);
            ui.product.gameObject.SetActive(State >= ViewerState.Assembled);
            if (State == ViewerState.Welcome) WelcomeUI();
            else if (State == ViewerState.Catalog) CatalogUI();
            else ProductUI();
            ui.dialog.gameObject.SetActive(modal && !partsOpen && !pdf.IsOpen);
            if (modal && !partsOpen && !pdf.IsOpen) ModalUI();
            if (ui.product.partsMenu) ui.product.partsMenu.gameObject.SetActive(partsOpen);
            if (resized) ui.product.informationPanel.ResetLayoutOrigin();
            if (rig) fitDistance = FitDistance(overallRadius);
        }
        bool languageChangePending;
        void RequestLanguageChange()
        {
            if (!languageChangePending) StartCoroutine(PrepareLanguageChange(!ViewerLanguage.English));
        }

        System.Collections.Generic.IEnumerable<ProductText> AllLocalizedTexts()
        {
            if (!catalog) yield break;
            yield return catalog.welcome; yield return catalog.legal;
            if (catalog.products == null) yield break;
            foreach (var item in catalog.products)
            {
                if (!item) continue;
                if (item.information != null) { yield return item.information.title; yield return item.information.description; }
                if (item.guidedSteps != null) foreach (var step in item.guidedSteps)
                    if (step != null) { yield return step.title; yield return step.description; }
                if (item.prefab) foreach (var part in item.prefab.GetComponentsInChildren<ProductPart>(true))
                    if (part && part.information != null) { yield return part.information.title; yield return part.information.description; }
            }
        }

        IEnumerator PrepareLanguageChange(bool english)
        {
            languageChangePending = true;
            if (ui && ui.language && ui.language.button) ui.language.button.interactable = false;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(english ? "en" : "fr");
            if (locale != null)
            {
                // Unity releases cached tables on a locale change: select first, then load.
                // Keep the existing UI until every needed table is ready.
                LocalizationSettings.SelectedLocale = locale;
                var seen = new HashSet<UnityEngine.Localization.Tables.TableReference>();
                foreach (var item in AllLocalizedTexts())
                {
                    if (item == null || item.localized == null || item.localized.IsEmpty) continue;
                    var table = item.localized.TableReference;
                    if (!seen.Add(table)) continue;
                    var load = LocalizationSettings.StringDatabase.GetTableAsync(table, locale);
                    if (!load.IsDone) yield return load;
                }
            }
            ViewerLanguage.Set(english);
            if (ui && ui.language && ui.language.button) ui.language.button.interactable = true;
            languageChangePending = false;
            refreshQueued = false;
            RefreshUI();
        }

        void WelcomeUI()
        {
            ui.welcome.heading.text = catalog.welcome.Value;
            ui.welcome.explore.BindAction(() => { State = ViewerState.Catalog; RefreshUI(); });
        }
        void CatalogUI()
        {
            if (cards.Count != catalog.products.Length)
            {
                foreach (var card in cards) if (card) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
                cards.Clear();
                foreach (var definition in catalog.products) cards.Add(Instantiate(ui.catalog.cardPrefab, ui.catalog.cards));
            }
            for (int i = 0; i < cards.Count; i++)
            {
                var definition = catalog.products[i]; var card = cards[i]; card.gameObject.SetActive(definition != null);
                if (!definition) continue;
                card.title.text = definition.information.title.Value;
                card.thumbnail.sprite = definition.thumbnail; card.thumbnail.enabled = definition.thumbnail != null;
                card.explore.Bind(T("Explorer", "Explore"), () => OpenProduct(definition));
            }
        }
        void Bind(UIActionButton button, string text, UnityEngine.Events.UnityAction action)
        {
            button.Bind(text, () => { if (!Busy) action(); });
        }
        void ProductUI()
        {
            var view = ui.product;
            view.title.text = selected ? selected.information.title.Value : product.information.title.Value;
            view.state.text = State == ViewerState.Exploded ? T("VUE ÉCLATÉE", "EXPLODED VIEW") : State == ViewerState.Detail ? T("COMPOSANT", "COMPONENT") : T("VUE D’ENSEMBLE", "OVERVIEW");
            if (view.parts) Bind(view.parts, T("Choisir une pièce", "Choose a part"), OpenPartsMenu);
            Bind(view.home, T("Accueil", "Home"), Home); Bind(view.reset, T("Réinitialiser", "Reset view"), ResetView);
            view.explosion.gameObject.SetActive(State != ViewerState.Detail);
            Bind(view.explosion, State == ViewerState.Exploded ? T("Réassembler", "Assemble") : T("Vue éclatée", "Exploded view"), ToggleExplosion);
            Bind(view.information, information ? T("Masquer les infos", "Hide information") : T("Afficher les infos", "Show information"), ToggleInformation);
            view.UpdateActionSprites(information, State == ViewerState.Exploded);
            view.tour.gameObject.SetActive(product.guidedSteps != null && product.guidedSteps.Length > 0);
            Bind(view.tour, T("Visite guidée", "Guided tour"), () => StartGuide(0));
            Bind(view.legal, T("Mentions légales", "Legal notice"), () => { legal = true; modal = true; RefreshUI(); });
            view.overview.gameObject.SetActive(State == ViewerState.Detail);
            Bind(view.overview, T("← Revenir à la vue d’ensemble", "← Return to overview"), ReturnOverview);
            bool panelVisible = information && State != ViewerState.Exploded && guideIndex < 0;
            view.informationPanel.SetVisible(panelVisible);
            if (panelVisible) InformationUI(selected ? selected.information : product.information);
            view.labels.gameObject.SetActive(information && State == ViewerState.Exploded);
            if (State == ViewerState.Exploded) EnsureLabels();
            view.guide.gameObject.SetActive(guideIndex >= 0);
            if (guideIndex >= 0) GuideUI();
            if (partsOpen) PartsMenuUI();
        }
        void ClearLabels()
        {
            foreach (var label in labels) if (label.view) { label.view.gameObject.SetActive(false); Destroy(label.view.gameObject); }
            labels.Clear();
        }
        void EnsureLabels()
        {
            if (labels.Count != rig.parts.Length || (labels.Count > 0 && labels[0].part != rig.parts[0]))
            {
                ClearLabels();
                foreach (var part in rig.parts)
                {
                    var view = Instantiate(ui.product.labelPrefab, ui.product.labels);
                    var rt = (RectTransform)view.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                    view.marker.part = part;
                    labels.Add(new LabelView {part = part, view = view, box = view.badge, line = view.leader, group = view.group, initialScale = view.badge.localScale});
                }
            }
            foreach (var label in labels) label.view.title.text = label.part.information.title.Value;
        }
        public void OpenPartsMenu()
        {
            if (Busy || !rig || !ui.product.partsMenu || modal) return;
            AutoRotating = false; partsOpen = modal = true;
            ui.product.partsMenu.scroll.verticalNormalizedPosition = 1;
            RefreshUI();
        }
        void PartsMenuUI()
        {
            var view = ui.product.partsMenu;
            view.title.text = T("Choisir une pièce", "Choose a part");
            view.close.Bind(T("Retour", "Back"), CloseModal);
            int index = 0;
            foreach (var part in rig.parts)
            {
                if (!part) continue;
                if (index == partRows.Count) partRows.Add(Instantiate(view.rowPrefab, view.content));
                var row = partRows[index++]; row.gameObject.SetActive(true);
                var target = part;
                row.Bind(part.information.title.Value, () => RequestPart(target));
            }
            for (; index < partRows.Count; index++) partRows[index].gameObject.SetActive(false);
        }
        void ToggleInformation() { information = !information; RefreshUI(); }
        void InformationUI(ProductInformation info)
        {
            var view = ui.product.informationPanel;
            view.title.text = info.title.Value; view.description.text = info.description.Value;
            view.images.gameObject.SetActive(info.images != null && info.images.Length > 0);
            Bind(view.images, T("Voir plus d’images", "View more images"), () => { gallery = true; imageIndex = 0; modal = true; RefreshUI(); });
            view.website.gameObject.SetActive(!string.IsNullOrEmpty(info.webUrl));
            Bind(view.website, T("Site web", "Website"), () => OpenWeb(info.webUrl));
            view.document.gameObject.SetActive(info.Pdf != null);
            Bind(view.document, T("Plus d’informations · PDF", "More information · PDF"), () => { modal = true; pdf.Open(info.Pdf); RefreshUI(); });
            view.Reflow(width < 800);
        }
        void OpenWeb(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http")) Application.OpenURL(url);
        }
        void ModalUI()
        {
            var view = ui.dialog;
            view.yes.gameObject.SetActive(pending != null); view.no.gameObject.SetActive(pending != null);
            view.close.gameObject.SetActive(pending == null);
            view.textArea.SetActive(!gallery); view.mediaArea.SetActive(gallery);
            view.previous.gameObject.SetActive(gallery); view.next.gameObject.SetActive(gallery); view.page.gameObject.SetActive(gallery);
            view.close.Bind(T("Retour", "Back"), CloseModal);
            if (pending)
            {
                view.title.text = T("Explorer ce composant ?", "Explore this component?");
                view.description.text = T("Voulez-vous accéder à ", "Would you like to view ") + pending.information.title.Value + " ?";
                view.no.Bind(T("Non", "No"), CloseModal); view.yes.Bind(T("Oui", "Yes"), ConfirmPart);
            }
            else if (gallery)
            {
                var photos = (selected ? selected.information : product.information).images;
                view.title.text = T("Photographies", "Photographs"); view.page.text = $"{imageIndex + 1} / {photos.Length}";
                view.image.sprite = photos[imageIndex];
                view.previous.Bind("←", () => { imageIndex = (imageIndex + photos.Length - 1) % photos.Length; RefreshUI(); });
                view.next.Bind("→", () => { imageIndex = (imageIndex + 1) % photos.Length; RefreshUI(); });
            }
            else { view.title.text = T("Mentions légales", "Legal notice"); view.description.text = notice ?? catalog.legal.Value; }
        }
        void CloseModal() { modal = gallery = legal = partsOpen = false; pending = null; notice = null; RefreshUI(); }

        void UpdateLabels()
        {
            if (labels.Count == 0 || State != ViewerState.Exploded || !information) return;
            var used = new List<Rect>();
            Vector3 productCenter = rig.Bounds.center;
            foreach (var label in labels)
            {
                var screen = viewCamera.WorldToScreenPoint(label.part.WorldAnchor);
                Vector2 anchor = new Vector2((screen.x - Screen.safeArea.x) / canvas.scaleFactor, (Screen.safeArea.yMax - screen.y) / canvas.scaleFactor);
                float margin = Mathf.Min(Mathf.Min(anchor.x, width - anchor.x), Mathf.Min(anchor.y - 76, height - 50 - anchor.y));
                float alpha = screen.z <= 0 || Busy ? 0 : Mathf.Clamp01(margin / 80);
                float relativeDistance = Vector3.Distance(viewCamera.transform.position, label.part.WorldAnchor) / Mathf.Max(.01f, fitDistance);
                float distanceFactor = Mathf.InverseLerp(labelNearDistance, Mathf.Max(labelNearDistance + .01f, labelFarDistance), relativeDistance);
                float scale = Mathf.Lerp(labelNearScale, labelFarScale, distanceFactor);
                alpha *= Mathf.Lerp(labelNearOpacity, labelFarOpacity, distanceFactor);
                label.box.localScale = label.initialScale * scale;
                Vector3 direction = label.part.Bounds.center - productCenter;
                if (direction.sqrMagnitude > .000001f)
                {
                    Vector3 towardCamera = (viewCamera.transform.position - label.part.WorldAnchor).normalized;
                    float facing = Vector3.Dot(direction.normalized, towardCamera);
                    alpha *= Mathf.Lerp(.08f, 1f, Mathf.InverseLerp(-.1f, .55f, facing));
                }
                Vector2 offset = label.part.labelOffset;
                float x = anchor.x + offset.x; float y = anchor.y - offset.y;
                float left = width < 800 ? 182 : 214;
                float boxWidth = label.box.rect.width * Mathf.Abs(label.box.localScale.x);
                float boxHeight = label.box.rect.height * Mathf.Abs(label.box.localScale.y);
                float right = Mathf.Max(left, width - boxWidth - 12);
                float top = 172, bottom = Mathf.Max(top, height - boxHeight - 20);
                x = Mathf.Clamp(x, left, right); y = Mathf.Clamp(y, top, bottom);
                var bounds = new Rect(x, y, boxWidth, boxHeight);
                var best = bounds; bool found = false;
                float stepX = bounds.width + 12, stepY = bounds.height + 8;
                int columns = Mathf.Max(1, Mathf.CeilToInt((right - left) / Mathf.Max(1, stepX)) + 1);
                int rows = Mathf.Max(1, Mathf.CeilToInt((bottom - top) / Mathf.Max(1, stepY)) + 1);
                for (int column = 0; column < columns * 2 + 1 && !found; column++)
                for (int row = 0; row < rows * 2 + 1 && !found; row++)
                {
                    int columnOffset = column == 0 ? 0 : (column % 2 == 1 ? (column + 1) / 2 : -column / 2);
                    var candidate = bounds;
                    candidate.x = Mathf.Clamp(x + columnOffset * stepX, left, right);
                    int rowOffset = row == 0 ? 0 : (row % 2 == 1 ? (row + 1) / 2 : -row / 2);
                    candidate.y = Mathf.Clamp(y + rowOffset * stepY, top, bottom);
                    bool overlap = false;
                    foreach (var previous in used) if (candidate.Overlaps(previous)) { overlap = true; break; }
                    if (!overlap) { best = candidate; found = true; }
                }
                bounds = best;
                // Solve the target layout immediately, then move the rendered label smoothly.
                // Keep target rectangles separate from displayed rectangles so a moving label
                // does not make other labels change slots on each frame.
                if (alpha > .35f) used.Add(bounds);
                if (label.positioned)
                {
                    float smoothing = 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
                    bounds.position = Vector2.Lerp(label.bounds.position, bounds.position, smoothing);
                }
                else label.positioned = true;
                label.bounds = bounds;
                ViewerUI.Place(label.box, new Rect(bounds.position, label.box.rect.size)); label.group.alpha = alpha; label.group.blocksRaycasts = alpha > .35f;
                var endpoint = new Vector2(Mathf.Clamp(anchor.x, bounds.xMin, bounds.xMax), Mathf.Clamp(anchor.y, bounds.yMin, bounds.yMax));
                var vector = endpoint - anchor;
                ViewerUI.Place(label.line, new Rect(anchor.x, anchor.y, vector.magnitude, Mathf.Max(.5f, label.line.rect.height)));
                label.line.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg);
                label.view.PlaceDot(anchor);
            }
        }
        void StartGuide(int index)
        {
            if (Busy) return;
            guideIndex = index; StartCoroutine(ApplyGuide());
        }
        IEnumerator ApplyGuide()
        {
            var step = product.guidedSteps[guideIndex];
            AutoRotating = false; selected = null; zoom = 1;
            State = step.exploded || !string.IsNullOrEmpty(step.partId) ? ViewerState.Exploded : ViewerState.Assembled;
            RunTransition(State == ViewerState.Exploded, null, false, false, Quaternion.Euler(step.viewEuler)); RefreshUI();
            while (Busy) yield return null;
            if (!string.IsNullOrEmpty(step.partId))
            {
                foreach (var part in rig.parts) if (part.partId == step.partId) { pending = part; ConfirmPart(); break; }
            }
            RefreshUI();
        }
        void GuideUI()
        {
            var step = product.guidedSteps[guideIndex]; var view = ui.product.guide;
            view.title.text = $"{guideIndex + 1}/{product.guidedSteps.Length}  " + step.title.Value;
            view.description.text = step.description.Value;
            Bind(view.close, T("Quitter", "Exit"), () => { guideIndex = -1; RefreshUI(); });
            view.previous.gameObject.SetActive(guideIndex > 0); view.next.gameObject.SetActive(guideIndex < product.guidedSteps.Length - 1);
            Bind(view.previous, "←", () => StartGuide(guideIndex - 1)); Bind(view.next, "→", () => StartGuide(guideIndex + 1));
        }
    }
}

namespace Liberty3D.ProductViewer
{
    public sealed class PortraitNavigationLayout : UnityEngine.MonoBehaviour
    {
        UnityEngine.GameObject wide, portrait, wideBackground, portraitBackground;
        UIActionButton[] wideActions, portraitActions;
        bool initialized;
        void LateUpdate() => ApplyForDimensions(UnityEngine.Device.Screen.width, UnityEngine.Device.Screen.height);
        public void ApplyForDimensions(int screenWidth, int screenHeight)
        {
            if (!initialized) {
                var w = transform.Find("Navigation"); var p = transform.Find("NavigationPortrait");
                var wb = transform.Find("BackgroundNavigation"); var pb = transform.Find("BackgroundNavigationPortrait");
                if (!w || !p || !wb || !pb) return;
                wide=w.gameObject; portrait=p.gameObject; wideBackground=wb.gameObject; portraitBackground=pb.gameObject;
                wideActions=wide.GetComponentsInChildren<UIActionButton>(true);
                portraitActions=portrait.GetComponentsInChildren<UIActionButton>(true);
                if (wideActions.Length != portraitActions.Length) { UnityEngine.Debug.LogError("Portrait navigation: number of actions differs from landscape menu.", this); enabled=false; return; }
                for (int i=0; i<wideActions.Length; i++) {
                    var original=wideActions[i]; var copy=portraitActions[i];
                    if (copy.button && original.button) copy.button.onClick.AddListener(() => original.button.onClick.Invoke());
                }
                initialized=true;
            }
            bool isPortrait=screenHeight > screenWidth;
            wide.SetActive(!isPortrait); portrait.SetActive(isPortrait);
            wideBackground.SetActive(!isPortrait); portraitBackground.SetActive(isPortrait);
            if (!isPortrait) return;
            for (int i=0; i<wideActions.Length; i++) {
                var source=wideActions[i]; var target=portraitActions[i];
                if (target.gameObject.activeSelf != source.gameObject.activeSelf) target.gameObject.SetActive(source.gameObject.activeSelf);
                if (target.label && source.label && target.label.text != source.label.text) target.label.text=source.label.text;
                if (target.icon && source.icon && target.icon.sprite != source.icon.sprite) target.icon.sprite=source.icon.sprite;
                if (target.button && source.button) target.button.interactable=source.button.interactable;
            }
        }
    }
}
