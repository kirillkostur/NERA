using NERA.Items;
using UnityEngine;

namespace NERA.Station
{
    [DisallowMultipleComponent]
    public sealed class StationUpgradeSlot : MonoBehaviour
    {
        [SerializeField] private string slotId;
        [SerializeField] private GameObject fakeVisual;
        [Header("Dynamic Part Mount")]
        [SerializeField] private Vector3 installedLocalPosition;
        [SerializeField] private Vector3 installedLocalEulerAngles;
        [SerializeField] private Vector3 installedLocalScale = Vector3.one;

        private GameObject runtimeVisual;
        private bool upgradeModeActive;
        private bool hasPart;
        private bool showEmptyFake;

        public string SlotId => slotId?.Trim() ?? string.Empty;
        public GameObject FakeVisual => fakeVisual;

        private void Awake()
        {
            ResolveReferences();
        }

        public void Configure(string id, GameObject fake)
        {
            slotId = id?.Trim() ?? string.Empty;
            fakeVisual = fake;
            ResolveReferences();
        }

        public void ConfigureMount(
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            installedLocalPosition = localPosition;
            installedLocalEulerAngles = localEulerAngles;
            installedLocalScale = localScale;
        }

        public bool Owns(Collider collider)
        {
            return collider != null && collider.transform.IsChildOf(transform);
        }

        public void ShowEmpty()
        {
            ShowEmpty(true);
        }

        public void ShowEmpty(bool showFake)
        {
            ResolveReferences();
            ClearRuntimeVisual();
            hasPart = false;
            showEmptyFake = showFake;
            RefreshFakeVisual();
        }

        public void ShowPart(ItemData item)
        {
            ResolveReferences();
            ClearRuntimeVisual();
            hasPart = item != null;
            showEmptyFake = false;
            RefreshFakeVisual();

            if (item == null)
                return;

            GameObject prefab = item.EngineeringPartDefinition?
                .InstalledVisualPrefab;
            if (prefab == null && item.WorldPrefab != null)
                prefab = item.WorldPrefab.gameObject;
            if (prefab == null)
                return;

            runtimeVisual = Instantiate(prefab, transform, false);
            runtimeVisual.name = $"Installed_{item.ItemId}";
            runtimeVisual.transform.localPosition = installedLocalPosition;
            runtimeVisual.transform.localRotation = Quaternion.Euler(
                installedLocalEulerAngles);
            runtimeVisual.transform.localScale = installedLocalScale;
            SetLayerRecursively(runtimeVisual, gameObject.layer);
            MakeVisualOnly(runtimeVisual);
        }

        public void SetUpgradeModeActive(bool active)
        {
            upgradeModeActive = active;
            RefreshFakeVisual();
        }

        private void ResolveReferences()
        {
            slotId = slotId?.Trim();
            if (string.IsNullOrEmpty(slotId))
                slotId = gameObject.name;

            if (fakeVisual == null)
            {
                Transform candidate = transform.Find("Fake");
                if (candidate != null)
                    fakeVisual = candidate.gameObject;
            }

            if (installedLocalScale == Vector3.zero)
                installedLocalScale = Vector3.one;
        }

        private void ClearRuntimeVisual()
        {
            if (runtimeVisual != null)
            {
                if (Application.isPlaying)
                    Destroy(runtimeVisual);
                else
                    DestroyImmediate(runtimeVisual);
            }
            runtimeVisual = null;
        }

        private void RefreshFakeVisual()
        {
            if (fakeVisual == null)
                return;

            bool showSilhouette = !hasPart && showEmptyFake;
            bool keepSlotHitbox = upgradeModeActive && hasPart;
            bool keepActive = showSilhouette || keepSlotHitbox;
            fakeVisual.SetActive(keepActive);
            if (!keepActive)
                return;

            foreach (Renderer renderer in
                     fakeVisual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = showSilhouette;
            }

            foreach (Collider collider in
                     fakeVisual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
            }
        }

        private static void MakeVisualOnly(GameObject root)
        {
            foreach (MonoBehaviour behaviour in
                     root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null)
                    behaviour.enabled = false;
            }

            foreach (Rigidbody body in
                     root.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private void OnValidate()
        {
            ResolveReferences();
        }
    }
}
