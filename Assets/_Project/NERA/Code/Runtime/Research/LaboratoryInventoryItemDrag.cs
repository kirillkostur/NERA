using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NERA.Inventory;

namespace NERA.Research
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LaboratoryInventoryItemDrag : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public NERA.Items.ItemData Item { get; private set; }
        public InventorySlotGroup SourceGroup { get; private set; }
        public int SourceIndex { get; private set; }
        public bool IsLaboratorySource { get; private set; }
        public bool IsChargingSource { get; private set; }
        public bool IsUpgradeSource { get; private set; }
        public bool IsStationStorageSource { get; private set; }
        public event Action<LaboratoryInventoryItemDrag> InteractionStarted;

        private CanvasGroup canvasGroup;
        private Canvas rootCanvas;
        private GameObject dragIcon;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Initialize(
            NERA.Items.ItemData item,
            Canvas canvas,
            InventorySlotGroup sourceGroup = InventorySlotGroup.Backpack,
            int sourceIndex = -1,
            bool isLaboratorySource = false,
            bool isChargingSource = false,
            bool isStationStorageSource = false,
            bool isUpgradeSource = false
        )
        {
            Item = item;
            rootCanvas = canvas;
            SourceGroup = sourceGroup;
            SourceIndex = sourceIndex;
            IsLaboratorySource = isLaboratorySource;
            IsChargingSource = isChargingSource;
            IsUpgradeSource = isUpgradeSource;
            IsStationStorageSource = isStationStorageSource;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            InteractionStarted?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Item == null || rootCanvas == null)
                return;

            ClearDragVisual();
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
            ClearDragVisual();
        }

        private void OnDisable()
        {
            ClearDragVisual();
        }

        private void ClearDragVisual()
        {
            if (canvasGroup != null)
                canvasGroup.blocksRaycasts = true;

            if (dragIcon != null)
            {
                Destroy(dragIcon);
                dragIcon = null;
            }
        }
    }
}
