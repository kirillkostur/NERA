using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace NERA.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class SequentialEllipsisText : MonoBehaviour
    {
        private const int DotCount = 3;

        [SerializeField, Min(0.05f)] private float stepSeconds = 0.22f;
        [SerializeField, Range(0, 255)] private int inactiveDotAlpha = 72;

        private TMP_Text label;
        private string baseText = string.Empty;
        private string lastRenderedText = string.Empty;
        private int activeDotIndex = -1;

        public string BaseText => baseText;
        public int ActiveDotIndex => activeDotIndex;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();
            CaptureExternalText();
        }

        private void OnEnable()
        {
            label ??= GetComponent<TMP_Text>();
            if (label != null && label.text != lastRenderedText)
                CaptureExternalText();

            activeDotIndex = -1;
            RefreshAnimationFrame();
        }

        private void Update()
        {
            if (label == null)
                return;

            if (label.text != lastRenderedText)
                CaptureExternalText();

            RefreshAnimationFrame();
        }

        public void SetBaseText(string value)
        {
            label ??= GetComponent<TMP_Text>();
            baseText = StripTrailingEllipsis(value);
            activeDotIndex = -1;
            RefreshAnimationFrame();
        }

        private void CaptureExternalText()
        {
            if (label == null)
                return;

            baseText = StripTrailingEllipsis(label.text);
            lastRenderedText = label.text;
        }

        private void RefreshAnimationFrame()
        {
            if (label == null)
                return;

            int frame = Mathf.FloorToInt(
                Time.unscaledTime / Mathf.Max(0.05f, stepSeconds)) % DotCount;
            if (frame == activeDotIndex && label.text == lastRenderedText)
                return;

            activeDotIndex = frame;
            lastRenderedText = BuildAnimatedText(frame);
            label.text = lastRenderedText;
        }

        private string BuildAnimatedText(int highlightedDot)
        {
            var builder = new StringBuilder(baseText.Length + 64);
            builder.Append(baseText);
            for (int index = 0; index < DotCount; index++)
            {
                int alpha = index == highlightedDot
                    ? 255
                    : inactiveDotAlpha;
                builder.Append("<alpha=#");
                builder.Append(alpha.ToString("X2"));
                builder.Append(">.");
            }
            builder.Append("<alpha=#FF>");
            return builder.ToString();
        }

        private static string StripTrailingEllipsis(string value)
        {
            string result = (value ?? string.Empty).TrimEnd();
            if (result.EndsWith("…", StringComparison.Ordinal))
                return result.Substring(0, result.Length - 1).TrimEnd();

            int removedDots = 0;
            while (removedDots < DotCount &&
                   result.EndsWith(".", StringComparison.Ordinal))
            {
                result = result.Substring(0, result.Length - 1);
                removedDots++;
            }

            return result.TrimEnd();
        }
    }
}
