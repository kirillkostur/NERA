using NERA.Energy;
using NERA.Graphics;
using UnityEngine;

namespace NERA.Inventory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerStationEquipmentCharger : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float exposurePointHeight = 1f;

        private PlayerInventory inventory;

        public bool IsInsidePlayerStation { get; private set; }

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
        }

        private void Update()
        {
            Vector3 exposurePoint =
                transform.position + Vector3.up * exposurePointHeight;
            bool inside =
                StationEnvironmentController.IsPlayerStationSceneActive &&
                FogExclusionVolume.IsWorldPointExcluded(exposurePoint);
            AdvanceCharging(Time.deltaTime, inside);
        }

        public float AdvanceCharging(
            float deltaTime,
            bool isInsidePlayerStation)
        {
            IsInsidePlayerStation = isInsidePlayerStation;
            if (!isInsidePlayerStation || deltaTime <= 0f)
                return 0f;

            inventory ??= GetComponent<PlayerInventory>();
            return inventory != null
                ? inventory.RechargePermanentEquipment(deltaTime)
                : 0f;
        }
    }
}
