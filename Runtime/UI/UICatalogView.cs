using UnityEngine;
using UnityEngine.UI;
namespace Liberty3D.ProductViewer
{
    public class UICatalogView : MonoBehaviour
    {
        public RectTransform cards;
        public UIProductCard cardPrefab;
        public GameObject previewCard;
        public GridLayoutGroup grid;
        public bool autoColumns = true;
        public float preferredCardWidth = 340;
        void OnRectTransformDimensionsChange()
        {
            if (!autoColumns || !grid || !cards || !cards.parent) return;
            float w = ((RectTransform)cards.parent).rect.width - grid.padding.horizontal;
            int count = Mathf.Max(1, Mathf.FloorToInt((w + grid.spacing.x) / (preferredCardWidth + grid.spacing.x)));
            grid.constraintCount = count;
            grid.cellSize = new Vector2(Mathf.Max(100, (w - (count - 1) * grid.spacing.x) / count), grid.cellSize.y);
        }
    }
}
