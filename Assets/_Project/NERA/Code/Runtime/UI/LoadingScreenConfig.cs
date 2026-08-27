using System;
using System.Collections.Generic;
using NERA.Localization;
using UnityEngine;

namespace NERA.UI
{
    [Serializable]
    public sealed class LoadingTipDefinition
    {
        [SerializeField] private string localizationKey;
        [SerializeField, TextArea(2, 3)] private string fallbackText;

        public string LocalizationKey => localizationKey;
        public string FallbackText => fallbackText;

        public string GetLocalizedText()
        {
            return NERALocalization.Get(
                NERALocalization.HudTable,
                localizationKey,
                fallbackText);
        }
    }

    [CreateAssetMenu(
        fileName = "CFG_LoadingScreen",
        menuName = "NERA/UI/Loading Screen Config")]
    public sealed class LoadingScreenConfig : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] private float minimumDisplaySeconds = 3f;

        [Header("Independent Random Pools")]
        [SerializeField] private Texture2D[] images = Array.Empty<Texture2D>();
        [SerializeField] private LoadingTipDefinition[] tips =
            Array.Empty<LoadingTipDefinition>();

        public float MinimumDisplaySeconds => minimumDisplaySeconds;
        public IReadOnlyList<Texture2D> Images => images;
        public IReadOnlyList<LoadingTipDefinition> Tips => tips;
    }
}
