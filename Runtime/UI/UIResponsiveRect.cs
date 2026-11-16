using System;
using UnityEngine;

namespace Liberty3D.ProductViewer
{
    [Serializable]
    public struct UIRectPose
    {
        public Vector2 anchorMin, anchorMax, pivot, position, size;
        public static UIRectPose Read(RectTransform rect) => new UIRectPose {anchorMin=rect.anchorMin, anchorMax=rect.anchorMax, pivot=rect.pivot, position=rect.anchoredPosition, size=rect.sizeDelta};
        public void Apply(RectTransform rect) { rect.anchorMin=anchorMin; rect.anchorMax=anchorMax; rect.pivot=pivot; rect.anchoredPosition=position; rect.sizeDelta=size; }
    }
    [ExecuteAlways]
    public class UIResponsiveRect : MonoBehaviour
    {
        [Tooltip("Enable only for a block that changes position between narrow and wide layouts. Edit these poses or use Capture from the component menu.")]
        public bool useProfiles = true;
        public float narrowBelow = 800;
        public UIRectPose wide, narrow;
        int current = -1;
        void OnEnable() { current = -1; }
        void OnValidate() { current = -1; }
        void Update() { ApplyIfNeeded(); }
        public void ApplyIfNeeded()
        {
            if (!useProfiles || !transform.parent) return;
            var parent = transform.parent as RectTransform; if (!parent) return;
            int choice = parent.rect.width < narrowBelow ? 1 : 0;
            if (choice == current) return;
            (choice == 1 ? narrow : wide).Apply((RectTransform)transform); current = choice;
        }
        public void ForceApply() { current = -1; ApplyIfNeeded(); }
        [ContextMenu("Capture current RectTransform as WIDE")]
        public void CaptureWide() { wide = UIRectPose.Read((RectTransform)transform); }
        [ContextMenu("Capture current RectTransform as NARROW")]
        public void CaptureNarrow() { narrow = UIRectPose.Read((RectTransform)transform); }
    }
}
