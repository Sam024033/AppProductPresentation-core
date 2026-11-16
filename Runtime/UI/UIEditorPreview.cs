#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
namespace Liberty3D.ProductViewer
{
    [ExecuteAlways]
    public class UIEditorPreview : MonoBehaviour
    {
        public enum Screen {Welcome,Catalog,Product,Exploded,Part,Confirmation,Legal,Gallery,Document,PartsMenu}
        public ViewerUIRoot ui;
        public Screen screen;
        public bool narrowPreview;
        void OnEnable() { Apply(); }
        void OnValidate()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall+=()=> { if(this) Apply(); };
#endif
        }
        public void Apply()
        {
            if(!ui || Application.isPlaying) return;
            if(narrowPreview) { ui.safeArea.anchorMin=ui.safeArea.anchorMax=new Vector2(.5f,.5f); ui.safeArea.anchoredPosition=Vector2.zero; ui.safeArea.sizeDelta=new Vector2(506,900); }
            else { ui.safeArea.anchorMin=Vector2.zero; ui.safeArea.anchorMax=Vector2.one; ui.safeArea.offsetMin=ui.safeArea.offsetMax=Vector2.zero; }
            ui.welcome.gameObject.SetActive(screen==Screen.Welcome); ui.catalog.gameObject.SetActive(screen==Screen.Catalog);
            ui.product.gameObject.SetActive(screen>=Screen.Product && screen<=Screen.Part || screen==Screen.PartsMenu);
            ui.product.informationPanel.gameObject.SetActive(screen==Screen.Product || screen==Screen.Part);
            ui.product.overview.gameObject.SetActive(screen==Screen.Part); ui.product.explosion.gameObject.SetActive(screen!=Screen.Part);
            ui.product.previewLabel.SetActive(screen==Screen.Exploded);
            if(ui.product.partsMenu) ui.product.partsMenu.gameObject.SetActive(screen==Screen.PartsMenu);
            bool modal=screen>=Screen.Confirmation && screen<=Screen.Gallery; ui.dialog.gameObject.SetActive(modal);
            ui.dialog.textArea.SetActive(screen!=Screen.Gallery); ui.dialog.mediaArea.SetActive(screen==Screen.Gallery);
            ui.dialog.yes.gameObject.SetActive(screen==Screen.Confirmation); ui.dialog.no.gameObject.SetActive(screen==Screen.Confirmation);
            ui.dialog.close.gameObject.SetActive(screen!=Screen.Confirmation); ui.dialog.previous.gameObject.SetActive(screen==Screen.Gallery); ui.dialog.next.gameObject.SetActive(screen==Screen.Gallery);
            ui.document.gameObject.SetActive(screen==Screen.Document);
        }
    }
}
