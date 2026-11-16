using UnityEngine;
using UnityEngine.UI;
using Screen = UnityEngine.Device.Screen;
namespace Liberty3D.ProductViewer
{
    public class ViewerUIRoot : MonoBehaviour
    {
        public Canvas canvas;
        public RectTransform safeArea;
        public Text manufacturer;
        public Image logo;
        public UIActionButton language;
        public UIWelcomeView welcome;
        public UICatalogView catalog;
        public UIProductView product;
        public UIDialogView dialog;
        public UIDocumentView document;
        public void ApplySafeArea()
        {
            var area = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            if (area.width <= 0 || area.height <= 0) area = new Rect(0, 0, Screen.width, Screen.height);
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
    }
}
