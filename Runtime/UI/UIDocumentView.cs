using UnityEngine;
using UnityEngine.UI;
namespace Liberty3D.ProductViewer
{
    public class UIDocumentView : MonoBehaviour
    {
        public UIActionButton back, previous, next, zoomIn, zoomOut, openExternal;
        public Text page, message;
        public RawImage image;
        public RectTransform viewport;
        public GameObject toolbar;
    }
}
