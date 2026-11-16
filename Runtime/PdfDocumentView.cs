using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Liberty3D.ProductViewer
{
    public class PdfDocumentView : MonoBehaviour
    {
        public event Action Closed;
        public bool IsOpen { get; private set; }
        UIDocumentView view;
        Texture2D texture;
        int page, pages, lastWidth, lastHeight;
        float scale = 1;
        bool rendering;
        string localPath;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass bridge;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void LibertyPdfLoad(byte[] data, int length, string receiver);
        [DllImport("__Internal")] static extern void LibertyPdfRender(int page, int width);
        [DllImport("__Internal")] static extern void LibertyPdfClose();
#endif
        public void Configure(UIDocumentView prefabInstance) { view = prefabInstance; view.gameObject.SetActive(false); }
        string T(string fr, string en) => ViewerLanguage.Pick(fr,en);
        public void Open(TextAsset document)
        {
            if (!document || IsOpen || !view) return;
            IsOpen = true; page = 0; pages = 0; scale = 1;
            lastWidth = Screen.width; lastHeight = Screen.height;
            view.gameObject.SetActive(true); view.image.enabled = false; view.toolbar.SetActive(true); view.openExternal.gameObject.SetActive(false);
            view.message.text = T("Chargement du document…", "Loading document…"); view.page.text = "";
            view.back.Bind(T("← Retour", "← Back"), Close);
            view.previous.Bind("←", () => ChangePage(-1)); view.next.Bind("→", () => ChangePage(1));
            view.zoomIn.Bind("+", () => { if (!rendering) { scale = Mathf.Min(3,scale+.5f); Render(); } });
            view.zoomOut.Bind("−", () => { if (!rendering) { scale = Mathf.Max(1,scale-.5f); Render(); } });
#if UNITY_WEBGL && !UNITY_EDITOR
            LibertyPdfLoad(document.bytes, document.bytes.Length, gameObject.name);
#else
            localPath = Path.Combine(Application.temporaryCachePath,"liberty-document.pdf");
            File.WriteAllBytes(localPath,document.bytes);
#if UNITY_ANDROID && !UNITY_EDITOR
            try { bridge = new AndroidJavaClass("com.liberty3d.viewer.PdfBridge"); pages=bridge.CallStatic<int>("open",localPath); Render(); }
            catch(Exception error) { Error(error.Message); }
#else
            view.toolbar.SetActive(false); view.openExternal.gameObject.SetActive(true);
            view.message.text=T("Éditeur Windows : ouvrir le PDF original. Lecteur intégré sur Android et Web.","Windows editor: open original PDF. Embedded reader on Android and Web.");
            view.openExternal.Bind(T("Ouvrir le PDF original","Open original PDF"),()=>Application.OpenURL(new Uri(localPath).AbsoluteUri));
#endif
#endif
        }
        void Update()
        {
            if(!IsOpen || rendering || (lastWidth==Screen.width && lastHeight==Screen.height)) return;
            lastWidth=Screen.width; lastHeight=Screen.height;
            if(pages>0) Render();
        }
        void ChangePage(int delta) { if(rendering || pages==0) return; page=Mathf.Clamp(page+delta,0,pages-1); Render(); }
        void Render()
        {
            if(!IsOpen || pages==0 || rendering) return;
            Canvas.ForceUpdateCanvases(); rendering=true;
            int renderWidth=Mathf.Clamp(Mathf.RoundToInt(view.viewport.rect.width * view.GetComponentInParent<Canvas>().scaleFactor * scale),512,2400);
#if UNITY_WEBGL && !UNITY_EDITOR
            LibertyPdfRender(page,renderWidth);
#elif UNITY_ANDROID && !UNITY_EDITOR
            try { SetPage(bridge.CallStatic<byte[]>("render",page,renderWidth)); }
            catch(Exception error) { Error(error.Message); }
#else
            rendering=false;
#endif
        }
        void SetPage(byte[] bytes)
        {
            if(!IsOpen) return;
            if(texture) Destroy(texture);
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false); texture.LoadImage(bytes);
            view.image.texture=texture; view.image.enabled=true;
            float w=view.viewport.rect.width*scale;
            view.image.rectTransform.sizeDelta=new Vector2(w,w*texture.height/texture.width);
            view.image.rectTransform.anchoredPosition=Vector2.zero;
            view.page.text=$"{page+1} / {pages}"; view.message.text=""; rendering=false;
            view.previous.button.interactable=page>0; view.next.button.interactable=page<pages-1;
        }
        public void WebLoaded(string count) { if(!IsOpen) return; pages=int.Parse(count); Render(); }
        public void WebPage(string base64) { if(IsOpen) { try { SetPage(Convert.FromBase64String(base64)); } catch(Exception e) { Error(e.Message); } } }
        public void WebError(string message) { if(IsOpen) Error(message); }
        void Error(string message) { rendering=false; view.message.text="PDF: "+message; }
        public void Close()
        {
            if(!IsOpen) return; IsOpen=false;
#if UNITY_WEBGL && !UNITY_EDITOR
            LibertyPdfClose();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            if(bridge!=null) { bridge.CallStatic("close"); bridge.Dispose(); bridge=null; }
#endif
            if(view) view.gameObject.SetActive(false);
            if(texture) Destroy(texture); rendering=false; pages=0; Closed?.Invoke();
        }
        void OnDestroy() { Close(); }
    }
}
