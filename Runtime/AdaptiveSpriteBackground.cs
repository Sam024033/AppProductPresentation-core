using UnityEngine;
using UnityEngine.UI;

namespace Liberty3D.ProductViewer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("Liberty 3D/Adaptive Sprite Background")]
    public class AdaptiveSpriteBackground : MonoBehaviour
    {
        [Tooltip("Camera du ProductViewer. Si vide, utilise Main Camera.")]
        public Camera targetCamera;
        [Tooltip("Sprite optionnel pour paysage. Si vide, conserve le sprite existant.")]
        public Sprite landscapeSprite;
        [Tooltip("Sprite optionnel pour portrait. Si vide, utilise le sprite paysage ou le sprite existant.")]
        public Sprite portraitSprite;
        [Tooltip("Distance du fond devant la camera. Le fond doit rester derriere le produit et avant le plan Far.")]
        [Min(.1f)] public float distance = 500f;

        SpriteRenderer spriteRenderer;
        Sprite originalSprite;
        Transform originalParent;
        Vector3 originalPosition, originalScale;
        Quaternion originalRotation;
        AspectRatioFitter fitter;
        bool fitterWasEnabled, initialized;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            spriteRenderer = GetComponent<SpriteRenderer>();
            originalSprite = spriteRenderer.sprite;
            originalParent = transform.parent;
            originalPosition = transform.localPosition; originalRotation = transform.localRotation; originalScale = transform.localScale;
            fitter = GetComponent<AspectRatioFitter>();
            if (fitter) { fitterWasEnabled = fitter.enabled; fitter.enabled = false; }
            // UI canvas scaling must not distort the world-space sprite.
            transform.SetParent(null, true);
            initialized = true;
            UpdateBackground();
        }

        void LateUpdate() { if (initialized) UpdateBackground(); }

        void UpdateBackground()
        {
            var cam = targetCamera ? targetCamera : Camera.main;
            if (!cam || !spriteRenderer) return;
            bool portrait = cam.aspect < 1f;
            var chosen = portrait && portraitSprite ? portraitSprite : landscapeSprite ? landscapeSprite : originalSprite;
            if (spriteRenderer.sprite != chosen) spriteRenderer.sprite = chosen;
            if (!chosen) return;
            float depth = Mathf.Clamp(distance, cam.nearClipPlane + .01f, Mathf.Max(cam.nearClipPlane + .01f, cam.farClipPlane - .01f));
            Vector3 lower = cam.ViewportToWorldPoint(new Vector3(0, 0, depth));
            Vector3 upper = cam.ViewportToWorldPoint(new Vector3(1, 1, depth));
            float width = Vector3.Dot(upper - lower, cam.transform.right);
            float height = Vector3.Dot(upper - lower, cam.transform.up);
            Vector2 size = chosen.bounds.size;
            if (size.x <= .0001f || size.y <= .0001f) return;
            float scale = Mathf.Max(Mathf.Abs(width) / size.x, Mathf.Abs(height) / size.y);
            transform.rotation = cam.transform.rotation;
            transform.localScale = new Vector3(scale, scale, 1);
            transform.position = cam.ViewportToWorldPoint(new Vector3(.5f, .5f, depth)) - transform.rotation * Vector3.Scale(chosen.bounds.center, transform.localScale);
        }

        void OnDisable()
        {
            if (!initialized) return;
            transform.SetParent(originalParent, false);
            transform.localPosition = originalPosition; transform.localRotation = originalRotation; transform.localScale = originalScale;
            if (spriteRenderer) spriteRenderer.sprite = originalSprite;
            if (fitter) fitter.enabled = fitterWasEnabled;
            initialized = false;
        }
    }
}
