using UnityEngine;

namespace NERA.Station
{
    /// <summary>
    /// Shows one authored Stage_N child according to the installed station
    /// upgrade level. Stage contents remain fully editable in the prefab.
    /// </summary>
    public sealed class StationUpgradeStageController : MonoBehaviour
    {
        [SerializeField] private StationSystemType systemType;
        [SerializeField, Min(0)] private int maxStage = 3;
        [SerializeField] private string objectId;
        [SerializeField, Min(0)] private int initialStage;
        [SerializeField] private Transform stageContainer;

        private StationSystemsController subscribedSystems;

        public StationSystemType SystemType => systemType;
        public int MaxStage => Mathf.Max(0, maxStage);
        public string ObjectId => ResolveObjectId();
        public int InitialStage => ResolveInitialStage();
        public int CurrentStage { get; private set; }

        private void OnEnable()
        {
            BindSystems();
            RefreshVisuals();
        }

        private void Start()
        {
            BindSystems();
            RefreshVisuals();
        }

        private void Update()
        {
            if (subscribedSystems != StationSystemsController.Instance)
                BindSystems();
        }

        public void RefreshVisuals()
        {
            StationSystemsController systems =
                StationSystemsController.Instance;
            int level = systems != null
                ? systems.GetUpgradeLevel(
                    systemType,
                    ResolveObjectId(),
                    ResolveInitialStage())
                : StationSystemsConfig.LoadDefault()
                    .Find(
                        systemType,
                        ResolveObjectId())?.InitialLevel ??
                    ResolveInitialStage();
            CurrentStage = Mathf.Clamp(level, 0, Mathf.Max(0, maxStage));

            Transform container = stageContainer != null
                ? stageContainer
                : transform;
            for (int stage = 0; stage <= maxStage; stage++)
            {
                Transform stageRoot = container.Find($"Stage_{stage}");
                if (stageRoot != null)
                    stageRoot.gameObject.SetActive(stage == CurrentStage);
            }
        }

        private void BindSystems()
        {
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= RefreshVisuals;

            subscribedSystems = StationSystemsController.Instance;
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged += RefreshVisuals;
            RefreshVisuals();
        }

        private string ResolveObjectId()
        {
            if (!string.IsNullOrWhiteSpace(objectId))
                return objectId.Trim();

            StationTurretController turret =
                GetComponentInParent<StationTurretController>();
            return turret != null ? turret.TurretId : string.Empty;
        }

        private int ResolveInitialStage()
        {
            StationTurretController turret =
                GetComponentInParent<StationTurretController>();
            return turret != null
                ? turret.InitialUpgradeLevel
                : Mathf.Max(0, initialStage);
        }

        private void OnDisable()
        {
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= RefreshVisuals;
            subscribedSystems = null;
        }
    }
}
