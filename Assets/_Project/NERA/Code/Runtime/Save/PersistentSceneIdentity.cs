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

            if (string.IsNullOrWhiteSpace(authoredId))
                return string.Empty;

            string sceneName = target.gameObject.scene.name;
            string localId = authoredId.Trim();
            return Normalize($"{sceneName}/{localId}");
        }

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/').ToLowerInvariant();
        }

    }
}
