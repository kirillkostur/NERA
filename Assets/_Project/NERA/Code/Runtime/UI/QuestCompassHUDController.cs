using System;
using System.Collections.Generic;
using NERA.Navigation;
using NERA.Player;
using NERA.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.UI
{
    public static class QuestCompassMath
    {
        public static float SignedHorizontalAngle(
            Transform cameraTransform,
            Vector3 targetPosition)
        {
            if (cameraTransform == null)
                return 0f;

            Vector3 forward = Vector3.ProjectOnPlane(
                cameraTransform.forward,
                Vector3.up);
            Vector3 direction = Vector3.ProjectOnPlane(
                targetPosition - cameraTransform.position,
                Vector3.up);
            if (forward.sqrMagnitude < 0.0001f ||
                direction.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            return Vector3.SignedAngle(forward, direction, Vector3.up);
        }

        public static float CalculateCompassX(
            float signedAngle,
            float visibleHalfAngle,
            float halfWidth)
        {
            float safeAngle = Mathf.Max(1f, visibleHalfAngle);
            float safeWidth = Mathf.Max(0f, halfWidth);
            return Mathf.Clamp(
                signedAngle / safeAngle * safeWidth,
                -safeWidth,
                safeWidth);
        }

        public static string FormatDistance(float distance)
        {
            return $"{Mathf.RoundToInt(Mathf.Max(0f, distance))}m";
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class QuestCompassHUDController : MonoBehaviour
    {
        private sealed class MarkerView
        {
            public QuestMarkerAnchor Anchor;
            public RectTransform CompassRoot;
            public Image CompassIcon;
            public TMP_Text CompassDistance;
            public RectTransform WorldRoot;
            public Image WorldIcon;
            public TMP_Text WorldDistance;
        }

        private sealed class TickView
        {
            public RectTransform Root;
            public RectTransform Line;
            public TMP_Text Label;
        }

        public readonly struct MarkerState
        {
            public MarkerState(
                Vector2 compassPosition,
                string compassDistance,
                bool worldVisible,
                Vector2 worldPosition,
                string worldDistance)
            {
                CompassPosition = compassPosition;
                CompassDistance = compassDistance;
                WorldVisible = worldVisible;
                WorldPosition = worldPosition;
                WorldDistance = worldDistance;
            }

            public Vector2 CompassPosition { get; }
            public string CompassDistance { get; }
            public bool WorldVisible { get; }
            public Vector2 WorldPosition { get; }
            public string WorldDistance { get; }
        }

        private static readonly string[] CardinalLabels =
        {
            "N", "NE", "E", "SE", "S", "SW", "W", "NW"
        };

        [Header("Sources")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform distanceOrigin;

        [Header("Compass")]
        [SerializeField, Min(320f)] private float compassWidth = 760f;
        [SerializeField, Range(30f, 180f)]
        private float visibleHalfAngle = 90f;
        [SerializeField, Min(0f)] private float markerEdgePadding = 28f;

        [Header("Style")]
        [SerializeField] private Color frameColor =
            new Color(0.05f, 0.84f, 1f, 0.95f);
        [SerializeField] private Color backgroundColor =
            new Color(0.025f, 0.055f, 0.12f, 0.92f);
        [SerializeField] private Color tickColor =
            new Color(0.82f, 0.9f, 0.98f, 0.9f);

        private readonly Dictionary<QuestMarkerAnchor, MarkerView> views =
            new Dictionary<QuestMarkerAnchor, MarkerView>();
        private readonly List<QuestMarkerAnchor> registeredAnchors =
            new List<QuestMarkerAnchor>();
        private readonly List<QuestMarkerAnchor> removals =
            new List<QuestMarkerAnchor>();
        private readonly HashSet<string> activeMarkerIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<TickView> ticks = new List<TickView>();

        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform compassRoot;
        private RectTransform tickLayer;
        private RectTransform compassMarkerLayer;
        private RectTransform worldMarkerLayer;
        private QuestController questController;
        private ParkourPlayerBridge automaticPlayer;
        private Transform automaticPlayerOrigin;
        private bool markerSetDirty = true;
        private bool hasExplicitGameplayCamera;
        private bool hasExplicitDistanceOrigin;

        public int CompassMarkerCount => views.Count;
        public int VisibleWorldMarkerCount
        {
            get
            {
                int count = 0;
                foreach (MarkerView view in views.Values)
                {
                    if (view.WorldRoot != null &&
                        view.WorldRoot.gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Awake()
        {
            hasExplicitGameplayCamera = gameplayCamera != null;
            hasExplicitDistanceOrigin = distanceOrigin != null;
            EnsureView();
        }

        private void OnEnable()
        {
            QuestMarkerAnchor.RegistryChanged += HandleMarkersChanged;
            EnsureView();
            BindQuestController();
            markerSetDirty = true;
        }

        private void OnDisable()
        {
            QuestMarkerAnchor.RegistryChanged -= HandleMarkersChanged;
            UnbindQuestController();
        }

        private void LateUpdate()
        {
            RefreshNow();
        }

        public void Configure(Camera cameraSource, Transform playerOrigin)
        {
            gameplayCamera = cameraSource;
            distanceOrigin = playerOrigin;
            hasExplicitGameplayCamera = cameraSource != null;
            hasExplicitDistanceOrigin = playerOrigin != null;
            markerSetDirty = true;
            RefreshNow();
        }

        public void RefreshNow()
        {
            EnsureView();
            ResolveSources();
            BindQuestController();

            if (markerSetDirty)
                SynchronizeMarkerViews();

            UpdateCompassTicks();
            UpdateMarkerPositions();
        }

        public bool TryGetMarkerState(
            string markerId,
            out MarkerState state)
        {
            string normalized = QuestMarkerAnchor.NormalizeId(markerId);
            foreach (MarkerView view in views.Values)
            {
                if (view.Anchor == null ||
                    view.Anchor.MarkerId != normalized)
                {
                    continue;
                }

                state = new MarkerState(
                    view.CompassRoot.anchoredPosition,
                    view.CompassDistance.text,
                    view.WorldRoot.gameObject.activeSelf,
                    view.WorldRoot.anchoredPosition,
                    view.WorldDistance.text);
                return true;
            }

            state = default;
            return false;
        }

        private void BindQuestController()
        {
            QuestController current = QuestController.Instance;
            if (questController == current)
                return;

            UnbindQuestController();
            questController = current;
            if (questController != null)
                questController.QuestsChanged += HandleQuestsChanged;
            markerSetDirty = true;
        }

        private void UnbindQuestController()
        {
            if (questController != null)
                questController.QuestsChanged -= HandleQuestsChanged;
            questController = null;
        }

        private void HandleQuestsChanged()
        {
            markerSetDirty = true;
        }

        private void HandleMarkersChanged()
        {
            markerSetDirty = true;
        }

        private void SynchronizeMarkerViews()
        {
            markerSetDirty = false;
            CollectActiveMarkerIds();
            QuestMarkerAnchor.CopyRegisteredTo(registeredAnchors);

            removals.Clear();
            foreach (KeyValuePair<QuestMarkerAnchor, MarkerView> pair in views)
            {
                QuestMarkerAnchor anchor = pair.Key;
                if (anchor == null || !ShouldDisplay(anchor))
                    removals.Add(anchor);
            }

            for (int index = 0; index < removals.Count; index++)
            {
                QuestMarkerAnchor anchor = removals[index];
                if (!views.TryGetValue(anchor, out MarkerView view))
                    continue;

                if (view.CompassRoot != null)
                    Destroy(view.CompassRoot.gameObject);
                if (view.WorldRoot != null)
                    Destroy(view.WorldRoot.gameObject);
                views.Remove(anchor);
            }

            for (int index = 0; index < registeredAnchors.Count; index++)
            {
                QuestMarkerAnchor anchor = registeredAnchors[index];
                if (!ShouldDisplay(anchor) || views.ContainsKey(anchor))
                    continue;

                views.Add(anchor, CreateMarkerView(anchor));
            }
        }

        private void CollectActiveMarkerIds()
        {
            activeMarkerIds.Clear();
            if (questController == null)
                return;

            IReadOnlyList<QuestRuntimeState> active =
                questController.ActiveQuests;
            for (int questIndex = 0;
                 questIndex < active.Count;
                 questIndex++)
            {
                QuestRuntimeState state = active[questIndex];
                IReadOnlyList<string> markerIds =
                    state.CurrentStage?.QuestMarkerIds;
                if (markerIds == null)
                    continue;

                for (int markerIndex = 0;
                     markerIndex < markerIds.Count;
                     markerIndex++)
                {
                    string resolved = QuestMarkerAnchor.ResolveStageId(
                        markerIds[markerIndex],
                        state.QuestId,
                        state.ContextTargetId);
                    if (!string.IsNullOrEmpty(resolved))
                        activeMarkerIds.Add(resolved);
                }
            }
        }

        private bool ShouldDisplay(QuestMarkerAnchor anchor)
        {
            return anchor != null &&
                anchor.IsAvailable &&
                (anchor.AvailableWithoutQuest ||
                 activeMarkerIds.Contains(anchor.MarkerId));
        }

        private void ResolveSources()
        {
            if ((!hasExplicitGameplayCamera ||
                 !hasExplicitDistanceOrigin) &&
                automaticPlayerOrigin == null)
            {
                ResolveAutomaticPlayer();
            }

            if (hasExplicitGameplayCamera && gameplayCamera == null)
                hasExplicitGameplayCamera = false;

            if (!hasExplicitGameplayCamera)
            {
                Camera playerCamera = automaticPlayer != null
                    ? automaticPlayer.GameplayCamera
                    : null;
                gameplayCamera = playerCamera != null
                    ? playerCamera
                    : Camera.main;
            }

            if (hasExplicitDistanceOrigin && distanceOrigin == null)
                hasExplicitDistanceOrigin = false;

            if (!hasExplicitDistanceOrigin)
            {
                distanceOrigin = automaticPlayerOrigin != null
                    ? automaticPlayerOrigin
                    : gameplayCamera != null
                        ? gameplayCamera.transform
                        : null;
            }
        }

        private void ResolveAutomaticPlayer()
        {
            automaticPlayer = FindFirstObjectByType<ParkourPlayerBridge>();
            if (automaticPlayer != null)
            {
                automaticPlayerOrigin = automaticPlayer.transform;
                return;
            }

            GameObject taggedPlayer =
                GameObject.FindGameObjectWithTag("Player");
            automaticPlayerOrigin = taggedPlayer != null
                ? taggedPlayer.transform
                    : null;
        }

        private void UpdateMarkerPositions()
        {
            if (gameplayCamera == null || distanceOrigin == null ||
                canvasRect == null || compassMarkerLayer == null)
            {
                return;
            }

            float halfWidth = Mathf.Max(
                0f,
                compassMarkerLayer.rect.width * 0.5f - markerEdgePadding);
            Camera eventCamera = canvas.renderMode ==
                    RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            foreach (MarkerView view in views.Values)
            {
                QuestMarkerAnchor anchor = view.Anchor;
                if (anchor == null)
                    continue;

                Vector3 worldPosition = anchor.WorldPosition;
                float distance = Vector3.Distance(
                    distanceOrigin.position,
                    worldPosition);
                string distanceText = QuestCompassMath.FormatDistance(
                    distance);
                float angle = QuestCompassMath.SignedHorizontalAngle(
                    gameplayCamera.transform,
                    worldPosition);
                float compassX = QuestCompassMath.CalculateCompassX(
                    angle,
                    visibleHalfAngle,
                    halfWidth);
                view.CompassRoot.anchoredPosition =
                    new Vector2(compassX, 0f);
                view.CompassDistance.text = distanceText;
                view.CompassDistance.gameObject.SetActive(anchor.ShowDistance);

                Vector3 viewport = gameplayCamera.WorldToViewportPoint(
                    worldPosition);
                bool onScreen = viewport.z > 0f &&
                    viewport.x >= 0.015f && viewport.x <= 0.985f &&
                    viewport.y >= 0.03f && viewport.y <= 0.97f;
                bool showWorld = onScreen &&
                    distance >= anchor.WorldMarkerFadeDistance &&
                    distance <= anchor.WorldMarkerMaxDistance;
                view.WorldRoot.gameObject.SetActive(showWorld);
                if (!showWorld)
                    continue;

                Vector3 screen = gameplayCamera.WorldToScreenPoint(
                    worldPosition);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect,
                        screen,
                        eventCamera,
                        out Vector2 localPoint))
                {
                    view.WorldRoot.anchoredPosition = localPoint;
                }

                view.WorldDistance.text = distanceText;
                view.WorldDistance.gameObject.SetActive(anchor.ShowDistance);
            }
        }

        private void EnsureView()
        {
            canvas ??= GetComponent<Canvas>();
            canvasRect ??= transform as RectTransform;
            if (canvas == null || canvasRect == null)
                return;

            if (worldMarkerLayer == null)
            {
                Transform existing = transform.Find("QuestWorldMarkers");
                worldMarkerLayer = existing as RectTransform;
            }

            if (worldMarkerLayer == null)
            {
                worldMarkerLayer = CreateRect(
                    "QuestWorldMarkers",
                    canvasRect,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero);
            }

            if (compassRoot == null)
            {
                Transform existing = transform.Find("QuestCompassHUD");
                compassRoot = existing as RectTransform;
            }

            if (compassRoot == null)
                BuildCompassView();
            else
                RecoverViewReferences();

            worldMarkerLayer.SetAsLastSibling();
            compassRoot.SetAsLastSibling();
        }

        private void BuildCompassView()
        {
            compassRoot = CreateRect(
                "QuestCompassHUD",
                canvasRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(compassWidth, 76f),
                new Vector2(0f, -22f));

            CreateImage(
                "Frame",
                compassRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(compassWidth + 6f, 48f),
                Vector2.zero,
                frameColor);
            CreateImage(
                "Tape",
                compassRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(compassWidth, 42f),
                new Vector2(0f, -3f),
                backgroundColor);

            tickLayer = CreateRect(
                "Ticks",
                compassRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(compassWidth - 24f, 40f),
                new Vector2(0f, -4f));
            compassMarkerLayer = CreateRect(
                "Markers",
                compassRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(compassWidth - 24f, 72f),
                new Vector2(0f, -1f));

            CreateImage(
                "Center",
                compassRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(4f, 10f),
                new Vector2(0f, -39f),
                frameColor);
            BuildTickPool();
        }

        private void RecoverViewReferences()
        {
            tickLayer = compassRoot.Find("Ticks") as RectTransform;
            compassMarkerLayer = compassRoot.Find("Markers") as RectTransform;
            if (tickLayer == null || compassMarkerLayer == null)
            {
                Destroy(compassRoot.gameObject);
                compassRoot = null;
                BuildCompassView();
                return;
            }

            if (ticks.Count == 0)
                BuildTickPool();
        }

        private void BuildTickPool()
        {
            ticks.Clear();
            for (int index = 0; index < 13; index++)
            {
                RectTransform root = CreateRect(
                    $"Tick_{index:00}",
                    tickLayer,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(54f, 38f),
                    Vector2.zero);
                Image line = CreateImage(
                    "Line",
                    root,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(2f, 12f),
                    new Vector2(0f, -8f),
                    tickColor);
                TMP_Text label = CreateText(
                    "Label",
                    root,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(56f, 22f),
                    new Vector2(0f, -5f),
                    17f,
                    tickColor);
                ticks.Add(new TickView
                {
                    Root = root,
                    Line = line.rectTransform,
                    Label = label
                });
            }
        }

        private void UpdateCompassTicks()
        {
            if (gameplayCamera == null || tickLayer == null || ticks.Count == 0)
                return;

            Vector3 forward = Vector3.ProjectOnPlane(
                gameplayCamera.transform.forward,
                Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                return;

            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            const float step = 15f;
            float center = Mathf.Round(yaw / step) * step;
            float halfWidth = tickLayer.rect.width * 0.5f;
            int middle = ticks.Count / 2;

            for (int index = 0; index < ticks.Count; index++)
            {
                float angle = center + (index - middle) * step;
                float delta = Mathf.DeltaAngle(yaw, angle);
                TickView tick = ticks[index];
                tick.Root.anchoredPosition = new Vector2(
                    delta / visibleHalfAngle * halfWidth,
                    0f);

                float cardinalAngle = Mathf.Round(angle / 45f) * 45f;
                bool major = Mathf.Abs(Mathf.DeltaAngle(
                    angle,
                    cardinalAngle)) < 0.01f;
                tick.Line.sizeDelta = new Vector2(2f, major ? 20f : 10f);
                tick.Line.anchoredPosition = new Vector2(
                    0f,
                    major ? -17f : -12f);
                tick.Label.text = major
                    ? CardinalLabels[Mathf.RoundToInt(
                        Mathf.Repeat(cardinalAngle, 360f) / 45f) %
                        CardinalLabels.Length]
                    : string.Empty;
            }
        }

        private MarkerView CreateMarkerView(QuestMarkerAnchor anchor)
        {
            string safeName = string.IsNullOrEmpty(anchor.MarkerId)
                ? anchor.GetInstanceID().ToString()
                : anchor.MarkerId.Replace('/', '_');

            RectTransform compassView = CreateRect(
                $"CompassMarker_{safeName}",
                compassMarkerLayer,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(72f, 70f),
                Vector2.zero);
            Image compassIcon = CreateImage(
                "Icon",
                compassView,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(20f, 20f),
                new Vector2(0f, -10f),
                anchor.Color);
            ConfigureIcon(compassIcon, anchor);
            TMP_Text compassDistance = CreateText(
                "Distance",
                compassView,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(72f, 22f),
                new Vector2(0f, -34f),
                15f,
                Color.white);

            RectTransform worldView = CreateRect(
                $"WorldMarker_{safeName}",
                worldMarkerLayer,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(92f, 56f),
                Vector2.zero);
            Image worldIcon = CreateImage(
                "Point",
                worldView,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(18f, 18f),
                new Vector2(0f, 9f),
                anchor.Color);
            ConfigureIcon(worldIcon, anchor);
            TMP_Text worldDistance = CreateText(
                "Distance",
                worldView,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(84f, 24f),
                new Vector2(0f, -15f),
                17f,
                Color.white);
            worldView.gameObject.SetActive(false);

            return new MarkerView
            {
                Anchor = anchor,
                CompassRoot = compassView,
                CompassIcon = compassIcon,
                CompassDistance = compassDistance,
                WorldRoot = worldView,
                WorldIcon = worldIcon,
                WorldDistance = worldDistance
            };
        }

        private static void ConfigureIcon(
            Image image,
            QuestMarkerAnchor anchor)
        {
            image.sprite = anchor.Icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.localRotation = anchor.Icon == null
                ? Quaternion.Euler(0f, 0f, 45f)
                : Quaternion.identity;
        }

        private static RectTransform CreateRect(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 position)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Image CreateImage(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMin,
                anchorMax,
                pivot,
                size,
                position);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 position,
            float fontSize,
            Color color)
        {
            RectTransform rect = CreateRect(
                name,
                parent,
                anchorMin,
                anchorMax,
                pivot,
                size,
                position);
            TextMeshProUGUI text =
                rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }
    }
}
