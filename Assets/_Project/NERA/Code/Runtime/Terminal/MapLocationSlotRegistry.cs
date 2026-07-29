using System.Collections.Generic;
using UnityEngine;

namespace NERA.Terminal
{
    [DisallowMultipleComponent]
    [AddComponentMenu("NERA/Terminal Map/Slot Registry")]
    public sealed class MapLocationSlotRegistry : MonoBehaviour
    {
        private readonly Dictionary<MapSlotData, MapLocationSlot> slots =
            new Dictionary<MapSlotData, MapLocationSlot>();

        public int Count => slots.Count;

        private void Awake()
        {
            Rebuild();
        }

        private void OnTransformChildrenChanged()
        {
            Rebuild();
        }

        public void Rebuild()
        {
            slots.Clear();

            MapLocationSlot[] authoredSlots =
                GetComponentsInChildren<MapLocationSlot>(true);
            foreach (MapLocationSlot authoredSlot in authoredSlots)
            {
                if (authoredSlot == null || authoredSlot.Slot == null)
                    continue;

                if (!slots.TryAdd(authoredSlot.Slot, authoredSlot))
                {
                    Debug.LogError(
                        $"Duplicate terminal map slot " +
                        $"'{authoredSlot.Slot.DisplayName}' under " +
                        $"'{name}'.",
                        authoredSlot);
                }
            }
        }

        public bool TryGetSlot(
            MapSlotData slotData,
            out MapLocationSlot authoredSlot)
        {
            if (slotData == null)
            {
                authoredSlot = null;
                return false;
            }

            return slots.TryGetValue(slotData, out authoredSlot);
        }

        public bool TryGetSlot(
            Transform target,
            out MapLocationSlot authoredSlot)
        {
            authoredSlot =
                target != null ? target.GetComponentInParent<MapLocationSlot>() : null;
            return authoredSlot != null &&
                   authoredSlot.transform.IsChildOf(transform);
        }
    }
}
