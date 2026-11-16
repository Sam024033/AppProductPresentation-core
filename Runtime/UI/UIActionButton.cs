using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Liberty3D.ProductViewer
{
    [ExecuteAlways]
    public class UIActionButton : MonoBehaviour
    {
        public Button button;
        public Text label;
        public Image icon;
        public bool hideEmptyIcon = true;
        UnityAction boundAction;
        public void Bind(string text, UnityAction action)
        {
            label.text = text;
            if (boundAction != null) button.onClick.RemoveListener(boundAction);
            boundAction = action;
            if (action != null) button.onClick.AddListener(action);
        }
        void Update()
        {
            if (icon && hideEmptyIcon && icon.gameObject.activeSelf != (icon.sprite != null)) icon.gameObject.SetActive(icon.sprite != null);
        }
    }
}
