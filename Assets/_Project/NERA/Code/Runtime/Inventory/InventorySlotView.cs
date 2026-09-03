using NERA.Research;
using NERA.Items;
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
        private Image anomalyContainerIcon;
        private LaboratoryInventoryItemDrag anomalyContainerDrag;

        private Color defaultBackgroundColor = Color.white;
        private bool backgroundColorCached;

        public Button Button => button;
        public Image Background => background;
        public Image Icon => icon;
        public LaboratoryInventoryItemDrag LaboratoryDrag => laboratoryDrag;
        public LaboratoryInventoryItemDrag AnomalyContainerDrag =>
            anomalyContainerDrag;

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

        public void SetAnomalyContainer(
            ItemData container,
            ItemData displayedContent,
            Canvas rootCanvas,
            InventorySlotGroup ownerGroup,
            int ownerIndex)
        {
            EnsureAnomalyContainerView();
            bool visible = container != null;
            anomalyContainerIcon.gameObject.SetActive(visible);
            if (!visible)
                return;

            ItemData display = displayedContent != null
                ? displayedContent
                : container;
            anomalyContainerIcon.sprite = display.Icon;
            anomalyContainerIcon.color = display.Icon != null
                ? Color.white
                : new Color(0.18f, 0.8f, 0.92f, 0.95f);
            anomalyContainerDrag.Initialize(
                container,
                rootCanvas,
                ownerGroup,
                ownerIndex,
                false,
                false,
                false,
                true);
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

        private void EnsureAnomalyContainerView()
        {
            if (anomalyContainerIcon != null &&
                anomalyContainerDrag != null)
            {
                return;
            }

            Transform existing = transform.Find("AnomalyContainerAttachment");
            GameObject view = existing != null
                ? existing.gameObject
                : new GameObject(
                    "AnomalyContainerAttachment",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup),
                    typeof(LaboratoryInventoryItemDrag));
            if (existing == null)
                view.transform.SetParent(transform, false);

            RectTransform rect = view.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.56f, 0.02f);
            rect.anchorMax = new Vector2(0.98f, 0.44f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            anomalyContainerIcon = view.GetComponent<Image>();
            anomalyContainerIcon.preserveAspect = true;
            anomalyContainerIcon.raycastTarget = true;
            anomalyContainerDrag =
                view.GetComponent<LaboratoryInventoryItemDrag>();
            view.SetActive(false);
        }
    }
}
