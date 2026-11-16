using UnityEngine;

namespace Liberty3D.ProductViewer
{
    public class ProductPart : MonoBehaviour
    {
        public string partId;
        public ProductInformation information = new ProductInformation();
        public Vector3 assembledPosition;
        public Vector3 assembledEuler;
        public Vector3 explodedPosition;
        public Vector3 explodedEuler;
        public Vector3 labelAnchor;
        public Vector2 labelOffset = new Vector2(110, 40);
        [Range(.3f, 1)] public float minimumZoomFactor = .55f;
        [Range(1, 4)] public float maximumZoomFactor = 2;
        ProductFade fade;
        public float Opacity => fade?.Opacity ?? 1;

        public Vector3 WorldAnchor => transform.TransformPoint(labelAnchor);
        public Bounds Bounds
        {
            get
            {
                var rs = GetComponentsInChildren<Renderer>(true);
                var bounds = new Bounds(transform.position, Vector3.one * .05f);
                if (rs.Length > 0) { bounds = rs[0].bounds; for (int i = 1; i < rs.Length; i++) bounds.Encapsulate(rs[i].bounds); }
                return bounds;
            }
        }
        public void Initialize() { fade?.Dispose(); fade = new ProductFade(gameObject); }
        public void SetOpacity(float value) { fade?.SetOpacity(value); }
        void OnDestroy() { fade?.Dispose(); }
        [ContextMenu("Capture assembled pose")]
        public void CaptureAssembled() { assembledPosition = transform.localPosition; assembledEuler = transform.localEulerAngles; }
        [ContextMenu("Capture exploded pose")]
        public void CaptureExploded() { explodedPosition = transform.localPosition; explodedEuler = transform.localEulerAngles; }
    }
}
