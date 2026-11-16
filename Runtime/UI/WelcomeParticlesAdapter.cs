using UnityEngine;

namespace Liberty3D.ProductViewer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem), typeof(ParticleSystemRenderer))]
    public class WelcomeParticlesAdapter : MonoBehaviour
    {
        ParticleSystem system;
        ParticleSystemRenderer sourceRenderer;
        ParticleSystemCullingMode originalCulling;
        bool originalRendererEnabled;
        WelcomeParticlesGraphic graphic;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            system = GetComponent<ParticleSystem>(); sourceRenderer = GetComponent<ParticleSystemRenderer>();
            originalRendererEnabled = sourceRenderer.enabled;
            originalCulling = system.main.cullingMode;
            var main = system.main; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var material = sourceRenderer.sharedMaterial;
            Texture texture = material && material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material ? material.mainTexture : null;
            var go = new GameObject("Particules UI (runtime)", typeof(RectTransform), typeof(CanvasRenderer), typeof(WelcomeParticlesGraphic));
            var rect = (RectTransform)go.transform; rect.SetParent(transform.parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.one;
            graphic = go.GetComponent<WelcomeParticlesGraphic>(); graphic.Configure(system, texture);
            sourceRenderer.enabled = false;
            if (!system.isPlaying) system.Play();
        }
        void OnDisable()
        {
            if (sourceRenderer) sourceRenderer.enabled = originalRendererEnabled;
            if (system) { var main = system.main; main.cullingMode = originalCulling; }
            if (graphic) Destroy(graphic.gameObject);
            graphic = null;
        }
    }
}
