using UnityEngine;
using UnityEngine.UI;
namespace Liberty3D.ProductViewer
{
    public class UIPartsMenuView : MonoBehaviour
    {
        public Text title;
        public RectTransform content;
        public ScrollRect scroll;
        public UIActionButton rowPrefab, close;
        void Awake()
        {
            if (!scroll || scroll.verticalScrollbar || !scroll.viewport) return;
            var track = new GameObject("Scrollbar (runtime)", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var rect = (RectTransform)track.transform;
            rect.SetParent(scroll.viewport.parent, false);
            rect.anchorMin = new Vector2(1, 0); rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, .5f); rect.offsetMin = new Vector2(-18, scroll.viewport.offsetMin.y);
            rect.offsetMax = new Vector2(-8, scroll.viewport.offsetMax.y);
            track.GetComponent<Image>().color = new Color(.25f, .31f, .37f, .8f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(rect, false); handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
            var handleImage = handle.GetComponent<Image>(); handleImage.color = new Color(.65f, .75f, .82f, 1);
            var bar = track.GetComponent<Scrollbar>(); bar.handleRect = handleRect; bar.targetGraphic = handleImage;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
    }
}
