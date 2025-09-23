using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class AlertManager : MonoBehaviour
{
    public static AlertManager Instance;

    [Header("UI Elements")]
    public TMP_Text alertText;         // Текст уведомления
    public CanvasGroup canvasGroup;    // CanvasGroup для fade

    [Header("Настройки")]
    public float fadeDuration = 0.5f;  // Время появления/исчезновения
    public float displayTime = 2f;     // Время показа текста

    // Очередь сообщений
    private readonly LinkedList<(string message, bool critical)> alertQueue = new();
    private bool isDisplaying = false;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (alertText != null) alertText.text = "";
    }

    /// <summary>
    /// Добавить уведомление в очередь.
    /// </summary>
    public void ShowAlert(string message, bool critical = false)
    {
        if (Instance == null) return; // объект уже уничтожен

        if (critical)
            alertQueue.AddFirst((message, true));
        else
            alertQueue.AddLast((message, false));

        if (!isDisplaying && gameObject != null)
            StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        isDisplaying = true;

        while (alertQueue.Count > 0)
        {
            var entry = alertQueue.First.Value;
            alertQueue.RemoveFirst();

            if (this != null) // проверка что объект не уничтожен
                yield return StartCoroutine(DisplayAlert(entry.message, entry.critical));
        }

        isDisplaying = false;
    }

    private IEnumerator DisplayAlert(string message, bool critical)
    {
        if (alertText != null)
        {
            alertText.text = message;
            alertText.color = critical ? Color.red : Color.white;
        }

        // Fade in
        yield return StartCoroutine(FadeCanvas(0f, 1f, fadeDuration));

        // Ждём, пока текст виден
        yield return new WaitForSeconds(displayTime);

        // Fade out
        yield return StartCoroutine(FadeCanvas(1f, 0f, fadeDuration));

        if (alertText != null) alertText.text = "";
    }

    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
