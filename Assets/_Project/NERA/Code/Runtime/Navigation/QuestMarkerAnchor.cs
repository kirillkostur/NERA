using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Navigation
{
    [DisallowMultipleComponent]
    public sealed class QuestMarkerAnchor : MonoBehaviour
    {
        private static readonly HashSet<QuestMarkerAnchor> Registered =
            new HashSet<QuestMarkerAnchor>();

        [Header("Identity")]
        [Tooltip(
            "Stable ID referenced by a quest stage. IDs are case-insensitive.")]
        [SerializeField] private string markerId;

        [Header("Position")]
        [Tooltip(
            "Optional source transform. Leave empty to use this object.")]
        [SerializeField] private Transform positionSource;
        [Tooltip("Offset in the source transform's local space.")]
        [SerializeField] private Vector3 localOffset =
            new Vector3(0f, 1.5f, 0f);

        [Header("Presentation")]
        [SerializeField] private Sprite icon;
        [SerializeField] private Color color =
            new Color(0.12f, 0.9f, 1f, 1f);
        [SerializeField] private bool showDistance = true;
        [Tooltip(
            "The screen-space marker is hidden when the player is closer " +
            "than this distance. The compass marker remains visible.")]
        [SerializeField, Min(0f)] private float worldMarkerFadeDistance = 2f;
        [Tooltip(
            "The screen-space marker is hidden beyond this distance. " +
            "The compass marker remains visible.")]
        [SerializeField, Min(0.1f)] private float worldMarkerMaxDistance = 50f;

        [Header("Availability")]
        [Tooltip(
            "Show this marker even when no active quest stage references it.")]
        [SerializeField] private bool availableWithoutQuest;
        [SerializeField] private bool available = true;

        public static event Action RegistryChanged;

        public string MarkerId => NormalizeId(markerId);
        public Sprite Icon => icon;
        public Color Color => color;
        public bool ShowDistance => showDistance;
        public float WorldMarkerFadeDistance =>
            Mathf.Clamp(worldMarkerFadeDistance, 0f, WorldMarkerMaxDistance);
        public float WorldMarkerMaxDistance =>
            Mathf.Max(0.1f, worldMarkerMaxDistance);
        public bool AvailableWithoutQuest => availableWithoutQuest;
        public bool IsAvailable => available && isActiveAndEnabled;
        public Vector3 WorldPosition
        {
            get
            {
                Transform source = positionSource != null
                    ? positionSource
                    : transform;
                return source.TransformPoint(localOffset);
            }
        }

        public void SetAvailable(bool value)
        {
            if (available == value)
                return;

            available = value;
            RegistryChanged?.Invoke();
        }

        public static string NormalizeId(string value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        public static string ResolveStageId(
            string value,
            string questId,
            string contextTargetId)
        {
            return NormalizeId((value ?? string.Empty)
                .Replace("{questId}", questId ?? string.Empty)
                .Replace("{targetId}", contextTargetId ?? string.Empty));
        }

        public static void CopyRegisteredTo(List<QuestMarkerAnchor> result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            Registered.RemoveWhere(anchor => anchor == null);
            result.Clear();
            foreach (QuestMarkerAnchor anchor in Registered)
            {
                if (anchor != null)
                    result.Add(anchor);
            }

            result.Sort((left, right) =>
                left.GetInstanceID().CompareTo(right.GetInstanceID()));
        }

        private void OnEnable()
        {
            if (Registered.Add(this))
                RegistryChanged?.Invoke();
        }

        private void OnDisable()
        {
            if (Registered.Remove(this))
                RegistryChanged?.Invoke();
        }

        private void OnValidate()
        {
            markerId = NormalizeId(markerId);
            worldMarkerMaxDistance = Mathf.Max(
                0.1f,
                worldMarkerMaxDistance);
            worldMarkerFadeDistance = Mathf.Clamp(
                worldMarkerFadeDistance,
                0f,
                worldMarkerMaxDistance);
            if (isActiveAndEnabled)
                RegistryChanged?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 position = WorldPosition;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(position, 0.22f);
            Gizmos.DrawLine(position + Vector3.left * 0.35f,
                position + Vector3.right * 0.35f);
            Gizmos.DrawLine(position + Vector3.down * 0.35f,
                position + Vector3.up * 0.35f);
        }
    }
}
