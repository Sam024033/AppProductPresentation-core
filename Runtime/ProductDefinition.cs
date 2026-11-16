using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Liberty3D.ProductViewer
{
    [Serializable]
    public class ProductText
    {
        public LocalizedString localized = new LocalizedString();
        [TextArea] public string french;
        [TextArea] public string english;
        [NonSerialized] string pendingLocale;
        public string Value
        {
            get
            {
                if (!localized.IsEmpty && LocalizationSettings.InitializationOperation.IsDone)
                {
                    // Never WaitForCompletion: synchronous Addressables loading is unsupported on WebGL.
                    var operation = LocalizationSettings.StringDatabase.GetTableAsync(localized.TableReference);
                    if (operation.IsDone && operation.Result != null)
                    {
                        var reference = localized.TableEntryReference;
                        var entry = reference.KeyId != 0 ? operation.Result.GetEntry(reference.KeyId) : operation.Result.GetEntry(reference.Key);
                        if (entry != null) return entry.LocalizedValue;
                    }
                    else
                    {
                        string locale = ViewerLanguage.English ? "en" : "fr";
                        if (pendingLocale != locale) { pendingLocale = locale; operation.Completed += _ => ViewerLanguage.Notify(); }
                    }
                }
                return ViewerLanguage.English && !string.IsNullOrWhiteSpace(english) ? english : french;
            }
        }
        public ProductText() { }
        public ProductText(string fr, string en) { french = fr; english = en; }
    }

    public static class ViewerLanguage
    {
        public static bool English { get; private set; }
        public static event Action Changed;
        public static void Notify() { Changed?.Invoke(); }
        public static void Set(bool english)
        {
            English = english;
            PlayerPrefs.SetInt("Liberty3D.Language", english ? 1 : 0);
            var locale = LocalizationSettings.AvailableLocales.GetLocale(english ? "en" : "fr");
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
            Changed?.Invoke();
        }
        public static string Pick(string fr, string en) => English ? en : fr;
    }

    [Serializable]
    public class ProductInformation
    {
        public ProductText title = new ProductText();
        public ProductText description = new ProductText();
        public Sprite[] images = Array.Empty<Sprite>();
        [Tooltip("Optional HTTPS URL. Empty hides the button.")]
        public string webUrl;
        [Tooltip("Import PDFs as .bytes, then assign here. Shared fallback is used for either language.")]
        public TextAsset sharedPdf;
        public TextAsset frenchPdf;
        public TextAsset englishPdf;
        public TextAsset Pdf => SelectPdf(ViewerLanguage.English);
        public TextAsset SelectPdf(bool english)
        {
            var preferred = english ? englishPdf : frenchPdf;
            if (preferred != null) return preferred;
            if (sharedPdf != null) return sharedPdf;
            return english ? frenchPdf : englishPdf;
        }
    }

    [Serializable]
    public class GuidedStep
    {
        public ProductText title = new ProductText();
        public ProductText description = new ProductText();
        public bool exploded;
        [Tooltip("Empty: whole product. Otherwise exact part ID.")]
        public string partId;
        public Vector3 viewEuler = new Vector3(15, -25, 0);
    }

    [CreateAssetMenu(menuName = "Liberty 3D/Product", fileName = "Product")]
    public class ProductDefinition : ScriptableObject
    {
        public string productId;
        public ProductInformation information = new ProductInformation();
        public Sprite thumbnail;
        public ProductRig prefab;
        public Vector3 initialViewEuler = new Vector3(12, -25, 0);
        [Header("Cadrage initial")]
        [Tooltip("Distance de cadrage en vue assemblee : 1 = automatique, 0.8 = camera 20% plus proche.")]
        [Range(.5f, 2f)] public float assembledFraming = 1f;
        [Tooltip("Distance de cadrage en vue eclatee : 1 = automatique, 0.8 = camera 20% plus proche.")]
        [Range(.5f, 2f)] public float explodedFraming = 1f;
        [Range(0, 30)] public float autoRotationSpeed = 12;
        [Range(.3f, 1)] public float minimumZoomFactor = .5f;
        [Range(1, 4)] public float maximumZoomFactor = 2;
        public GuidedStep[] guidedSteps = Array.Empty<GuidedStep>();
    }
}
