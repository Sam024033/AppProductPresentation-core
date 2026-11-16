using UnityEngine;
using UnityEngine.UI;

namespace Liberty3D.ProductViewer
{
    // Renders the existing Shuriken simulation as Canvas quads in Overlay mode.
    // This intentionally supports basic billboard particles (position, size,
    // rotation and color), not trails, meshes or texture-sheet animation.
    [RequireComponent(typeof(CanvasRenderer))]
    public class WelcomeParticlesGraphic : MaskableGraphic
    {
        ParticleSystem source;
        ParticleSystem.Particle[] particles;
        Texture particleTexture;
        public override Texture mainTexture => particleTexture ? particleTexture : s_WhiteTexture;

        public void Configure(ParticleSystem system, Texture texture)
        {
            source = system; particleTexture = texture; raycastTarget = false;
            SetVerticesDirty(); SetMaterialDirty();
        }

        void LateUpdate() { if (source) SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!source || !source.gameObject.activeInHierarchy) return;
            int capacity = Mathf.Clamp(source.main.maxParticles, 1, 5000);
            if (particles == null || particles.Length != capacity) particles = new ParticleSystem.Particle[capacity];
            int count = source.GetParticles(particles);
            var main = source.main;
            for (int i = 0; i < count; i++)
            {
                var particle = particles[i];
                Vector3 world;
                if (main.simulationSpace == ParticleSystemSimulationSpace.World) world = particle.position;
                else if (main.simulationSpace == ParticleSystemSimulationSpace.Custom && main.customSimulationSpace)
                    world = main.customSimulationSpace.TransformPoint(particle.position);
                else world = source.transform.TransformPoint(particle.position);
                Vector3 center = transform.InverseTransformPoint(world);
                float scale = source.transform.lossyScale.x / Mathf.Max(.00001f, transform.lossyScale.x);
                float half = particle.GetCurrentSize(source) * Mathf.Abs(scale) * .5f;
                float radians = particle.rotation * Mathf.Deg2Rad;
                float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
                var right = new Vector2(c, s) * half;
                var up = new Vector2(-s, c) * half;
                var position = new Vector2(center.x, center.y);
                Color32 tint = particle.GetCurrentColor(source) * color;
                int start = vh.currentVertCount;
                Add(vh, position - right - up, new Vector2(0, 0), tint);
                Add(vh, position - right + up, new Vector2(0, 1), tint);
                Add(vh, position + right + up, new Vector2(1, 1), tint);
                Add(vh, position + right - up, new Vector2(1, 0), tint);
                vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start + 2, start + 3, start);
            }
        }

        static void Add(VertexHelper vh, Vector2 position, Vector2 uv, Color32 color)
        {
            UIVertex vertex = UIVertex.simpleVert; vertex.position = position; vertex.uv0 = uv; vertex.color = color; vh.AddVert(vertex);
        }
    }
}
