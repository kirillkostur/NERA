using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class ObjectiveUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text objectiveText;

    [Header("Behavior")]
    [SerializeField] private bool hideWhenNoObjective = true;
    [SerializeField] private bool hideOnSceneWithoutObjectiveController = true;

    private ExpeditionObjectiveController currentController;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        HideInstant();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        SubscribeLocalization();
        TrySubscribeToController();
        Refresh();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        UnsubscribeFromController();
        UnsubscribeLocalization();
    }

    private void Update()
    {
        if (currentController == null)
            TrySubscribeToController();

        if (hideOnSceneWithoutObjectiveController && currentController == null)
            Hide();
    }

    private void TrySubscribeToController()
    {
        ExpeditionObjectiveController controller = ExpeditionObjectiveController.Instance;

        if (controller == null)
            return;

        if (controller == currentController)
            return;

        UnsubscribeFromController();

        currentController = controller;
        currentController.ObjectiveChanged += Refresh;

        Refresh();
    }

    private void UnsubscribeFromController()
    {
        if (currentController != null)
            currentController.ObjectiveChanged -= Refresh;

        currentController = null;
    }

    private void SubscribeLocalization()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeLocalization()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(GameLanguage language)
    {
        Refresh();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnsubscribeFromController();

        TrySubscribeToController();

        if (currentController == null)
        {
            if (hideOnSceneWithoutObjectiveController)
                Hide();

            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (objectiveText == null)
            return;

        if (currentController == null)
        {
            if (hideWhenNoObjective)
                Hide();

            return;
        }

        string text = currentController.GetCurrentText();

        if (string.IsNullOrWhiteSpace(text))
        {
            if (hideWhenNoObjective)
                Hide();

            return;
        }

        objectiveText.text = text;
        Show();
    }

    private void Show()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void Hide()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void HideInstant()
    {
        Hide();
    }
}