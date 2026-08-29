using System;
using System.Text;
using NERA.Antenna;
using NERA.Drone;
using NERA.Energy;
using NERA.Localization;
using NERA.Maintenance;
using NERA.Station;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Terminal
{
    /// <summary>
    /// Read-only station status screen. Physical upgrades are installed on the
    /// world object and reflected here through its preview slots and stats.
    /// </summary>
    public sealed class TerminalStationScreenController : MonoBehaviour
    {
        private const float DataRefreshInterval = 0.1f;
        private const float DefaultInfoHeight = 200f;
        private const float DefaultStatusHeight = 300f;

        private TerminalUIScreen terminal;
        [SerializeField] private RawImage stationImage;
        [SerializeField] private Camera stationCamera;
        [Header("Object status popup")]
        [SerializeField] private RectTransform statusRoot;
        [SerializeField] private RectTransform objectInfoPanel;
        [SerializeField] private RectTransform statusPanelRect;
        [SerializeField] private Button statusExpandButton;
        [SerializeField] private Image statusConnectorLine;
        [SerializeField, Min(0f)] private float popupGap = 32f;
        [SerializeField, Min(0f)] private float safeAreaPadding = 16f;
        [SerializeField, Min(1f)] private float connectorWidth = 3f;
        [SerializeField] private TMP_Text objectNameText;
        [SerializeField] private TMP_Text objectInfoText;
        [SerializeField] private Image objectImage;
        [SerializeField] private GameObject powerSwitchRoot;
        [SerializeField] private Button powerOnButton;
        [SerializeField] private Button powerOffButton;
        [SerializeField] private RectTransform powerHandle;
        [SerializeField] private Animator powerHandleAnimator;
        [SerializeField] private TMP_Text powerStatusText;
        [SerializeField] private GameObject statusPanel;
        [SerializeField] private TMP_Text statusText;

        private StationSystemType? selectedSystem;
        private string selectedObjectName;
        private string selectedObjectId;
        private bool initialized;
        private bool preserveAuthoredSwitchAnimation;
        private bool forcePowerHandleSync;
        private StationSystemType? renderedPowerSystem;
        private string renderedPowerObjectId;
        private bool? renderedPowerActive;
        private StationSystemsController subscribedSystems;
        private EnergySystemController subscribedEnergy;
        private DroneScanController subscribedDrone;
        private AntennaController subscribedAntenna;
        private TerminalPreviewRenderer previewRenderer;
        private bool dataRefreshPending;
        private float nextDataRefreshAt;
        private Transform selectedPreviewRoot;
        private Renderer[] selectedRenderers = Array.Empty<Renderer>();
        private Collider[] selectedColliders = Array.Empty<Collider>();
        private TMP_Text statusExpandGlyph;
        private bool statusExpanded;

        public StationSystemType? SelectedSystem => selectedSystem;
        public string SelectedObjectId => selectedObjectId;
        public bool IsStatusVisible =>
            statusRoot != null && statusRoot.gameObject.activeSelf;
        public bool IsStatusExpanded => statusExpanded;

        private void Update()
        {
            bool canUpdate = terminal?.IsOpen == true &&
                gameObject.activeInHierarchy;
            if (canUpdate && IsStatusVisible)
                UpdateStatusLayout();

            if (!dataRefreshPending ||
                Time.unscaledTime < nextDataRefreshAt ||
                !canUpdate)
            {
                return;
            }

            RefreshAll();
        }

        public void SelectSystem(StationSystemType type)
        {
            StationSystemDefinition definition =
                StationSystemsController.Instance?.GetDefinition(type) ??
                StationSystemsConfig.LoadDefault()?.Find(type);
            selectedSystem = type;
            selectedObjectName = definition?.DisplayName ?? type.ToString();
            selectedObjectId = definition?.ObjectId ?? string.Empty;
            selectedPreviewRoot = FindPreviewRoot(type, selectedObjectId);
            CacheSelectedGeometry();
            SetStatusExpanded(false, false);
            SetStatusVisible(true);
            RefreshAll();
            UpdateStatusLayout();
        }

        public bool SelectPreviewObject(Transform target)
        {
            if (target == null)
                return false;

            ResolveStationObject(
                target,
                out selectedObjectName,
                out selectedSystem,
                out selectedObjectId,
                out Transform previewRoot);
            if (!selectedSystem.HasValue)
            {
                ClearSelection();
                return false;
            }

            bool changed = selectedPreviewRoot != previewRoot;
            selectedPreviewRoot = previewRoot;
            CacheSelectedGeometry();
            if (changed)
                SetStatusExpanded(false, false);
            SetStatusVisible(true);
            RefreshAll();
            UpdateStatusLayout();
            return true;
        }

        public void DismissSelection()
        {
            ClearSelection();
        }

        public void ToggleStatusDetails()
        {
            if (!selectedSystem.HasValue)
                return;

            SetStatusExpanded(!statusExpanded, true);
        }

        public void Initialize(TerminalUIScreen owner)
        {
            terminal = owner;
            if (initialized)
                return;
            initialized = true;
            NERALocalization.LocaleChanged += RefreshIfVisible;
            CacheHierarchy();
            EnsureStatusVisuals();
            BindButtons();
            ConfigurePreviewPicking();
            ClearSelection();
            SetScreenActive(false);
        }

        public void SetScreenActive(bool active)
        {
            bool shouldRender = active && terminal != null && terminal.IsOpen;
            if (!shouldRender)
            {
                ClearSelection();
                previewRenderer?.SetPreviewActive(false);
                UnbindDataEvents();
                TerminalUIUtility.ReleaseCameraTarget(stationCamera);
                return;
            }
            BindDataEvents();
            ClearSelection();
            RefreshAll();
            previewRenderer?.SetPreviewActive(true);
        }

        private void CacheHierarchy()
        {
            stationImage ??= TerminalUIUtility.FindComponent<RawImage>(
                transform, "Station_RawImage");
            stationCamera ??= TerminalUIUtility.FindComponent<Camera>(
                transform, "StationUICamera");
            if (stationCamera != null)
            {
                previewRenderer =
                    stationCamera.GetComponent<TerminalPreviewRenderer>() ??
                    stationCamera.gameObject.AddComponent<TerminalPreviewRenderer>();
                previewRenderer.Initialize(stationCamera);
            }
            statusRoot ??= TerminalUIUtility.Find(
                transform, "ScreenStatus") as RectTransform;
            objectInfoPanel ??= TerminalUIUtility.Find(
                statusRoot != null ? statusRoot : transform,
                "background_info_obj") as RectTransform;
            objectNameText ??= TerminalUIUtility.FindComponent<TMP_Text>(
                transform, "Text_nameObj");
            objectInfoText ??= TerminalUIUtility.FindComponent<TMP_Text>(
                transform, "Text_info_obj");
            objectImage ??= TerminalUIUtility.FindComponent<Image>(
                transform, "Image_obj");

            Transform toggleRoot = powerSwitchRoot != null
                ? powerSwitchRoot.transform
                : TerminalUIUtility.Find(transform, "Toggle");
            if (toggleRoot != null)
            {
                powerSwitchRoot ??= toggleRoot.gameObject;
                powerOnButton ??= TerminalUIUtility.FindComponent<Button>(
                    toggleRoot, "OnButton");
                powerOffButton ??= TerminalUIUtility.FindComponent<Button>(
                    toggleRoot, "OffButton");
                powerHandle ??= TerminalUIUtility.FindComponent<RectTransform>(
                    toggleRoot, "Handle");
                powerHandleAnimator ??= powerHandle != null
                    ? powerHandle.GetComponent<Animator>()
                    : null;
                powerStatusText ??= TerminalUIUtility.FindComponent<TMP_Text>(
                    toggleRoot, "Text_Status");
            }

            statusExpandButton ??=
                TerminalUIUtility.FindComponent<Button>(
                    transform, "StatusExpandButton") ??
                TerminalUIUtility.FindComponent<Button>(
                    transform, "StatusMapButton");
            statusExpandGlyph = statusExpandButton != null
                ? statusExpandButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            statusPanel ??= TerminalUIUtility.Find(
                transform, "background_Status")?.gameObject;
            statusPanelRect = statusPanel != null
                ? statusPanel.transform as RectTransform
                : null;
            statusText ??= TerminalUIUtility.FindComponent<TMP_Text>(
                statusPanel != null ? statusPanel.transform : transform,
                "Text_description");
        }

        private void BindButtons()
        {
            statusExpandButton?.onClick.AddListener(ToggleStatusDetails);
            powerOnButton?.onClick.AddListener(
                () => HandlePowerSwitchChanged(false));
            powerOffButton?.onClick.AddListener(
                () => HandlePowerSwitchChanged(true));
        }

        private void ConfigurePreviewPicking()
        {
            if (stationImage == null)
                return;
            UIPreviewRaycaster picker =
                stationImage.GetComponent<UIPreviewRaycaster>() ??
                stationImage.gameObject.AddComponent<UIPreviewRaycaster>();
            picker.Initialize(
                stationImage,
                stationCamera,
                HandlePreviewHit,
                ClearSelection);
        }

        private void HandlePreviewHit(RaycastHit hit)
        {
            Transform target = hit.collider != null
                ? hit.collider.transform
                : hit.transform;
            if (target == null || !SelectPreviewObject(target))
                ClearSelection();
        }

        private void ResolveStationObject(
            Transform hit,
            out string objectName,
            out StationSystemType? system,
            out string objectId,
            out Transform previewRoot)
        {
            objectName = hit != null ? hit.name : string.Empty;
            system = null;
            objectId = string.Empty;
            previewRoot = null;
            Transform current = hit;
            StationSystemsConfig config =
                StationSystemsController.Instance?.Config ??
                StationSystemsConfig.LoadDefault();
            while (current != null && current != transform)
            {
                StationObjectIdentity identity =
                    current.GetComponent<StationObjectIdentity>();
                StationSystemDefinition definition =
                    identity?.ResolveDefinition(config);
                if (definition != null)
                {
                    system = definition.SystemType;
                    objectName = definition.DisplayName;
                    objectId = definition.ObjectId;
                    previewRoot = current;
                    return;
                }
                current = current.parent;
            }
        }

        private void ClearSelection()
        {
            selectedSystem = null;
            selectedObjectName = string.Empty;
            selectedObjectId = string.Empty;
            selectedPreviewRoot = null;
            selectedRenderers = Array.Empty<Renderer>();
            selectedColliders = Array.Empty<Collider>();
            SetStatusExpanded(false, false);
            SetStatusVisible(false);
            TerminalUIUtility.SetText(
                objectNameText,
                Localize("station.select_object", "SELECT STATION OBJECT"));
            TerminalUIUtility.SetText(
                objectInfoText,
                Localize(
                    "station.select_object_hint",
                    "Select an object in the 3D station preview."));
            if (powerSwitchRoot != null)
                powerSwitchRoot.SetActive(false);
            renderedPowerSystem = null;
            renderedPowerObjectId = string.Empty;
            renderedPowerActive = null;
            preserveAuthoredSwitchAnimation = false;
            forcePowerHandleSync = false;
            RefreshStatus();
        }

        private void EnsureStatusVisuals()
        {
            if (statusRoot == null)
                return;

            if (objectInfoPanel != null)
            {
                Image background = objectInfoPanel.GetComponent<Image>();
                if (background != null)
                    background.raycastTarget = true;
            }
            if (statusPanelRect != null)
            {
                Image background = statusPanelRect.GetComponent<Image>();
                if (background != null)
                    background.raycastTarget = true;
            }

            if (statusExpandButton != null)
            {
                RectTransform buttonRect =
                    statusExpandButton.transform as RectTransform;
                if (buttonRect != null)
                {
                    buttonRect.SetParent(statusRoot, false);
                    buttonRect.anchorMin = new Vector2(0.5f, 0f);
                    buttonRect.anchorMax = new Vector2(0.5f, 0f);
                    buttonRect.pivot = new Vector2(0.5f, 1f);
                    buttonRect.anchoredPosition = new Vector2(0f, -4f);
                    buttonRect.sizeDelta = new Vector2(44f, 30f);
                }
                statusExpandButton.navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.None
                };
                statusExpandButton.gameObject.SetActive(true);
            }

            if (statusExpandGlyph != null)
            {
                statusExpandGlyph.enableAutoSizing = false;
                statusExpandGlyph.fontSize = 22f;
                statusExpandGlyph.alignment =
                    TextAlignmentOptions.Center;
                statusExpandGlyph.raycastTarget = false;
            }

            if (statusConnectorLine == null && statusRoot.parent != null)
            {
                GameObject lineObject = new GameObject(
                    "StatusConnectorLine",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                RectTransform lineRect =
                    lineObject.GetComponent<RectTransform>();
                lineRect.SetParent(statusRoot.parent, false);
                lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);
                statusConnectorLine = lineObject.GetComponent<Image>();
                statusConnectorLine.color =
                    new Color32(0, 220, 255, 230);
                statusConnectorLine.raycastTarget = false;
                lineRect.SetSiblingIndex(statusRoot.GetSiblingIndex());
            }

            ApplyStatusGeometry();
            UpdateExpandVisual();
        }

        private void SetStatusVisible(bool visible)
        {
            if (statusRoot != null)
            {
                statusRoot.gameObject.SetActive(visible);
                if (visible)
                    statusRoot.SetAsLastSibling();
            }
            if (statusConnectorLine != null)
                statusConnectorLine.gameObject.SetActive(visible);
        }

        private void SetStatusExpanded(bool expanded, bool updateLayout)
        {
            statusExpanded = expanded && selectedSystem.HasValue;
            if (statusPanel != null)
                statusPanel.SetActive(statusExpanded);
            ApplyStatusGeometry();
            UpdateExpandVisual();
            if (statusExpanded)
                RefreshStatus();
            if (updateLayout)
            {
                Canvas.ForceUpdateCanvases();
                UpdateStatusLayout();
            }
        }

        private void ApplyStatusGeometry()
        {
            if (statusRoot == null || objectInfoPanel == null)
                return;

            float width = Mathf.Max(
                statusRoot.sizeDelta.x,
                objectInfoPanel.sizeDelta.x);
            float infoHeight = objectInfoPanel.sizeDelta.y > 0f
                ? objectInfoPanel.sizeDelta.y
                : DefaultInfoHeight;
            float detailsHeight = statusPanelRect != null &&
                statusPanelRect.sizeDelta.y > 0f
                    ? statusPanelRect.sizeDelta.y
                    : DefaultStatusHeight;
            float totalHeight = statusExpanded
                ? infoHeight + detailsHeight
                : infoHeight;
            statusRoot.sizeDelta = new Vector2(width, totalHeight);

            objectInfoPanel.anchorMin = new Vector2(0.5f, 0.5f);
            objectInfoPanel.anchorMax = new Vector2(0.5f, 0.5f);
            objectInfoPanel.pivot = new Vector2(0.5f, 0.5f);
            objectInfoPanel.anchoredPosition = new Vector2(
                0f,
                statusExpanded ? detailsHeight * 0.5f : 0f);

            if (statusPanelRect == null)
                return;
            statusPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            statusPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            statusPanelRect.pivot = new Vector2(0.5f, 0.5f);
            statusPanelRect.anchoredPosition =
                new Vector2(0f, -infoHeight * 0.5f);
        }

        private void UpdateExpandVisual()
        {
            TerminalUIUtility.SetText(
                statusExpandGlyph,
                statusExpanded ? "▲" : "▼");
        }

        private Transform FindPreviewRoot(
            StationSystemType type,
            string objectId)
        {
            StationObjectIdentity[] identities =
                GetComponentsInChildren<StationObjectIdentity>(true);
            for (int i = 0; i < identities.Length; i++)
            {
                StationObjectIdentity identity = identities[i];
                if (identity == null || identity.SystemType != type)
                    continue;
                if (!string.IsNullOrWhiteSpace(objectId) &&
                    !string.Equals(
                        identity.ObjectId,
                        objectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                return identity.transform;
            }
            return null;
        }

        private void CacheSelectedGeometry()
        {
            selectedRenderers = selectedPreviewRoot != null
                ? selectedPreviewRoot.GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();
            selectedColliders = selectedPreviewRoot != null
                ? selectedPreviewRoot.GetComponentsInChildren<Collider>(true)
                : Array.Empty<Collider>();
        }

        private void UpdateStatusLayout()
        {
            if (statusRoot == null ||
                stationImage == null ||
                stationCamera == null ||
                selectedPreviewRoot == null)
            {
                if (statusConnectorLine != null)
                    statusConnectorLine.gameObject.SetActive(false);
                return;
            }

            if (!TryGetProjectedSelection(
                    out Rect objectRect,
                    out Vector2 objectCenter))
            {
                if (statusConnectorLine != null)
                    statusConnectorLine.gameObject.SetActive(false);
                return;
            }

            Rect safeRect = GetPopupSafeRect();
            Rect infoRect = GetRectInStatusParent(objectInfoPanel);
            Vector2 preferredInfoCenter = ChoosePopupPosition(
                objectRect,
                safeRect,
                infoRect.size);
            MoveStatusRootToInfoCenter(preferredInfoCenter, infoRect.center);

            if (statusExpanded)
            {
                Rect expandedRect = GetRectInStatusParent(statusRoot);
                Vector2 clampedCenter = ClampPopupCenter(
                    expandedRect.center,
                    safeRect,
                    expandedRect.size);
                statusRoot.anchoredPosition +=
                    clampedCenter - expandedRect.center;
            }

            UpdateConnector(objectCenter);
        }

        private void MoveStatusRootToInfoCenter(
            Vector2 desiredInfoCenter,
            Vector2 currentInfoCenter)
        {
            statusRoot.anchoredPosition +=
                desiredInfoCenter - currentInfoCenter;
        }

        private bool TryGetProjectedSelection(
            out Rect projectedRect,
            out Vector2 projectedCenter)
        {
            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < selectedRenderers.Length; i++)
            {
                Renderer renderer = selectedRenderers[i];
                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            for (int i = 0; i < selectedColliders.Length; i++)
            {
                Collider collider = selectedColliders[i];
                if (collider == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (!hasBounds)
            {
                if (!TryProjectWorldPoint(
                        selectedPreviewRoot.position,
                        out projectedCenter))
                {
                    projectedRect = default;
                    return false;
                }
                projectedRect = new Rect(
                    projectedCenter - Vector2.one * 8f,
                    Vector2.one * 16f);
                return true;
            }

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float xMin = float.PositiveInfinity;
            float yMin = float.PositiveInfinity;
            float xMax = float.NegativeInfinity;
            float yMax = float.NegativeInfinity;
            bool projectedAny = false;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        if (!TryProjectWorldPoint(corner, out Vector2 point))
                            continue;
                        projectedAny = true;
                        xMin = Mathf.Min(xMin, point.x);
                        yMin = Mathf.Min(yMin, point.y);
                        xMax = Mathf.Max(xMax, point.x);
                        yMax = Mathf.Max(yMax, point.y);
                    }
                }
            }

            if (!projectedAny)
            {
                projectedRect = default;
                projectedCenter = default;
                return false;
            }

            projectedRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            projectedCenter = projectedRect.center;
            return true;
        }

        private bool TryProjectWorldPoint(
            Vector3 worldPoint,
            out Vector2 parentPoint)
        {
            parentPoint = default;
            if (stationCamera == null ||
                stationImage == null ||
                statusRoot == null ||
                statusRoot.parent == null)
            {
                return false;
            }

            Vector3 viewport = stationCamera.WorldToViewportPoint(worldPoint);
            if (viewport.z <= 0f)
                return false;

            Rect uv = stationImage.uvRect;
            float normalizedX = Mathf.Abs(uv.width) > Mathf.Epsilon
                ? (viewport.x - uv.x) / uv.width
                : viewport.x;
            float normalizedY = Mathf.Abs(uv.height) > Mathf.Epsilon
                ? (viewport.y - uv.y) / uv.height
                : viewport.y;
            Rect rawRect = stationImage.rectTransform.rect;
            Vector3 rawLocal = new Vector3(
                Mathf.LerpUnclamped(
                    rawRect.xMin,
                    rawRect.xMax,
                    normalizedX),
                Mathf.LerpUnclamped(
                    rawRect.yMin,
                    rawRect.yMax,
                    normalizedY),
                0f);
            Vector3 world = stationImage.rectTransform.TransformPoint(rawLocal);
            Vector3 local = statusRoot.parent.InverseTransformPoint(world);
            parentPoint = new Vector2(local.x, local.y);
            return true;
        }

        private Rect GetPopupSafeRect()
        {
            RectTransform rawRect = stationImage.rectTransform;
            Rect rawBounds = GetRectInStatusParent(rawRect);
            Rect safe = Rect.MinMaxRect(
                rawBounds.xMin + safeAreaPadding,
                rawBounds.yMin + safeAreaPadding,
                rawBounds.xMax - safeAreaPadding,
                rawBounds.yMax - safeAreaPadding);
            if (safe.width <= 0f || safe.height <= 0f)
                return rawBounds;
            return safe;
        }

        private Rect GetRectInStatusParent(RectTransform rectTransform)
        {
            if (rectTransform == null ||
                statusRoot == null ||
                statusRoot.parent == null)
            {
                return default;
            }

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            float xMin = float.PositiveInfinity;
            float yMin = float.PositiveInfinity;
            float xMax = float.NegativeInfinity;
            float yMax = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 local =
                    statusRoot.parent.InverseTransformPoint(corners[i]);
                xMin = Mathf.Min(xMin, local.x);
                yMin = Mathf.Min(yMin, local.y);
                xMax = Mathf.Max(xMax, local.x);
                yMax = Mathf.Max(yMax, local.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private Vector2 ChoosePopupPosition(
            Rect objectRect,
            Rect safeRect,
            Vector2 popupSize)
        {
            Rect protectedObject = Rect.MinMaxRect(
                objectRect.xMin - popupGap * 0.25f,
                objectRect.yMin - popupGap * 0.25f,
                objectRect.xMax + popupGap * 0.25f,
                objectRect.yMax + popupGap * 0.25f);
            Vector2 center = objectRect.center;
            Vector2 half = popupSize * 0.5f;
            Vector2[] candidates =
            {
                new Vector2(
                    objectRect.xMax + popupGap + half.x,
                    center.y),
                new Vector2(
                    objectRect.xMin - popupGap - half.x,
                    center.y),
                new Vector2(
                    center.x,
                    objectRect.yMax + popupGap + half.y),
                new Vector2(
                    center.x,
                    objectRect.yMin - popupGap - half.y)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Rect candidateRect = RectFromCenter(
                    candidates[i],
                    popupSize);
                if (Contains(safeRect, candidateRect) &&
                    !candidateRect.Overlaps(protectedObject))
                {
                    return candidates[i];
                }
            }

            Vector2 best = ClampPopupCenter(
                candidates[0],
                safeRect,
                popupSize);
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < candidates.Length; i++)
            {
                Vector2 clamped = ClampPopupCenter(
                    candidates[i],
                    safeRect,
                    popupSize);
                Rect clampedRect = RectFromCenter(clamped, popupSize);
                float overlap = IntersectionArea(
                    clampedRect,
                    protectedObject);
                float movement = (clamped - candidates[i]).sqrMagnitude;
                float score = overlap * 1000f + movement + i * 0.01f;
                if (score >= bestScore)
                    continue;
                bestScore = score;
                best = clamped;
            }
            return best;
        }

        private static Vector2 ClampPopupCenter(
            Vector2 center,
            Rect safeRect,
            Vector2 size)
        {
            Vector2 half = size * 0.5f;
            float minX = safeRect.xMin + half.x;
            float maxX = safeRect.xMax - half.x;
            float minY = safeRect.yMin + half.y;
            float maxY = safeRect.yMax - half.y;
            return new Vector2(
                minX <= maxX
                    ? Mathf.Clamp(center.x, minX, maxX)
                    : safeRect.center.x,
                minY <= maxY
                    ? Mathf.Clamp(center.y, minY, maxY)
                    : safeRect.center.y);
        }

        private void UpdateConnector(Vector2 objectCenter)
        {
            if (statusConnectorLine == null || objectInfoPanel == null)
                return;

            Rect infoRect = GetRectInStatusParent(objectInfoPanel);
            Vector2 popupPoint = ClosestPointOnRectBoundary(
                infoRect,
                objectCenter);
            Vector2 delta = popupPoint - objectCenter;
            float length = delta.magnitude;
            statusConnectorLine.gameObject.SetActive(length > 0.5f);
            if (length <= 0.5f)
                return;

            RectTransform lineRect =
                statusConnectorLine.rectTransform;
            lineRect.anchoredPosition =
                (objectCenter + popupPoint) * 0.5f;
            lineRect.sizeDelta = new Vector2(length, connectorWidth);
            lineRect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static Vector2 ClosestPointOnRectBoundary(
            Rect rect,
            Vector2 point)
        {
            Vector2 clamped = new Vector2(
                Mathf.Clamp(point.x, rect.xMin, rect.xMax),
                Mathf.Clamp(point.y, rect.yMin, rect.yMax));
            if (!rect.Contains(point))
                return clamped;

            float left = point.x - rect.xMin;
            float right = rect.xMax - point.x;
            float bottom = point.y - rect.yMin;
            float top = rect.yMax - point.y;
            float minimum = Mathf.Min(left, right, bottom, top);
            if (Mathf.Approximately(minimum, left))
                clamped.x = rect.xMin;
            else if (Mathf.Approximately(minimum, right))
                clamped.x = rect.xMax;
            else if (Mathf.Approximately(minimum, bottom))
                clamped.y = rect.yMin;
            else
                clamped.y = rect.yMax;
            return clamped;
        }

        private static Rect RectFromCenter(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * 0.5f, size);
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin &&
                inner.xMax <= outer.xMax &&
                inner.yMin >= outer.yMin &&
                inner.yMax <= outer.yMax;
        }

        private static float IntersectionArea(Rect first, Rect second)
        {
            float width = Mathf.Max(
                0f,
                Mathf.Min(first.xMax, second.xMax) -
                Mathf.Max(first.xMin, second.xMin));
            float height = Mathf.Max(
                0f,
                Mathf.Min(first.yMax, second.yMax) -
                Mathf.Max(first.yMin, second.yMin));
            return width * height;
        }

        private void RefreshAll()
        {
            dataRefreshPending = false;
            nextDataRefreshAt = Time.unscaledTime + DataRefreshInterval;
            RefreshObjectInfo();
            RefreshPowerSwitch();
            RefreshStatus();
            UpdateExpandVisual();
            previewRenderer?.RequestRender();
        }

        private void RefreshObjectInfo()
        {
            if (!selectedSystem.HasValue)
                return;
            StationSystemDefinition definition =
                StationSystemsController.Instance?.GetDefinition(
                    selectedSystem.Value,
                    selectedObjectId) ??
                StationSystemsConfig.LoadDefault()?.Find(
                    selectedSystem.Value,
                    selectedObjectId);
            TerminalUIUtility.SetText(
                objectNameText,
                !string.IsNullOrWhiteSpace(selectedObjectName)
                    ? FormatObjectName(selectedObjectName)
                    : definition?.DisplayName ??
                        selectedSystem.Value.ToString());
            TerminalUIUtility.SetText(
                objectInfoText,
                definition?.Description ?? string.Empty);
            if (objectImage != null)
                objectImage.enabled = objectImage.sprite != null;
        }

        private void RefreshPowerSwitch()
        {
            if (powerSwitchRoot == null)
                return;
            bool visible = selectedSystem.HasValue;
            powerSwitchRoot.SetActive(visible);
            if (!visible)
                return;

            StationSystemType type = selectedSystem.Value;
            StationSystemsController systems = StationSystemsController.Instance;
            bool critical = type == StationSystemType.Battery ||
                type == StationSystemType.Terminal;
            bool controllable = critical ||
                systems?.GetDefinition(type, selectedObjectId)?.Controllable ==
                    true;
            bool requestedActive = IsSelectedSystemRequestedActive(type, systems);
            bool hasRequiredCharge = critical ||
                HasSelectedSystemRequiredCharge(type, systems);
            bool maintenanceReady = critical ||
                systems?.IsMaintenanceReady(type, selectedObjectId) != false;
            bool active = requestedActive &&
                hasRequiredCharge &&
                maintenanceReady;
            bool lowPower = !critical && requestedActive && !hasRequiredCharge;
            bool canChangeState = active || critical ||
                systems?.CanStart(type, selectedObjectId, out _) == true;
            bool interactable = controllable && canChangeState &&
                !(type == StationSystemType.Drone &&
                  DroneScanController.Instance?.IsAtStation == false);

            bool visualStateChanged = renderedPowerSystem != selectedSystem ||
                !string.Equals(
                    renderedPowerObjectId,
                    selectedObjectId,
                    StringComparison.OrdinalIgnoreCase) ||
                renderedPowerActive != active;
            if (powerOnButton != null)
            {
                powerOnButton.gameObject.SetActive(active);
                powerOnButton.interactable = interactable;
            }
            if (powerOffButton != null)
            {
                powerOffButton.gameObject.SetActive(!active);
                powerOffButton.interactable = interactable;
            }
            if (forcePowerHandleSync ||
                visualStateChanged && !preserveAuthoredSwitchAnimation)
            {
                SetPowerHandleState(active);
            }

            TerminalUIUtility.SetText(
                powerStatusText,
                lowPower
                    ? Localize("station.power.low", "Low Power")
                    : active
                        ? Localize("station.power.active", "Active")
                        : Localize("station.power.inactive", "Inactive"));
            renderedPowerSystem = selectedSystem;
            renderedPowerObjectId = selectedObjectId;
            renderedPowerActive = active;
            preserveAuthoredSwitchAnimation = false;
            forcePowerHandleSync = false;
        }

        private void SetPowerHandleState(bool active)
        {
            if (powerHandleAnimator != null &&
                powerHandleAnimator.runtimeAnimatorController != null)
            {
                powerHandleAnimator.Play(
                    active ? "ToggleOn_clip" : "ToggleOff_clip",
                    0,
                    1f);
                powerHandleAnimator.Update(0f);
            }
            if (powerHandle == null)
                return;
            Vector2 position = powerHandle.anchoredPosition;
            position.x = active ? 25f : -25f;
            powerHandle.anchoredPosition = position;
        }

        private bool IsSelectedSystemRequestedActive(
            StationSystemType type,
            StationSystemsController systems)
        {
            return type == StationSystemType.Battery
                ? EnergySystemController.Instance?.GridEnabled == true
                : systems == null ||
                    systems.IsRequestedActive(type, selectedObjectId);
        }

        private bool HasSelectedSystemRequiredCharge(
            StationSystemType type,
            StationSystemsController systems)
        {
            if (systems != null)
                return systems.HasRequiredCharge(type, selectedObjectId);
            EnergySystemController energy = EnergySystemController.Instance;
            return energy != null && energy.HasSufficientCharge(
                energy.Config.GetMinimumCharge01(type, selectedObjectId));
        }

        private void HandlePowerSwitchChanged(bool active)
        {
            if (!selectedSystem.HasValue)
                return;
            StationSystemType type = selectedSystem.Value;
            StationSystemsController systems = StationSystemsController.Instance;
            bool changed;
            if (type == StationSystemType.Battery)
            {
                changed = systems?.SetCriticalSystemActive(type, active) == true;
                if (changed)
                {
                    StationPowerController.Instance?.SetState(
                        active
                            ? StationPowerState.Online
                            : StationPowerState.Offline);
                }
            }
            else if (type == StationSystemType.Terminal)
            {
                changed = systems?.SetCriticalSystemActive(type, active) == true;
            }
            else
            {
                changed = systems?.SetRequestedActive(
                    type,
                    active,
                    selectedObjectId) == true;
                if (changed)
                {
                    AntennaController.Instance?.RefreshAvailability();
                    DroneScanController.Instance?.RefreshAvailability();
                }
            }

            preserveAuthoredSwitchAnimation = changed;
            forcePowerHandleSync = !changed;
            RefreshPowerSwitch();
            RefreshStatus();
            if (!active &&
                (type == StationSystemType.Battery ||
                 type == StationSystemType.Terminal))
            {
                terminal?.Close();
            }
        }

        private void RefreshStatus()
        {
            if (statusText == null)
                return;
            if (!selectedSystem.HasValue)
            {
                TerminalUIUtility.SetText(
                    statusText,
                    Localize("station.no_object_selected", "NO OBJECT SELECTED"));
                return;
            }

            StationSystemType type = selectedSystem.Value;
            StationSystemsController systems = StationSystemsController.Instance;
            StationSystemDefinition definition = systems?.GetDefinition(
                type,
                selectedObjectId) ?? StationSystemsConfig.LoadDefault()?.Find(
                type,
                selectedObjectId);
            var builder = new StringBuilder();
            if (StationSystemsController.UsesCondition(type))
            {
                builder.Append(Localize(
                    "station.status.condition",
                    "Condition"));
                builder.Append(" - ");
                builder.Append(
                    ((systems?.GetCondition(type, selectedObjectId) ?? 1f) *
                     100f).ToString("F0"));
                builder.AppendLine("%");
            }

            if (definition != null)
            {
                foreach (StationObjectStatDefinition stat in definition.BaseStats)
                {
                    if (stat == null)
                        continue;
                    float value = systems?.GetStat(
                        type,
                        selectedObjectId,
                        stat.Stat,
                        stat.BaseValue) ?? stat.BaseValue;
                    string statKey = NERALocalization.NormalizeKeyPart(
                        stat.Stat.ToString());
                    builder.Append(Localize(
                        $"station.stat.{statKey}",
                        stat.DisplayName));
                    builder.Append(" - ");
                    if (type == StationSystemType.Battery &&
                        (stat.Stat == StationObjectStat.Capacity ||
                         stat.Stat == StationObjectStat.BackupReserve))
                    {
                        EnergySystemController energy =
                            EnergySystemController.Instance;
                        bool isMainBattery =
                            stat.Stat == StationObjectStat.Capacity;
                        float currentCharge = isMainBattery
                            ? energy?.CurrentEnergy ?? value
                            : energy?.CurrentBackupReserve ?? value;
                        float maximumCharge = isMainBattery
                            ? energy?.TotalCapacity ?? value
                            : energy?.TotalBackupReserve ?? value;
                        string numberFormat = $"F{stat.Decimals}";
                        builder.Append(currentCharge.ToString(numberFormat));
                        builder.Append('/');
                        builder.Append(maximumCharge.ToString(numberFormat));
                        if (!string.IsNullOrEmpty(stat.Unit))
                        {
                            builder.Append(' ');
                            builder.Append(stat.Unit);
                        }
                        builder.AppendLine();
                        continue;
                    }
                    builder.AppendLine(stat.Format(value));
                }
                if (type == StationSystemType.Battery)
                {
                    EnergySystemController energy =
                        EnergySystemController.Instance;
                    builder.Append(Localize(
                        "station.stat.currentconsumption",
                        "Current Consumption"));
                    builder.Append(" - ");
                    builder.Append(
                        (energy?.CurrentConsumption ?? 0f).ToString("F1"));
                    builder.AppendLine(" kW");
                }
                builder.Append(Localize(
                    "station.status.installed_parts",
                    "Installed parts"));
                builder.Append(" - ");
                builder.Append(systems?.GetInstalledPartCount(
                    type,
                    selectedObjectId) ?? 0);
                builder.Append('/');
                builder.Append(definition.Slots.Count);
            }
            TerminalUIUtility.SetText(statusText, builder.ToString());
        }

        private void BindDataEvents()
        {
            MaintainableObject.AnyConditionChanged -=
                HandleMaintainableConditionChanged;
            MaintainableObject.AnyConditionChanged +=
                HandleMaintainableConditionChanged;

            StationSystemsController currentSystems =
                StationSystemsController.Instance;
            if (subscribedSystems != currentSystems)
            {
                if (subscribedSystems != null)
                    subscribedSystems.SystemsChanged -= HandleDataChanged;
                subscribedSystems = currentSystems;
                if (subscribedSystems != null)
                    subscribedSystems.SystemsChanged += HandleDataChanged;
            }

            EnergySystemController currentEnergy = EnergySystemController.Instance;
            if (subscribedEnergy != currentEnergy)
            {
                if (subscribedEnergy != null)
                    subscribedEnergy.EnergyChanged -= HandleDataChanged;
                subscribedEnergy = currentEnergy;
                if (subscribedEnergy != null)
                    subscribedEnergy.EnergyChanged += HandleDataChanged;
            }

            DroneScanController drone = DroneScanController.Instance;
            if (subscribedDrone != drone)
            {
                if (subscribedDrone != null)
                {
                    subscribedDrone.StateChanged -= HandleDroneStateChanged;
                    subscribedDrone.StationPresenceChanged -=
                        HandleDronePresenceChanged;
                }
                subscribedDrone = drone;
                if (subscribedDrone != null)
                {
                    subscribedDrone.StateChanged += HandleDroneStateChanged;
                    subscribedDrone.StationPresenceChanged +=
                        HandleDronePresenceChanged;
                }
            }

            AntennaController antenna = AntennaController.Instance;
            if (subscribedAntenna != antenna)
            {
                if (subscribedAntenna != null)
                {
                    subscribedAntenna.StateChanged -= HandleAntennaStateChanged;
                    subscribedAntenna.ConditionChanged -=
                        HandleAntennaConditionChanged;
                }
                subscribedAntenna = antenna;
                if (subscribedAntenna != null)
                {
                    subscribedAntenna.StateChanged += HandleAntennaStateChanged;
                    subscribedAntenna.ConditionChanged +=
                        HandleAntennaConditionChanged;
                }
            }
        }

        private void HandleDataChanged()
        {
            RefreshIfVisible();
        }

        private void HandleDroneStateChanged(DroneState _)
        {
            RefreshIfVisible();
        }

        private void HandleDronePresenceChanged(bool _)
        {
            RefreshIfVisible();
        }

        private void HandleAntennaStateChanged(AntennaState _)
        {
            RefreshIfVisible();
        }

        private void HandleAntennaConditionChanged(float _)
        {
            RefreshIfVisible();
        }

        private void HandleMaintainableConditionChanged(string _, float __)
        {
            RefreshIfVisible();
        }

        private void RefreshIfVisible()
        {
            if (terminal?.IsOpen == true && gameObject.activeInHierarchy)
                dataRefreshPending = true;
        }

        private void UnbindDataEvents()
        {
            MaintainableObject.AnyConditionChanged -=
                HandleMaintainableConditionChanged;

            if (subscribedSystems != null)
            {
                subscribedSystems.SystemsChanged -= HandleDataChanged;
                subscribedSystems = null;
            }
            if (subscribedEnergy != null)
            {
                subscribedEnergy.EnergyChanged -= HandleDataChanged;
                subscribedEnergy = null;
            }
            if (subscribedDrone != null)
            {
                subscribedDrone.StateChanged -= HandleDroneStateChanged;
                subscribedDrone.StationPresenceChanged -=
                    HandleDronePresenceChanged;
                subscribedDrone = null;
            }
            if (subscribedAntenna != null)
            {
                subscribedAntenna.StateChanged -= HandleAntennaStateChanged;
                subscribedAntenna.ConditionChanged -=
                    HandleAntennaConditionChanged;
                subscribedAntenna = null;
            }
        }

        private static string FormatObjectName(string objectName)
        {
            return objectName
                .Replace("SM_", string.Empty)
                .Replace("_", " ")
                .ToUpperInvariant();
        }

        private static string Localize(
            string key,
            string fallback,
            params object[] arguments)
        {
            return NERALocalization.Get(
                NERALocalization.TerminalTable,
                key,
                fallback,
                arguments);
        }

        private void OnDestroy()
        {
            NERALocalization.LocaleChanged -= RefreshIfVisible;
            previewRenderer?.SetPreviewActive(false);
            UnbindDataEvents();
        }
    }
}
