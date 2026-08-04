using System.Collections.Generic;
using UnityEngine;

namespace NERA.Save
{
    public static class PersistentSceneIdentity
    {
        public static string CreateKey(
            Transform target,
            string authoredId = null)
        {
            if (target == null)
                return string.Empty;

            string sceneName = target.gameObject.scene.name;
            string localId = string.IsNullOrWhiteSpace(authoredId)
                ? BuildHierarchyPath(target)
                : authoredId.Trim();
            return Normalize($"{sceneName}/{localId}");
        }

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/').ToLowerInvariant();
        }

        private static string BuildHierarchyPath(Transform target)
        {
            var segments = new List<string>();
            Transform current = target;
            while (current != null)
            {
                segments.Add($"{current.name}[{current.GetSiblingIndex()}]");
                current = current.parent;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
