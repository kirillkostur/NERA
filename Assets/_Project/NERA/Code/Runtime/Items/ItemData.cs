using UnityEngine;
using NERA.Research;

namespace NERA.Items
{
    [CreateAssetMenu(
        fileName = "Item_New",
        menuName = "NERA/Items/Item Data"
    )]
    public sealed class ItemData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;

        [Header("Description")]
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private ItemType itemType;
        [SerializeField] private Sprite icon;

        [Header("Prefabs")]
        [SerializeField] private WorldItem worldPrefab;
        [SerializeField] private GameObject equippedVisualPrefab;

        [Header("Research")]
        [SerializeField] private ResearchDefinition researchDefinition;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public ItemType ItemType => itemType;
        public Sprite Icon => icon;
        public WorldItem WorldPrefab => worldPrefab;
        public GameObject EquippedVisualPrefab => equippedVisualPrefab;
        public ResearchDefinition ResearchDefinition => researchDefinition;

        private void OnValidate()
        {
            itemId = itemId?.Trim();
            displayName = displayName?.Trim();
        }
    }
}
