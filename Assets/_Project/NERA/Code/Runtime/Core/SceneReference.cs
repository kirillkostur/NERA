using System;
using System.IO;
using UnityEngine;

namespace NERA.Core
{
    /// <summary>
    /// Build-safe scene reference. The editor drawer stores the asset GUID and
    /// path, while runtime code consumes the scene name derived from that path.
    /// </summary>
    [Serializable]
    public sealed class SceneReference
    {
        [SerializeField, HideInInspector] private string assetGuid;
        [SerializeField, HideInInspector] private string assetPath;

        public string AssetGuid => assetGuid?.Trim() ?? string.Empty;
        public string ScenePath => assetPath?.Trim() ?? string.Empty;
        public string SceneName => string.IsNullOrWhiteSpace(ScenePath)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(ScenePath);
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AssetGuid) &&
            !string.IsNullOrWhiteSpace(ScenePath);
    }
}
