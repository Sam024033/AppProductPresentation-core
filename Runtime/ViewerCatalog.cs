using UnityEngine;

namespace Liberty3D.ProductViewer
{
    [CreateAssetMenu(menuName = "Liberty 3D/Manufacturer catalog", fileName = "Catalog")]
    public class ViewerCatalog : ScriptableObject
    {
        public string manufacturer = "LIBERTY 3D";
        public Sprite logo;
        [Tooltip("Editable UI prefab set. Duplicate or create a prefab variant per manufacturer.")]
        public ViewerUIRoot uiPrefab;
        public Color accent = new Color(.15f, .82f, .72f);
        public Color background = new Color(.035f, .055f, .085f);
        public ProductText welcome = new ProductText("La précision, sous tous les angles.", "Precision, from every angle.");
        public ProductText legal = new ProductText("Démonstration technique Liberty 3D.\n\nRemplacer ce texte par les mentions du fabricant avant diffusion.", "Liberty 3D technical demonstration.\n\nReplace with the manufacturer's legal notice before release.");
        public ProductDefinition[] products;
    }
}
