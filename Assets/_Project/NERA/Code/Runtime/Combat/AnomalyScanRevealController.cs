using System.Collections.Generic;
using NERA.Enemies;
using NERA.Interaction;
using NERA.Items;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Combat
{
    [DisallowMultipleComponent]
    public sealed class AnomalyScanRevealController : MonoBehaviour
    {
        private sealed class RevealMarker
        {
            public Transform Target;
            public Vector3 LocalOffset;
            public Image Image;
        }

        private readonly List<RevealMarker> markers =
            new List<RevealMarker>();
        private Canvas canvas;
        private RectTransform markerRoot;
        private Camera worldCamera;
        private float revealEndsAt;

        public int ActiveMarkerCount => markers.Count;

        public int Reveal(
            Vector3 center,
            float radius,
            float duration,
            LayerMask affectedLayers,
            Color color)
        {
            ClearMarkers();

            Collider[] hits = Physics.OverlapSphere(
                center,
                Mathf.Max(0.1f, radius),
                affectedLayers,
                QueryTriggerInteraction.Collide);
            HashSet<Transform> revealed = new HashSet<Transform>();

            foreach (Collider hit in hits)
            {
                if (hit == null || hit.transform.IsChildOf(transform))
                    continue;

                Transform target = ResolveRevealTarget(hit);
                if (target == null || !revealed.Add(target))
                    continue;

                AddMarker(target, hit.bounds.center, color);
            }

            revealEndsAt =
                Time.unscaledTime + Mathf.Max(0.1f, duration);
            if (canvas != null)
                canvas.enabled = markers.Count > 0;
            return markers.Count;
        }

        private void Update()
        {
            if (markers.Count == 0)
                return;

            if (Time.unscaledTime >= revealEndsAt)
            {
                ClearMarkers();
                return;
            }

            if (worldCamera == null)
                worldCamera = Camera.main;

            for (int index = markers.Count - 1; index >= 0; index--)
            {
                RevealMarker marker = markers[index];
                if (marker.Target == null)
                {
                    RemoveMarkerAt(index);
                    continue;
                }

                UpdateMarker(marker);
            }

            if (canvas != null)
                canvas.enabled = markers.Count > 0;
        }

        private Transform ResolveRevealTarget(Collider hit)
        {
            IOEnemyController enemy =
                hit.GetComponentInParent<IOEnemyController>();
            if (enemy != null && enemy.IsAlive)
                return enemy.transform;

            WorldItem item = hit.GetComponentInParent<WorldItem>();
            if (item != null)
                return item.transform;

            MonoBehaviour[] behaviours =
                hit.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IAnomalyElectronic)
                    return behaviour.transform;
            }

            BaseInteractable interactable =
                hit.GetComponentInParent<BaseInteractable>();
            return interactable != null
                ? interactable.InteractionTransform
                : null;
        }

        private void AddMarker(
            Transform target,
            Vector3 worldPoint,
            Color color)
        {
            EnsureCanvas();

            GameObject markerObject =
                new GameObject("AnomalyScanMarker", typeof(RectTransform));
            markerObject.transform.SetParent(markerRoot, false);

            RectTransform rect =
                markerObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(14f, 14f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Image image = markerObject.AddComponent<Image>();
            image.color = new Color(color.r, color.g, color.b, 0.95f);
            image.raycastTarget = false;

            Outline outline = markerObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;

            markers.Add(new RevealMarker
            {
                Target = target,
                LocalOffset = target.InverseTransformPoint(worldPoint),
                Image = image
            });
        }

        private void UpdateMarker(RevealMarker marker)
        {
            if (marker.Image == null || worldCamera == null)
            {
                if (marker.Image != null)
                    marker.Image.enabled = false;
                return;
            }

            Vector3 worldPoint =
                marker.Target.TransformPoint(marker.LocalOffset);
            Vector3 screenPoint =
                worldCamera.WorldToScreenPoint(worldPoint);
            bool inView =
                screenPoint.z > 0f &&
                screenPoint.x >= 0f &&
                screenPoint.x <= Screen.width &&
                screenPoint.y >= 0f &&
                screenPoint.y <= Screen.height;

            marker.Image.enabled = inView;
            if (!inView)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    markerRoot,
                    screenPoint,
                    null,
                    out Vector2 localPoint))
            {
                marker.Image.rectTransform.anchoredPosition = localPoint;
            }
        }

        private void EnsureCanvas()
        {
            if (canvas != null)
                return;

            GameObject canvasObject =
                new GameObject("AnomalyScanOverlay", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject rootObject =
                new GameObject("Markers", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            markerRoot = rootObject.GetComponent<RectTransform>();
            markerRoot.anchorMin = Vector2.zero;
            markerRoot.anchorMax = Vector2.one;
            markerRoot.offsetMin = Vector2.zero;
            markerRoot.offsetMax = Vector2.zero;
        }

        private void ClearMarkers()
        {
            for (int index = markers.Count - 1; index >= 0; index--)
                RemoveMarkerAt(index);

            if (canvas != null)
                canvas.enabled = false;
        }

        private void RemoveMarkerAt(int index)
        {
            RevealMarker marker = markers[index];
            if (marker.Image != null)
                Destroy(marker.Image.gameObject);
            markers.RemoveAt(index);
        }

        private void OnDisable()
        {
            ClearMarkers();
        }
    }
}
