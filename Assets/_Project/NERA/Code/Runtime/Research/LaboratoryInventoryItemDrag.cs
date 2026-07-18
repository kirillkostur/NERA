using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NERA.Research
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LaboratoryInventoryItemDrag : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public NERA.Items.ItemData Item { get; private set; }

        private CanvasGroup canvasGroup;
        private Canvas rootCanvas;
        private GameObject dragIcon;

        public void Initialize(NERA.Items.ItemData item, Canvas canvas)
        {
            Item = item;
            rootCanvas = canvas;
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Item == null || rootCanvas == null)
                return;

            canvasGroup.blocksRaycasts = false;
            dragIcon = new GameObject("DraggedResearchItem", typeof(RectTransform), typeof(Image));
            dragIcon.transform.SetParent(rootCanvas.transform, false);
            dragIcon.transform.SetAsLastSibling();

            Image image = dragIcon.GetComponent<Image>();
            image.sprite = Item.Icon;
            image.preserveAspect = true;
            image.color = Item.Icon != null
                ? new Color(1f, 1f, 1f, 0.9f)
                : new Color(0.18f, 0.8f, 0.92f, 0.9f);
            image.raycastTarget = false;
            dragIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(64f, 64f);
            dragIcon.transform.position = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragIcon != null)
                dragIcon.transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = true;

            if (dragIcon != null)
                Destroy(dragIcon);
        }
    }
}
