using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Liberty3D.ProductViewer
{
    public static class ViewerUI
    {
        static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static readonly Color Ink = new Color(.9f, .94f, .97f);
        public static readonly Color Muted = new Color(.55f, .65f, .72f);
        public static readonly Color Surface = new Color(.065f, .095f, .135f, .98f);
        public static RectTransform Box(Transform parent, string name, Rect rect, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; Place(rt, rect);
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = raycast;
            return rt;
        }
        public static void Place(RectTransform rt, Rect rect)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y); rt.sizeDelta = rect.size;
        }
        public static Text Text(Transform parent, string value, Rect rect, int size = 20, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false); Place((RectTransform)go.transform, rect);
            var text = go.GetComponent<Text>(); text.font = Font; text.fontSize = size; text.color = color ?? Ink;
            text.fontStyle = style; text.text = value; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        public static Button Button(Transform parent, string value, Rect rect, UnityAction action, Color? color = null, int fontSize = 18)
        {
            var box = Box(parent, value, rect, color ?? new Color(.11f, .16f, .21f), true);
            var button = box.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
            var colors = button.colors; colors.highlightedColor = new Color(.8f, .92f, 1); colors.pressedColor = new Color(.55f, .7f, .8f); button.colors = colors;
            var label = Text(box, value, new Rect(9, 0, rect.width - 18, rect.height), fontSize);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        public static RawImage RawImage(Transform parent, Texture texture, Rect rect)
        {
            var go = new GameObject("Media", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false); Place((RectTransform)go.transform, rect);
            var image = go.GetComponent<RawImage>(); image.texture = texture; image.raycastTarget = false; return image;
        }
        public static void Sprite(Transform parent, Sprite sprite, Rect rect)
        {
            var box = Box(parent, "Image", rect, Color.white); var image = box.GetComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true;
        }
        public static void ScrollText(Transform parent, string value, Rect rect, int size = 19)
        {
            var viewport = Box(parent, "Scrollable text", rect, new Color(0, 0, 0, .001f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var label = Text(viewport, value, new Rect(0, 0, rect.width - 12, rect.height), size, Muted);
            label.rectTransform.sizeDelta = new Vector2(rect.width - 12, Mathf.Max(rect.height, label.preferredHeight + 12));
            scroll.viewport = viewport; scroll.content = label.rectTransform;
        }
    }
}
