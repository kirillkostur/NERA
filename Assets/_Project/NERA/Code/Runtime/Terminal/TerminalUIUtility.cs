using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NERA.Terminal
{
    internal static class TerminalUIUtility
    {
        public static Transform Find(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = Find(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        public static T FindComponent<T>(Transform root, string name)
            where T : Component
        {
            return Find(root, name)?.GetComponent<T>();
        }

        public static Button EnsureButton(Transform root)
        {
            if (root == null)
                return null;
            Button button = root.GetComponent<Button>();
            if (button == null)
                button = root.gameObject.AddComponent<Button>();
            if (button.targetGraphic == null)
                button.targetGraphic = root.GetComponent<Graphic>();
            return button;
        }

        public static Image EnsureSlotIcon(Transform slot)
        {
            if (slot == null)
                return null;

            Transform authored = slot.Find("Icon");
            if (authored != null)
                return authored.GetComponent<Image>();

            Transform existing = slot.Find("RuntimeIcon");
            if (existing != null)
                return existing.GetComponent<Image>();

            GameObject iconObject = new GameObject(
                "RuntimeIcon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            iconObject.transform.SetParent(slot, false);
            RectTransform rect = (RectTransform)iconObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(6f, 6f);
            rect.offsetMax = new Vector2(-6f, -6f);
            Image icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;
            return icon;
        }

        public static void SetItemIcon(Image image, NERA.Items.ItemData item)
        {
            if (image == null)
                return;
            image.sprite = item != null ? item.Icon : null;
            image.color = item != null && item.Icon == null
                ? new Color(0.18f, 0.28f, 0.31f, 1f)
                : Color.white;
            image.enabled = item != null;
        }

        public static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }

        public static void ReleaseCameraTarget(Camera camera)
        {
            RenderTexture target = camera != null
                ? camera.targetTexture
                : null;
            if (target != null && target.IsCreated())
                target.Release();
        }
    }

    public sealed class UIPreviewRaycaster : MonoBehaviour, IPointerClickHandler
    {
        private RawImage rawImage;
        private Camera previewCamera;
        private Action<RaycastHit> hitHandler;

        public void Initialize(
            RawImage image,
            Camera camera,
            Action<RaycastHit> onHit)
        {
            rawImage = image;
            previewCamera = camera;
            hitHandler = onHit;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (rawImage == null || previewCamera == null)
                return;

            RectTransform rectTransform = rawImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 local))
            {
                return;
            }

            Rect rect = rectTransform.rect;
            float x = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
            float y = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
            Rect uvRect = rawImage.uvRect;
            Vector2 viewport = new Vector2(
                uvRect.x + x * uvRect.width,
                uvRect.y + y * uvRect.height);
            Ray ray = previewCamera.ViewportPointToRay(viewport);
            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    1000f,
                    previewCamera.cullingMask,
                    QueryTriggerInteraction.Collide))
            {
                hitHandler?.Invoke(hit);
            }
        }
    }
}
