using UnityEngine;

namespace Liberty3D.ProductViewer
{
    public class ProductRig : MonoBehaviour
    {
        public ProductPart[] parts = System.Array.Empty<ProductPart>();
        [Tooltip("Visible only in the assembled overview. No label or selection entry.")]
        public ProductAccessory[] accessories = System.Array.Empty<ProductAccessory>();
        public Bounds Bounds
        {
            get
            {
                var b = new Bounds(transform.position, Vector3.one * .1f);
                bool found = false;
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    if (!found) { b = renderer.bounds; found = true; } else b.Encapsulate(renderer.bounds);
                }
                return b;
            }
        }
        public void Initialize()
        {
            // Rebuild the runtime lists from the instantiated hierarchy. This repairs
            // stale serialized prefab arrays that can contain destroyed object references.
            Collect();
            foreach (var p in parts) p.Initialize();
            foreach (var a in accessories) a.Initialize();
        }
        [ContextMenu("Collect parts")]
        public void Collect() { parts = GetComponentsInChildren<ProductPart>(true); accessories = GetComponentsInChildren<ProductAccessory>(true); }
        [ContextMenu("Preview assembled pose")]
        public void PreviewAssembled() { foreach (var p in parts) if (p) { p.transform.localPosition = p.assembledPosition; p.transform.localRotation = Quaternion.Euler(p.assembledEuler); } }
        [ContextMenu("Preview exploded pose")]
        public void PreviewExploded() { foreach (var p in parts) if (p) { p.transform.localPosition = p.explodedPosition; p.transform.localRotation = Quaternion.Euler(p.explodedEuler); } }
        public void SuggestExplosion(float spacing = 2)
        {
            // Authoring proposal only: geometry visibility still requires manual review.
            var center = Vector3.zero;
            foreach (var p in parts) center += p.assembledPosition;
            center /= Mathf.Max(1, parts.Length);
            float maximum = .001f;
            foreach (var p in parts) maximum = Mathf.Max(maximum, Vector3.Distance(center, p.assembledPosition));
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i]; var delta = p.assembledPosition - center;
                var radial = delta.magnitude / maximum;
                var direction = delta.sqrMagnitude > .001f ? delta.normalized : Quaternion.Euler(0, i * 137.5f, 35) * Vector3.right;
                p.explodedPosition = p.assembledPosition + direction * spacing * (.45f + radial);
                p.explodedEuler = p.assembledEuler;
            }
        }
    }
}
