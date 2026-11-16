using UnityEngine;
using UnityEngine.UI;
namespace Liberty3D.ProductViewer
{
    public class UIStaticText : MonoBehaviour
    {
        public Text target;
        [TextArea] public string french, english;
        void OnEnable() { ViewerLanguage.Changed += Refresh; Refresh(); }
        void OnDisable() { ViewerLanguage.Changed -= Refresh; }
        public void Refresh() { if (target) target.text = ViewerLanguage.Pick(french, english); }
    }
}
