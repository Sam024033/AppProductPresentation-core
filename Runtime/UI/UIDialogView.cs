using UnityEngine;
using UnityEngine.UI;
namespace Liberty3D.ProductViewer
{
    public class UIDialogView : MonoBehaviour
    {
        public Text title, description, page;
        public GameObject textArea, mediaArea;
        public Image image;
        public UIActionButton yes, no, previous, next, close;
    }
}
