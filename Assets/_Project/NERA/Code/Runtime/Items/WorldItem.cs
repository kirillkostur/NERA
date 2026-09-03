using NERA.Interaction;
using NERA.Inventory;
using NERA.Quests;
using NERA.Save;
using UnityEngine;

namespace NERA.Items
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WorldItem : BaseInteractable
    {
        [Header("Item")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private bool destroyAfterPickup = true;
        [Tooltip("Stable ID required for authored items that track world state.")]
        [SerializeField] private string persistentId;
        [SerializeField] private bool trackWorldState = true;

        [Header("Drop Physics")]
        [Tooltip("Minimum upward contact normal that immediately locks a dropped item in place.")]
        [SerializeField, Range(0f, 1f)] private float minimumSupportNormalY = 0.45f;

        private ItemInstance itemInstance;
        private string runtimePersistentKey;
        private string scenePersistentKey;
        private Rigidbody itemBody;
        private bool dropPhysicsActive;

        public ItemData ItemData => itemData;
        public ItemInstance ItemInstance => itemInstance;
        public string AuthoredPersistentId => persistentId?.Trim();
        public bool TracksWorldState => trackWorldState;
        public string PersistentKey => GetPersistentKey();

        private void Awake()
        {
            itemBody = GetComponent<Rigidbody>();
            scenePersistentKey = PersistentSceneIdentity.CreateKey(
                transform,
                persistentId);
        }

        public void ActivateDropPhysics()
        {
            if (itemBody == null)
                itemBody = GetComponent<Rigidbody>();
            if (itemBody == null)
                return;

            dropPhysicsActive = true;
            itemBody.isKinematic = false;
            itemBody.useGravity = true;
            itemBody.WakeUp();
        }

        public void Initialize(ItemData item)
        {
            Initialize(ItemInstance.Create(item));
        }

        public void Initialize(ItemInstance instance)
        {
            itemInstance = instance;
            itemData = instance?.ItemData;
            destroyAfterPickup = true;
            trackWorldState = false;
            runtimePersistentKey = string.Empty;
            SetActionText("Pick Up");
            SetAvailable(itemData != null, itemData == null ? "Item data missing" : string.Empty);
            RefreshAttachmentVisual();
        }

        public void SetPersistentWorldId(string persistentKey)
        {
            runtimePersistentKey =
                PersistentSceneIdentity.Normalize(persistentKey);
            trackWorldState = !string.IsNullOrEmpty(runtimePersistentKey);
        }

        private void Start()
        {
            RefreshAttachmentVisual();
            if (!trackWorldState)
                return;

            WorldStateController state = WorldStateController.Instance;
            if (state != null && state.IsConsumed(GetPersistentKey()))
                Destroy(gameObject);
        }

        private void Reset()
        {
            SetActionText("Pick Up");

            itemBody = GetComponent<Rigidbody>();
            DisableDropPhysics();
        }

        private void OnValidate()
        {
            persistentId = persistentId?.Trim();
        }

        private void OnCollisionEnter(Collision collision)
        {
            FreezeOnSupportedSurface(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            FreezeOnSupportedSurface(collision);
        }

        private void FreezeOnSupportedSurface(Collision collision)
        {
            if (!dropPhysicsActive ||
                itemBody == null ||
                itemBody.isKinematic ||
                collision == null)
            {
                return;
            }

            for (int i = 0; i < collision.contactCount; i++)
            {
                if (Vector3.Dot(
                        collision.GetContact(i).normal,
                        Vector3.up) < minimumSupportNormalY)
                {
                    continue;
                }

                DisableDropPhysics();
                return;
            }
        }

        private void DisableDropPhysics()
        {
            dropPhysicsActive = false;
            if (itemBody == null)
                return;

            if (!itemBody.isKinematic)
            {
                itemBody.linearVelocity = Vector3.zero;
                itemBody.angularVelocity = Vector3.zero;
            }

            itemBody.useGravity = false;
            itemBody.isKinematic = true;
            itemBody.Sleep();
        }

        public override InteractionPrompt GetPrompt()
        {
            if (itemData == null)
            {
                return new InteractionPrompt(
                    "Pick Up",
                    InteractionMode.Press,
                    0f,
                    false,
                    "Item data missing"
                );
            }

            return base.GetPrompt();
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            if (itemData == null || interactor == null)
                return;

            PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>();

            if (inventory == null)
            {
                Debug.LogWarning(
                    $"{name}: PlayerInventory was not found on the interactor.",
                    this
                );
                return;
            }

            ItemInstance instance = itemInstance ?? ItemInstance.Create(itemData);
            if (!inventory.AddItem(instance))
                return;

            QuestController.Instance?.Report(
                QuestSignalType.ItemCollected,
                itemData.ItemId,
                itemData.DisplayName);

            if (trackWorldState)
            {
                WorldStateController.Instance?.MarkConsumed(
                    GetPersistentKey());
            }

            base.CompleteInteraction(interactor);
            SetAvailable(false, "Picked Up");

            if (destroyAfterPickup)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }

        private string GetPersistentKey()
        {
            if (!string.IsNullOrEmpty(runtimePersistentKey))
                return runtimePersistentKey;

            if (string.IsNullOrEmpty(scenePersistentKey))
            {
                scenePersistentKey = PersistentSceneIdentity.CreateKey(
                    transform,
                    persistentId);
            }

            return scenePersistentKey;
        }

        private void RefreshAttachmentVisual()
        {
            AnomalyAttachmentVisual visual =
                GetComponent<AnomalyAttachmentVisual>();
            if (visual != null)
                visual.Bind(itemInstance ?? ItemInstance.Create(itemData));
        }
    }
}
