using UnityEngine;
using UnityEngine.UI;

namespace NERA.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ResponsiveCanvasLayout : MonoBehaviour
    {
        public static readonly Vector2 DefaultReferenceResolution =
            new Vector2(1920f, 1080f);

        [SerializeField] private Vector2 referenceResolution =
            new Vector2(1920f, 1080f);

        private CanvasScaler scaler;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;
        private bool applying;

        private void Awake()
        {
            CacheComponents();
            ApplyNow();
        }

        private void OnEnable()
        {
            ApplyNow();
        }

        private void Update()
        {
            if (Screen.width != lastScreenWidth ||
                Screen.height != lastScreenHeight)
            {
                ApplyNow();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && !applying)
                ApplyNow();
        }

        public void ApplyNow()
        {
            if (applying)
                return;

            applying = true;
            CacheComponents();
            ConfigureCanvasScale();
            Canvas.ForceUpdateCanvases();
            if (transform is RectTransform root)
                LayoutRebuilder.MarkLayoutForRebuild(root);
            applying = false;
        }

        public static float CalculateMatchWidthOrHeight(
            float screenWidth,
            float screenHeight,
            Vector2 reference)
        {
            float safeHeight = Mathf.Max(1f, screenHeight);
            float safeReferenceHeight = Mathf.Max(1f, reference.y);
            float screenAspect = Mathf.Max(1f, screenWidth) / safeHeight;
            float referenceAspect = Mathf.Max(1f, reference.x) /
                safeReferenceHeight;
            return screenAspect <= referenceAspect ? 0f : 1f;
        }

        private void CacheComponents()
        {
            scaler ??= GetComponent<CanvasScaler>();
        }

        private void ConfigureCanvasScale()
        {
            if (scaler == null)
                return;

            if (referenceResolution.x <= 0f || referenceResolution.y <= 0f)
                referenceResolution = DefaultReferenceResolution;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = CalculateMatchWidthOrHeight(
                Screen.width,
                Screen.height,
                referenceResolution);
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }

    }
}
