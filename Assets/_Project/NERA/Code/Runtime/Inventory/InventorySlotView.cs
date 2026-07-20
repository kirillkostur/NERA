using NERA.Research;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Inventory
{
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text numberLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private LaboratoryInventoryItemDrag laboratoryDrag;

        private Color defaultBackgroundColor = Color.white;
        private bool backgroundColorCached;

        public Button Button => button;
        public Image Background => background;
        public Image Icon => icon;
        public LaboratoryInventoryItemDrag LaboratoryDrag => laboratoryDrag;

        public void SetSelected(bool selected, Color selectedColor)
        {
            CacheComponents();
            if (background != null)
            {
                background.color = selected
                    ? selectedColor
                    : defaultBackgroundColor;
            }
        }

        public void SetKeyLabel(string text, bool visible)
        {
            CacheComponents();
            if (numberLabel == null)
                return;

            numberLabel.gameObject.SetActive(visible);
            numberLabel.text = text;
        }

        private void Awake()
        {
            CacheComponents();
        }

        public void Initialize(
            int index,
            bool showQuickAccessNumber,
            Canvas rootCanvas
        )
        {
            CacheComponents();

            if (numberLabel != null)
            {
                numberLabel.gameObject.SetActive(showQuickAccessNumber);
                numberLabel.text = string.Empty;
            }

            if (laboratoryDrag != null)
            {
                laboratoryDrag.enabled = true;
                laboratoryDrag.Initialize(null, rootCanvas);
            }
        }

        private void CacheComponents()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (background == null)
                background = GetComponent<Image>();
            if (!backgroundColorCached && background != null)
            {
                defaultBackgroundColor = background.color;
                backgroundColorCached = true;
            }
            if (icon == null)
            {
                Transform iconTransform = transform.Find("Icon");
                if (iconTransform != null)
                    icon = iconTransform.GetComponent<Image>();
            }
            if (numberLabel == null)
            {
                Transform numberTransform = transform.Find("Number");
                if (numberTransform != null)
                    numberLabel = numberTransform.GetComponent<TMP_Text>();
            }
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (laboratoryDrag == null)
            {
                laboratoryDrag = GetComponent<LaboratoryInventoryItemDrag>();
                if (laboratoryDrag == null)
                    laboratoryDrag = gameObject.AddComponent<LaboratoryInventoryItemDrag>();
            }
        }
    }
}
