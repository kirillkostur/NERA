using NERA.Items;
using NERA.Editor.Localization;
using UnityEditor;
using UnityEngine;

namespace NERA.Editor
{
    [CustomEditor(typeof(ItemData))]
    [CanEditMultipleObjects]
    public sealed class ItemDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty itemType =
                serializedObject.FindProperty("itemType");
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                bool showEquipmentOptions =
                    itemType.hasMultipleDifferentValues ||
                    itemType.enumValueIndex == (int)ItemType.Equipment;
                if (!showEquipmentOptions &&
                    IsEquipmentOnlyProperty(property.propertyPath))
                {
                    continue;
                }

                bool showAnomalyOptions =
                    itemType.hasMultipleDifferentValues ||
                    itemType.enumValueIndex == (int)ItemType.Anomaly;
                if (!showAnomalyOptions &&
                    IsAnomalyOnlyProperty(property.propertyPath))
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
            DrawLocalizationSection();
        }

        private void DrawLocalizationSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);

            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox(
                    "Select one ItemData asset to see its localization keys.",
                    MessageType.Info);
            }
            else
            {
                string itemId = serializedObject.FindProperty("itemId")
                    .stringValue.Trim();
                if (string.IsNullOrEmpty(itemId))
                {
                    EditorGUILayout.HelpBox(
                        "Set Item Id before synchronizing localization.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        "Name key",
                        $"item.{itemId}.name");
                    EditorGUILayout.LabelField(
                        "Description key",
                        $"item.{itemId}.description");
                }
            }

            EditorGUILayout.HelpBox(
                "Translations are stored in Localization/StringTables/Content. " +
                "Sync creates missing item entries and keeps existing Russian text.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(
                       serializedObject.isEditingMultipleObjects))
            {
                if (GUILayout.Button("Sync Item Localization"))
                    NERALocalizationSetup.SyncItemTables();
            }

            if (GUILayout.Button("Select Content String Table"))
                NERALocalizationSetup.SelectContentTable();
        }

        private static bool IsEquipmentOnlyProperty(string propertyPath)
        {
            switch (propertyPath)
            {
                case "equippedVisualPrefab":
                case "equipmentAnchorName":
                case "equippedLocalPosition":
                case "equippedLocalEulerAngles":
                case "quickAccessAction":
                case "useKey":
                case "acceptsAnomalyIntegration":
                case "weaponDefinition":
                case "energyDefinition":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAnomalyOnlyProperty(string propertyPath)
        {
            return propertyPath == "anomalyIntegrationDefinition";
        }
    }
}
