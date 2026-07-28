using System;
using System.Collections.Generic;
using NERA.Items;
using NERA.Library;
using NERA.Research;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Terminal
{
    public sealed class TerminalLibraryScreenController : MonoBehaviour
    {
        private enum ScreenCategory
        {
            Anomaly,
            Record,
            Equipment,
            Details
        }

        private sealed class DisplayEntry
        {
            public string Name;
            public string Description;
            public Sprite Image;
        }

        private sealed class SlotView
        {
            public Button Button;
            public TMP_Text Label;
            public DisplayEntry Entry;
        }

        private readonly Dictionary<ScreenCategory, GameObject> roots =
            new Dictionary<ScreenCategory, GameObject>();
        private readonly Dictionary<ScreenCategory, List<SlotView>> slots =
            new Dictionary<ScreenCategory, List<SlotView>>();
        private TMP_Text nameText;
        private TMP_Text descriptionText;
        private Image infoImage;
        private ScreenCategory activeCategory = ScreenCategory.Anomaly;
        private bool initialized;

        public void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            CacheHierarchy();
            BindTabs();
            BuildSlots();
            ShowCategory(ScreenCategory.Anomaly);
        }

        public void SetScreenActive(bool active)
        {
            if (active)
            {
                RefreshEntries();
                ShowCategory(activeCategory);
            }
        }

        private void CacheHierarchy()
        {
            Transform infoRoot = TerminalUIUtility.Find(
                transform, "background_Screen_Lybrary_Info");
            nameText = TerminalUIUtility.FindComponent<TMP_Text>(
                infoRoot, "Text_Name");
            descriptionText = TerminalUIUtility.FindComponent<TMP_Text>(
                infoRoot, "Text_Description");
            infoImage = TerminalUIUtility.FindComponent<Image>(
                infoRoot, "Image_info");

            roots[ScreenCategory.Anomaly] =
                TerminalUIUtility.Find(transform, "AnomalySlot")?.gameObject;
            roots[ScreenCategory.Record] =
                TerminalUIUtility.Find(transform, "RecordSlot")?.gameObject;
            roots[ScreenCategory.Equipment] =
                TerminalUIUtility.Find(transform, "EquipmentSlot")?.gameObject;
            roots[ScreenCategory.Details] =
                TerminalUIUtility.Find(transform, "DetailsSlot")?.gameObject;
        }

        private void BindTabs()
        {
            TerminalUIUtility.FindComponent<Button>(
                transform, "AnomalyButton")?.onClick.AddListener(
                () => ShowCategory(ScreenCategory.Anomaly));
            TerminalUIUtility.FindComponent<Button>(
                transform, "RecordButton")?.onClick.AddListener(
                () => ShowCategory(ScreenCategory.Record));
            TerminalUIUtility.FindComponent<Button>(
                transform, "EquipmentButton")?.onClick.AddListener(
                () => ShowCategory(ScreenCategory.Equipment));
            TerminalUIUtility.FindComponent<Button>(
                transform, "DetailsButton")?.onClick.AddListener(
                () => ShowCategory(ScreenCategory.Details));
        }

        private void BuildSlots()
        {
            foreach (KeyValuePair<ScreenCategory, GameObject> pair in roots)
            {
                List<SlotView> categorySlots = new List<SlotView>();
                if (pair.Value != null)
                {
                    List<Transform> authored = new List<Transform>();
                    for (int i = 0; i < pair.Value.transform.childCount; i++)
                    {
                        Transform child = pair.Value.transform.GetChild(i);
                        if (child.name.StartsWith(
                                "background_Slot_",
                                StringComparison.Ordinal))
                        {
                            authored.Add(child);
                        }
                    }

                    authored.Sort((left, right) =>
                        string.CompareOrdinal(left.name, right.name));
                    foreach (Transform slotRoot in authored)
                    {
                        Button button = TerminalUIUtility.EnsureButton(slotRoot);
                        SlotView view = new SlotView
                        {
                            Button = button,
                            Label = TerminalUIUtility.FindComponent<TMP_Text>(
                                slotRoot, "Text_Info")
                        };
                        button?.onClick.AddListener(() => SelectEntry(view));
                        categorySlots.Add(view);
                    }
                }

                slots[pair.Key] = categorySlots;
            }
        }

        private void ShowCategory(ScreenCategory category)
        {
            activeCategory = category;
            foreach (KeyValuePair<ScreenCategory, GameObject> pair in roots)
            {
                if (pair.Value != null)
                    pair.Value.SetActive(pair.Key == category);
            }

            RefreshEntries();
            ClearInfo();
        }

        private void RefreshEntries()
        {
            List<DisplayEntry> entries = BuildEntries(activeCategory);
            if (!slots.TryGetValue(
                    activeCategory,
                    out List<SlotView> categorySlots))
            {
                return;
            }

            for (int i = 0; i < categorySlots.Count; i++)
            {
                SlotView slot = categorySlots[i];
                slot.Entry = i < entries.Count ? entries[i] : null;
                TerminalUIUtility.SetText(
                    slot.Label,
                    slot.Entry?.Name ?? string.Empty);
                if (slot.Button != null)
                    slot.Button.interactable = slot.Entry != null;
            }
        }

        private static List<DisplayEntry> BuildEntries(ScreenCategory category)
        {
            List<DisplayEntry> result = new List<DisplayEntry>();
            ItemCatalogData catalog =
                Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            LibraryController library = LibraryController.Instance;
            ResearchController research = ResearchController.Instance;

            if (catalog != null)
            {
                foreach (ItemData item in catalog.Items)
                {
                    if (item == null ||
                        !MatchesCategory(item.ItemType, category))
                    {
                        continue;
                    }

                    bool unlocked =
                        library?.IsKnownItem(item) == true ||
                        research?.IsAnalyzed(item) == true;
                    if (!unlocked)
                        continue;

                    result.Add(new DisplayEntry
                    {
                        Name = item.DisplayName,
                        Description = item.Description,
                        Image = item.Icon
                    });
                }
            }

            if (category == ScreenCategory.Record && library != null)
            {
                foreach (LibraryEntryData entry in library.Entries)
                {
                    if (entry != null &&
                        entry.Category == LibraryCategory.Records &&
                        library.IsUnlocked(entry))
                    {
                        result.Add(new DisplayEntry
                        {
                            Name = entry.Title,
                            Description = entry.Description,
                            Image = entry.Illustration
                        });
                    }
                }
            }

            return result;
        }

        private static bool MatchesCategory(
            ItemType itemType,
            ScreenCategory category)
        {
            return category switch
            {
                ScreenCategory.Anomaly => itemType == ItemType.Anomaly,
                ScreenCategory.Record => itemType == ItemType.Record,
                ScreenCategory.Equipment => itemType == ItemType.Equipment,
                _ => itemType == ItemType.EngineeringPart ||
                     itemType == ItemType.Artifact ||
                     itemType == ItemType.Consumable ||
                     itemType == ItemType.KeyItem
            };
        }

        private void SelectEntry(SlotView slot)
        {
            if (slot?.Entry == null)
                return;

            TerminalUIUtility.SetText(nameText, slot.Entry.Name);
            TerminalUIUtility.SetText(descriptionText, slot.Entry.Description);
            if (infoImage != null)
            {
                infoImage.sprite = slot.Entry.Image;
                infoImage.enabled = slot.Entry.Image != null;
            }
        }

        private void ClearInfo()
        {
            TerminalUIUtility.SetText(nameText, string.Empty);
            TerminalUIUtility.SetText(descriptionText, string.Empty);
            if (infoImage != null)
                infoImage.enabled = false;
        }
    }
}
