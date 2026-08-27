using System.Collections;
using NERA.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.UI
{
    [DisallowMultipleComponent]
    public sealed class LoadingScreenController : MonoBehaviour
    {
        public const string PrefabResourcePath = "UI/P_LoadingScreen";

        [Header("Content")]
        [SerializeField] private LoadingScreenConfig config;

        [Header("Authored UI")]
        [SerializeField] private Camera loadingCamera;
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private RawImage loadingImage;
        [SerializeField] private AspectRatioFitter imageAspectRatio;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private TMP_Text loadingText;

        private static LoadingScreenController instance;

        private int activeRequests;
        private int selectedTipIndex = -1;
        private float shownAtRealtime;
        private Coroutine hideRoutine;

        public static LoadingScreenController Instance => instance;
        public bool IsVisible => windowRoot != null && windowRoot.activeSelf;
        public int ActiveRequestCount => activeRequests;
        public LoadingScreenConfig Config => config;
        public Camera LoadingCamera => loadingCamera;
        public Texture CurrentImage => loadingImage != null
            ? loadingImage.texture
            : null;
        public string CurrentTipText => tipText != null
            ? tipText.text
            : string.Empty;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SetPresentationActive(false);
            NERALocalization.EnsureInitialized();
        }

        private void OnEnable()
        {
            NERALocalization.LocaleChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            NERALocalization.LocaleChanged -= RefreshLocalizedText;
        }

        public static bool BeginLoading()
        {
            LoadingScreenController controller = EnsureInstance();
            if (controller == null)
                return false;

            controller.Acquire();
            return true;
        }

        public static void EndLoading()
        {
            if (instance != null)
                instance.Release();
        }

        public static IEnumerator EndLoadingAndWait()
        {
            LoadingScreenController controller = instance;
            if (controller == null)
                yield break;

            controller.Release();
            while (controller != null && controller.IsVisible)
                yield return null;
        }

        public static IEnumerator WaitForMinimumDisplayTime()
        {
            LoadingScreenController controller = instance;
            if (controller == null || !controller.IsVisible)
                yield break;

            while (controller != null && !controller.MinimumTimeElapsed)
                yield return null;
        }

        private static LoadingScreenController EnsureInstance()
        {
            if (instance != null)
                return instance;

            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError(
                    $"Loading screen prefab was not found at Resources/" +
                    $"{PrefabResourcePath}.");
                return null;
            }

            GameObject created = Instantiate(prefab);
            instance = created.GetComponent<LoadingScreenController>();
            if (instance == null)
            {
                Debug.LogError(
                    "Loading screen prefab has no LoadingScreenController.",
                    created);
                Destroy(created);
            }

            return instance;
        }

        private bool MinimumTimeElapsed
        {
            get
            {
                float minimum = config != null
                    ? config.MinimumDisplaySeconds
                    : 0f;
                return Time.realtimeSinceStartup - shownAtRealtime >= minimum;
            }
        }

        private void Acquire()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            if (activeRequests == 0)
            {
                SelectRandomContent();
                shownAtRealtime = Time.realtimeSinceStartup;
                SetPresentationActive(true);
            }

            activeRequests++;
        }

        private void Release()
        {
            if (activeRequests <= 0)
                return;

            activeRequests--;
            if (activeRequests > 0)
                return;

            if (hideRoutine != null)
                StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideWhenReady());
        }

        private IEnumerator HideWhenReady()
        {
            while (activeRequests == 0 && !MinimumTimeElapsed)
                yield return null;

            if (activeRequests == 0)
                SetPresentationActive(false);
            hideRoutine = null;
        }

        private void SetPresentationActive(bool active)
        {
            if (loadingCamera != null)
                loadingCamera.enabled = active;
            if (windowRoot != null)
                windowRoot.SetActive(active);
        }

        private void SelectRandomContent()
        {
            if (loadingImage != null)
            {
                Texture2D texture = SelectRandomImage();
                loadingImage.texture = texture;
                loadingImage.enabled = texture != null;
                if (texture != null && imageAspectRatio != null)
                {
                    imageAspectRatio.aspectRatio = texture.height > 0
                        ? (float)texture.width / texture.height
                        : 16f / 9f;
                }
            }

            selectedTipIndex = SelectRandomTipIndex();
            RefreshLocalizedText();
        }

        private Texture2D SelectRandomImage()
        {
            if (config == null || config.Images == null ||
                config.Images.Count == 0)
            {
                return null;
            }

            int start = Random.Range(0, config.Images.Count);
            for (int offset = 0; offset < config.Images.Count; offset++)
            {
                Texture2D candidate =
                    config.Images[(start + offset) % config.Images.Count];
                if (candidate != null)
                    return candidate;
            }

            return null;
        }

        private int SelectRandomTipIndex()
        {
            if (config == null || config.Tips == null ||
                config.Tips.Count == 0)
            {
                return -1;
            }

            int start = Random.Range(0, config.Tips.Count);
            for (int offset = 0; offset < config.Tips.Count; offset++)
            {
                int index = (start + offset) % config.Tips.Count;
                if (config.Tips[index] != null)
                    return index;
            }

            return -1;
        }

        private void RefreshLocalizedText()
        {
            if (loadingText != null)
            {
                loadingText.text = NERALocalization.Get(
                    NERALocalization.HudTable,
                    "loading.status",
                    "LOADING...");
            }

            if (tipText == null)
                return;

            tipText.text = config != null && selectedTipIndex >= 0 &&
                           selectedTipIndex < config.Tips.Count
                ? config.Tips[selectedTipIndex].GetLocalizedText()
                : string.Empty;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
