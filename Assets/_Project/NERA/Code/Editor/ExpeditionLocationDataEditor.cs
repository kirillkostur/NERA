using NERA.Expeditions;
using NERA.Locations;
using UnityEditor;

namespace NERA.Editor
{
    [CustomEditor(typeof(ExpeditionLocationData))]
    [CanEditMultipleObjects]
    public sealed class ExpeditionLocationDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty discoverySource =
                serializedObject.FindProperty("discoverySource");
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (ShouldHideDiscoveryProperty(
                        property.propertyPath,
                        discoverySource))
                {
                    continue;
                }

                using (new EditorGUI.DisabledScope(
                           property.propertyPath == "m_Script"))
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static bool ShouldHideDiscoveryProperty(
            string propertyPath,
            SerializedProperty discoverySource)
        {
            if (discoverySource.hasMultipleDifferentValues)
                return false;

            DiscoverySource source =
                (DiscoverySource)discoverySource.enumValueIndex;
            switch (propertyPath)
            {
                case "droneScanDuration":
                case "requiredDroneUpgradeLevel":
                    return source != DiscoverySource.Drone;
                case "antennaScanDuration":
                case "requiredAntennaUpgradeLevel":
                    return source != DiscoverySource.Antenna;
                default:
                    return false;
            }
        }
    }
}
