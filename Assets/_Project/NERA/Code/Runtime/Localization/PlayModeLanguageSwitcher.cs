#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NERA.Localization
{
    /// <summary>
    /// Editor-only language switcher shown over every scene in Play Mode.
    /// It is intentionally excluded from player builds.
    /// </summary>
    public sealed class PlayModeLanguageSwitcher : MonoBehaviour
    {
        private const float MinimumMargin = 8f;
        private const float MinimumWidth = 176f;
        private const float MinimumHeight = 34f;

        private GUIStyle buttonStyle;

        public string ButtonText =>
            NERALocalization.CurrentLocaleCode.StartsWith(
                NERALocalization.RussianCode,
                System.StringComparison.OrdinalIgnoreCase)
                ? "[F8] LANGUAGE: RU"
                : "[F8] LANGUAGE: EN";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCreated()
        {
            if (FindFirstObjectByType<PlayModeLanguageSwitcher>() != null)
                return;

            GameObject root = new GameObject("PlayModeLanguageSwitcher");
            DontDestroyOnLoad(root);
            root.AddComponent<PlayModeLanguageSwitcher>();
        }

        public void Toggle()
        {
            NERALocalization.ToggleEnglishRussian();
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().name == "Boot")
                return;

            Event current = Event.current;
            if (current != null &&
                current.type == EventType.KeyDown &&
                current.keyCode == KeyCode.F8)
            {
                Toggle();
                current.Use();
            }

            buttonStyle ??= new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            buttonStyle.fontSize = Mathf.Clamp(
                Mathf.RoundToInt(Screen.height * 0.025f),
                14,
                36);
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;

            float margin = Mathf.Max(MinimumMargin, Screen.height * 0.015f);
            float width = Mathf.Max(MinimumWidth, Screen.width * 0.17f);
            float height = Mathf.Max(MinimumHeight, Screen.height * 0.06f);
            Rect buttonRect = new Rect(
                margin,
                Screen.height - height - margin,
                width,
                height);
            Color previousBackground = GUI.backgroundColor;
            int previousDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.backgroundColor = new Color(0.12f, 0.18f, 0.24f, 0.96f);
            if (GUI.Button(buttonRect, ButtonText, buttonStyle))
                Toggle();
            GUI.backgroundColor = previousBackground;
            GUI.depth = previousDepth;
        }
    }
}
#endif
