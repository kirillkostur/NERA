using NERA.Items;
using UnityEditor;

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

                using (new EditorGUI.DisabledScope(
                           property.propertyPath == "m_Script"))
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsEquipmentOnlyProperty(string propertyPath)
        {
            switch (propertyPath)
            {
                case "acceptsAnomalyIntegration":
                case "anomalyIntegrationDefinition":
                case "weaponDefinition":
                case "energyDefinition":
                    return true;
                default:
                    return false;
            }
        }
    }
}
