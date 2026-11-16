using UnityEngine;
using UnityEngine.UI;
namespace Liberty3D.ProductViewer
{
    public class UIInformationView : MonoBehaviour
    {
        public Text title, description;
        public UIActionButton images, website, document;
        public LayoutElement descriptionViewport;
        public float minimumTextHeight = 65;
        public float maximumTextHeightWide = 265;
        public float maximumTextHeightNarrow = 135;
        public RectTransform Motion => (RectTransform)transform;
        public Vector2 Home { get; private set; }
        bool visible;
        float progress;
        public void Reflow(bool narrow)
        {
            Canvas.ForceUpdateCanvases();
            descriptionViewport.preferredHeight = Mathf.Clamp(description.preferredHeight + 8, minimumTextHeight, narrow ? maximumTextHeightNarrow : maximumTextHeightWide);
            LayoutRebuilder.ForceRebuildLayoutImmediate(Motion);
        }
        public void SetVisible(bool value)
        {
            var layout = GetComponent<UIResponsiveRect>(); if (layout) layout.ApplyIfNeeded();
            if (!gameObject.activeSelf) { Home = Motion.anchoredPosition; progress = 0; }
            if (value && !visible) { gameObject.SetActive(true); Home = Motion.anchoredPosition; }
            visible = value;
            if (!value && !gameObject.activeSelf) return;
        }
        void Update()
        {
            progress = Mathf.MoveTowards(progress, visible ? 1 : 0, Time.unscaledDeltaTime / .28f);
            Motion.anchoredPosition = Home + Vector2.left * (Motion.rect.width + 230) * (1 - Mathf.SmoothStep(0, 1, progress));
            if (!visible && progress <= 0) { Motion.anchoredPosition = Home; gameObject.SetActive(false); }
        }
        public void ResetLayoutOrigin()
        {
            Motion.anchoredPosition = Home;
            var layout = GetComponent<UIResponsiveRect>(); if (layout) layout.ForceApply();
            Home = Motion.anchoredPosition;
        }
    }
}
