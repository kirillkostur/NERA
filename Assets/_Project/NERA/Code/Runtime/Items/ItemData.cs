using UnityEngine;
using NERA.Research;
using NERA.Combat;

namespace NERA.Items
{
    public enum QuickAccessAction
    {
        None,
        ToggleLight,
        Scan,
        Fire
    }

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

        [Header("Equipment")]
        [SerializeField] private string equipmentAnchorName = "mixamorig1:RightHand";
        [SerializeField] private Vector3 equippedLocalPosition;
        [SerializeField] private Vector3 equippedLocalEulerAngles;
        [SerializeField] private QuickAccessAction quickAccessAction;
        [SerializeField] private KeyCode useKey = KeyCode.Mouse0;

        [Header("Research")]
        [SerializeField] private ResearchDefinition researchDefinition;

        [Header("Anomaly Integration")]
        [SerializeField] private bool acceptsAnomalyIntegration;
        [SerializeField]
        private AnomalyIntegrationDefinition anomalyIntegrationDefinition;

        [Header("Weapon")]
        [SerializeField] private WeaponDefinition weaponDefinition;

        [Header("Energy")]
        [SerializeField] private ItemEnergyDefinition energyDefinition;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Description => description;
        public ItemType ItemType => itemType;
        public Sprite Icon => icon;
        public WorldItem WorldPrefab => worldPrefab;
        public GameObject EquippedVisualPrefab => equippedVisualPrefab;
        public string EquipmentAnchorName => equipmentAnchorName;
        public Vector3 EquippedLocalPosition => equippedLocalPosition;
        public Vector3 EquippedLocalEulerAngles => equippedLocalEulerAngles;
        public QuickAccessAction QuickAccessAction => quickAccessAction;
        public KeyCode UseKey => useKey;
        public ResearchDefinition ResearchDefinition => researchDefinition;
        public bool AcceptsAnomalyIntegration =>
            acceptsAnomalyIntegration;
        public AnomalyIntegrationDefinition AnomalyIntegrationDefinition =>
            anomalyIntegrationDefinition;
        public WeaponDefinition WeaponDefinition => weaponDefinition;
        public ItemEnergyDefinition EnergyDefinition => energyDefinition;

        private void OnValidate()
        {
            itemId = itemId?.Trim();
            displayName = displayName?.Trim();
        }
    }
}
