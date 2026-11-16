using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Liberty3D.ProductViewer.Editor
{
    public static class ViewerValidator
    {
        [MenuItem("Tools/Liberty 3D/Validate all catalogs %#k")]
        public static void Validate()
        {
            var messages = new List<string>(); int errors = 0, products = 0;
            void Error(string value) { errors++; messages.Add("ERROR " + value); }
            foreach (string guid in AssetDatabase.FindAssets("t:ViewerCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<ViewerCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                if (catalog.products == null || catalog.products.Length == 0) { Error(catalog.name + ": no products"); continue; }
                var ids = new HashSet<string>();
                foreach (var product in catalog.products)
                {
                    if (!product) { Error(catalog.name + ": missing product"); continue; } products++;
                    if (string.IsNullOrWhiteSpace(product.productId) || !ids.Add(product.productId)) Error("Missing/duplicate product ID: " + product.name);
                    CheckInfo(product.information, product.name, messages, Error);
                    if (!product.thumbnail) messages.Add("WARNING " + product.name + ": no thumbnail");
                    if (!product.prefab || product.prefab.parts == null || product.prefab.parts.Length == 0) { Error(product.name + ": missing prefab/parts"); continue; }
                    var partIds = new HashSet<string>(); var transforms = new HashSet<Transform>();
                    foreach (var part in product.prefab.parts)
                    {
                        if (!part) { Error(product.name + ": null part"); continue; }
                        if (string.IsNullOrWhiteSpace(part.partId) || !partIds.Add(part.partId)) Error(product.name + ": duplicate/missing part ID " + part.partId);
                        if (!transforms.Add(part.transform)) Error("Duplicate object: " + part.name);
                        if (part.transform.parent != product.prefab.transform) Error(part.name + ": part root must be a direct child of ProductRig");
                        if (!part.GetComponentInChildren<Collider>()) Error(part.name + ": missing selection collider");
                        if (!part.GetComponentInChildren<Renderer>()) Error(part.name + ": missing geometry");
                        if (part.assembledPosition == part.explodedPosition) messages.Add("INFO " + part.name + ": stationary during explosion");
                        CheckInfo(part.information, part.name, messages, Error);
                    }
                    foreach (var step in product.guidedSteps) if (!string.IsNullOrEmpty(step.partId) && !partIds.Contains(step.partId)) Error("Guided tour references missing part: " + step.partId);
                    var accessorySet = new HashSet<ProductAccessory>();
                    foreach (var accessory in product.prefab.GetComponentsInChildren<ProductAccessory>(true))
                        if (product.prefab.accessories == null || Array.IndexOf(product.prefab.accessories, accessory) < 0) Error(accessory.name + ": accessory not registered in ProductRig");
                    foreach (var accessory in product.prefab.accessories ?? Array.Empty<ProductAccessory>())
                    {
                        if (!accessory || !accessorySet.Add(accessory)) { Error(product.name + ": missing/duplicate accessory"); continue; }
                        if (accessory.transform.parent != product.prefab.transform) Error(accessory.name + ": accessory root must be a direct child of ProductRig");
                        if (accessory.GetComponentInChildren<ProductPart>(true) || accessory.GetComponentInParent<ProductPart>()) Error(accessory.name + ": accessory overlaps a selectable part");
                    }
                }
            }
            string report = $"Liberty 3D validation · {products} product(s) · {errors} error(s)\n" + string.Join("\n", messages);
            File.WriteAllText("Liberty3D-validation.txt", report);
            if (errors > 0) Debug.LogError(report); else Debug.Log(report);
        }
        static void CheckInfo(ProductInformation info, string context, List<string> messages, Action<string> error)
        {
            if (string.IsNullOrWhiteSpace(info.title.french)) error(context + ": missing French title");
            if (string.IsNullOrWhiteSpace(info.title.english)) messages.Add("WARNING " + context + ": English title missing");
            if (string.IsNullOrWhiteSpace(info.description.english)) messages.Add("WARNING " + context + ": English description missing");
            if (!string.IsNullOrEmpty(info.webUrl) && (!Uri.TryCreate(info.webUrl, UriKind.Absolute, out var url) || (url.Scheme != "https" && url.Scheme != "http"))) error(context + ": invalid web URL");
            foreach (var pdf in new[] {info.sharedPdf, info.frenchPdf, info.englishPdf})
                if (pdf && (pdf.bytes.Length < 5 || Encoding.ASCII.GetString(pdf.bytes, 0, 5) != "%PDF-")) error(context + ": invalid PDF " + pdf.name);
            if (info.SelectPdf(true) && info.SelectPdf(true) == info.SelectPdf(false)) messages.Add("INFO " + context + ": same PDF used for FR and EN");
        }
    }
}
