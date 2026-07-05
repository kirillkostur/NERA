using TMPro;
using UnityEngine;

public class InteractionPromptUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text promptText;

    [Header("Animation")]
    [SerializeField] private float fadeSpeed = 12f;

    private bool isVisible;
    private float targetAlpha;

    private void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        promptText = GetComponentInChildren<TMP_Text>();
    }

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        HideInstant();
    }

    private void Update()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = Mathf.MoveTowards(
            canvasGroup.alpha,
            targetAlpha,
            fadeSpeed * Time.deltaTime
        );
    }

    public void Show(string text)
    {
        if (promptText != null)
            promptText.text = text;

        isVisible = true;
        targetAlpha = 1f;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void Hide()
    {
        isVisible = false;
        targetAlpha = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void HideInstant()
    {
        isVisible = false;
        targetAlpha = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public bool IsVisible()
    {
        return isVisible;
    }
}