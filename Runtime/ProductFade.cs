using UnityEngine;

namespace Liberty3D.ProductViewer
{
    // Per-instance materials only: imported/shared materials are never edited.
    public sealed class ProductFade
    {
        readonly Renderer[] renderers;
        readonly Collider[] colliders;
        readonly bool[] rendererEnabled, colliderEnabled;
        readonly Material[][] originals, fades;
        readonly float[][] alphas;
        public float Opacity { get; private set; } = 1;

        public ProductFade(GameObject root)
        {
            renderers = root.GetComponentsInChildren<Renderer>(true);
            colliders = root.GetComponentsInChildren<Collider>(true);
            rendererEnabled = new bool[renderers.Length]; colliderEnabled = new bool[colliders.Length];
            originals = new Material[renderers.Length][]; fades = new Material[renderers.Length][];
            alphas = new float[renderers.Length][];
            for (int i = 0; i < colliders.Length; i++) colliderEnabled[i] = colliders[i].enabled;
            for (int i = 0; i < renderers.Length; i++)
            {
                rendererEnabled[i] = renderers[i].enabled;
                originals[i] = renderers[i].sharedMaterials;
                fades[i] = new Material[originals[i].Length]; alphas[i] = new float[originals[i].Length];
                for (int j = 0; j < originals[i].Length; j++)
                {
                    if (!originals[i][j]) continue;
                    var m = new Material(originals[i][j]); fades[i][j] = m;
                    string color = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
                    alphas[i][j] = color != null ? m.GetColor(color).a : 1;
                    if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1);
                    if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0);
                    if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 5);
                    if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 10);
                    if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", 1);
                    if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", 10);
                    if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.DisableKeyword("_ALPHAMODULATE_ON");
                    m.SetShaderPassEnabled("ShadowCaster", false);
                    m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
                }
            }
        }

        public void SetOpacity(float value)
        {
            value = Mathf.Clamp01(value);
            bool fade = value < .999f, previousFade = Opacity < .999f;
            Opacity = value;
            for (int i = 0; i < colliders.Length; i++) if (colliders[i]) colliders[i].enabled = colliderEnabled[i] && value > .99f;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i]) continue;
                renderers[i].enabled = rendererEnabled[i] && value > .001f;
                if (fade != previousFade) renderers[i].sharedMaterials = fade ? fades[i] : originals[i];
                if (!fade) continue;
                for (int j = 0; j < fades[i].Length; j++)
                {
                    var m = fades[i][j]; if (!m) continue;
                    string property = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
                    if (property == null) continue;
                    var color = m.GetColor(property); color.a = alphas[i][j] * value; m.SetColor(property, color);
                }
            }
        }

        public void Dispose()
        {
            SetOpacity(1);
            foreach (var list in fades) foreach (var material in list) if (material)
            {
                if (Application.isPlaying) Object.Destroy(material); else Object.DestroyImmediate(material);
            }
        }
    }
}
