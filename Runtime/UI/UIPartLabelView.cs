using UnityEngine;
using UnityEngine.UI;
namespace Liberty3D.ProductViewer
{
    public class UIPartLabelView : MonoBehaviour
    {
        public RectTransform badge, leader;
        public Text title;
        public CanvasGroup group;
        public PartLabel marker;
        [Tooltip("Point au niveau de la piece. S'il est vide, l'enfant direct nomme Dot est utilise.")]
        public RectTransform dot;

        public void PlaceDot(Vector2 anchor)
        {
            if (!dot) dot = transform.Find("Dot") as RectTransform;
            if (!dot || dot.parent != transform) return;
            dot.anchorMin = dot.anchorMax = new Vector2(0, 1);
            dot.pivot = new Vector2(.5f, .5f);
            dot.anchoredPosition = new Vector2(anchor.x, -anchor.y);
        }
    }
}
