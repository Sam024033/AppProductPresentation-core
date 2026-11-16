using UnityEngine;

namespace Liberty3D.ProductViewer
{
    public enum AccessoryVisibility { AssembledOnly, AllOverviews }
    [DisallowMultipleComponent]
    [AddComponentMenu("Liberty 3D/Product Accessory (assembled only)")]
    public class ProductAccessory : MonoBehaviour
    {
        [Tooltip("Assembled Only : cache en vue eclatee. All Overviews : visible dans les deux vues d'ensemble, cache en vue detaillee.")]
        public AccessoryVisibility visibility = AccessoryVisibility.AssembledOnly;
        ProductFade fade;
        public float Opacity => fade?.Opacity ?? 1;
        public void Initialize() { fade?.Dispose(); fade = new ProductFade(gameObject); }
        public void SetOpacity(float value) { fade?.SetOpacity(value); }
        public float TargetOpacity(bool exploded, bool isolated) => isolated || (exploded && visibility == AccessoryVisibility.AssembledOnly) ? 0f : 1f;
        void OnDestroy() { fade?.Dispose(); }
    }
}
