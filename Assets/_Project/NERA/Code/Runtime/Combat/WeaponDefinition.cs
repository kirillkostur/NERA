using UnityEngine;

namespace NERA.Combat
{
    [CreateAssetMenu(
        fileName = "Weapon_NewEnergyWeapon",
        menuName = "NERA/Combat/Weapon Definition"
    )]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string weaponId;
        [SerializeField] private string displayName;

        [Header("Shot")]
        [SerializeField, Min(0.1f)] private float damage = 10f;
        [SerializeField, Min(0.1f)] private float range = 18f;
        [SerializeField, Min(0.01f)] private float cooldown = 0.55f;
        [Tooltip("Layers hit by the beam. Include Enemy and blocking geometry, " +
                 "but exclude Player so the camera ray cannot hit its owner.")]
        [SerializeField] private LayerMask hitMask =
            (1 << 0) | (1 << 6) | (1 << 7) | (1 << 8) |
            (1 << 9) | (1 << 10) | (1 << 11) |
            (1 << 14) | (1 << 15);

        [Header("Debug Visual")]
        [SerializeField] private Color beamColor = new Color(0.1f, 0.8f, 1f);
        [SerializeField, Min(0f)] private float debugBeamDuration = 0.08f;

        public string WeaponId => weaponId;
        public string DisplayName => displayName;
        public float Damage => Mathf.Max(0.1f, damage);
        public float Range => Mathf.Max(0.1f, range);
        public float Cooldown => Mathf.Max(0.01f, cooldown);
        public LayerMask HitMask => hitMask;
        public Color BeamColor => beamColor;
        public float DebugBeamDuration => Mathf.Max(0f, debugBeamDuration);

        private void OnValidate()
        {
            weaponId = weaponId?.Trim();
            displayName = displayName?.Trim();
            damage = Mathf.Max(0.1f, damage);
            range = Mathf.Max(0.1f, range);
            cooldown = Mathf.Max(0.01f, cooldown);
            debugBeamDuration = Mathf.Max(0f, debugBeamDuration);
        }
    }
}
