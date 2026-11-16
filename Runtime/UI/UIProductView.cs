using UnityEngine;
using UnityEngine.UI;
namespace Liberty3D.ProductViewer
{
    public class UIProductView : MonoBehaviour
    {
        public Text title, state;
        public UIActionButton home, reset, explosion, information, tour, legal, overview;
        public UIActionButton parts;
        public UIPartsMenuView partsMenu;
        public UIInformationView informationPanel;
        public UIGuideView guide;
        public RectTransform labels;
        public UIPartLabelView labelPrefab;
        public GameObject previewLabel;
        [Header("Icones selon action")]
        [Tooltip("Action visible quand les informations sont affichees.")]
        public Sprite hideInformationSprite;
        [Tooltip("Action visible quand les informations sont masquees.")]
        public Sprite showInformationSprite;
        [Tooltip("Action pour passer en vue eclatee.")]
        public Sprite explodeSprite;
        [Tooltip("Action pour revenir a la vue assemblee.")]
        public Sprite assembleSprite;
        Sprite originalInformationSprite, originalExplosionSprite;
        bool originalSpritesCaptured;

        public void UpdateActionSprites(bool informationVisible, bool exploded)
        {
            if (!originalSpritesCaptured)
            {
                originalInformationSprite = information && information.icon ? information.icon.sprite : null;
                originalExplosionSprite = explosion && explosion.icon ? explosion.icon.sprite : null;
                originalSpritesCaptured = true;
            }
            if (information && information.icon && (hideInformationSprite || showInformationSprite))
                information.icon.sprite = (informationVisible ? hideInformationSprite : showInformationSprite) ?? originalInformationSprite;
            if (explosion && explosion.icon && (explodeSprite || assembleSprite))
                explosion.icon.sprite = (exploded ? assembleSprite : explodeSprite) ?? originalExplosionSprite;
        }
    }
}
