using System;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// An entry list with all UI elements pooled.
    /// This is the component for handling the scroll list.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(ScrollRect))]
    [BindEvent(typeof(ScrollRect), nameof(ScrollRect.onValueChanged), nameof(_OnScroll))]
    [AddComponentMenu("VizVid/Components/Pooled Scroll View")]
    [DefaultExecutionOrder(2)]
    public partial class PooledScrollView : UdonSharpEventSender {
        [SerializeField, HideInInspector, Resolve(".")]
        ScrollRect scrollRect;
        [FieldChangeCallback(nameof(EventPrefix))]
        [SerializeField] string eventPrefix = "_On";
        [SerializeField] GameObject template;
        [FieldChangeCallback(nameof(SelectedIndex))]
        [SerializeField] int selectedIndex = -1;
        [SerializeField] bool inverseOrder;
        [NonSerialized] public int lastClickedIndex;
        [NonSerialized] public int lastDeletedIndex;
        [NonSerialized] public int lastInteractIndex;
        ListEntry[] entries;
        [FieldChangeCallback(nameof(EntryNames))]
        string[] entryNames;
        bool hasInit;
        [FieldChangeCallback(nameof(CanDelete))]
        bool canDelete = true;
        [FieldChangeCallback(nameof(CanInteract))]
        bool canInteract = true;
        bool isUpwards;
        public bool autoSelect = true;
        int offset, count;
        [SerializeField, HideInInspector, Resolve(nameof(scrollRect) + "." + nameof(ScrollRect.viewport))]
        RectTransform viewportRect;
        [SerializeField, HideInInspector, Resolve(nameof(scrollRect) + "." + nameof(ScrollRect.content))]
        RectTransform contentRect;
        [SerializeField, HideInInspector, Resolve(nameof(template))]
        RectTransform templateRect;
        Vector2 prevAnchorPosition;
        float entriesPerViewport;
        string entryClickEventName = "_OnEntryClick";
        string entryDeleteEventName = "_OnEntryDelete";
        string scrollEventName = "_OnScroll";

        public string EventPrefix {
            get => eventPrefix;
            set {
                eventPrefix = value;
                entryClickEventName = eventPrefix + "EntryClick";
                entryDeleteEventName = eventPrefix + "EntryDelete";
                scrollEventName = eventPrefix + "Scroll";
            }
        }

        public int SelectedIndex {
            get => selectedIndex;
            set {
                selectedIndex = value;
                if (hasInit && gameObject.activeInHierarchy) {
                    UpdateEntryState();
                    ScrollToSelected();
                }
            }
        }

        public string[] EntryNames {
            get {
                if (!Utilities.IsValid(entryNames)) return null;
                if (offset == 0 && count == entryNames.Length)
                    return entryNames;
                var result = new string[count];
                Array.Copy(entryNames, offset, result, 0, count);
                return result;
            }
            set {
                entryNames = value;
                offset = 0;
                count = Utilities.IsValid(entryNames) ? entryNames.Length : 0;
                if (hasInit && gameObject.activeInHierarchy)
                    UpdateEntryState();
            }
        }

        public bool CanDelete {
            get => canDelete;
            set {
                canDelete = value;
                if (hasInit && gameObject.activeInHierarchy)
                    foreach (var entry in entries)
                        entry.HasDelete = value;
            }
        }

        public bool CanInteract {
            get => canInteract;
            set {
                canInteract = value;
                if (hasInit && gameObject.activeInHierarchy)
                    foreach (var entry in entries)
                        entry.Unlocked = value;
            }
        }

        public float ScrollPosition {
            get {
                if (!Utilities.IsValid(scrollRect)) return 0F;
                return scrollRect.normalizedPosition.y;
            }
            set {
                if (!Utilities.IsValid(scrollRect)) return;
                var normalizedPosition = scrollRect.normalizedPosition;
                normalizedPosition.y = value;
                scrollRect.normalizedPosition = normalizedPosition;
            }
        }

        void OnEnable() {
            if (!hasInit) {
                if (!Utilities.IsValid(template)) {
                    var listEntry = GetComponentInChildren<ListEntry>(true);
                    if (!Utilities.IsValid(listEntry)) {
                        Debug.LogError("No template found in PooledScrollView", this);
                        return;
                    }
                    template = listEntry.gameObject;
                }
                isUpwards = contentRect.pivot.y < 0.5F;
                var transformsAfterEntry = new Transform[contentRect.childCount];
                int count = 0;
                for (int i = contentRect.childCount - 1; i >= 0; i--) {
                    var child = contentRect.GetChild(i);
                    if (child == template.transform) break;
                    transformsAfterEntry[count++] = child;
                }
                var templateHeight = templateRect.rect.height;
                var viewportHeight = viewportRect.rect.height;
                entriesPerViewport = viewportHeight / templateHeight;
                var entryCount = Mathf.CeilToInt(entriesPerViewport) + 1;
                entries = new ListEntry[entryCount];
                var templatePos = templateRect.localPosition;
                var templateRot = templateRect.localRotation;
                for (var i = 0; i < entryCount; i++) {
                    var instance = Instantiate(template);
                    var instTrans = instance.transform;
                    instTrans.SetParent(contentRect, false);
                    instTrans.SetLocalPositionAndRotation(templatePos, templateRot);
                    var entry = instance.GetComponent<ListEntry>();
                    entry.asPooledEntry = true;
                    entry.indexAsUserData = true;
                    entry.callbackTarget = this;
                    entry.isUpwards = isUpwards;
                    entry.inverseOrder = inverseOrder;
                    entry.callbackEventName = nameof(_OnEntryClick);
                    entry.deleteEventName = nameof(_OnEntryDelete);
                    entry.callbackVariableName = nameof(lastInteractIndex);
                    entry.entryOffset = i;
                    entry.HasDelete = canDelete;
                    entry.Unlocked = canInteract;
                    entry.spawnedEntryCount = entryCount;
                    entry._OnParentScroll();
                    entries[i] = entry;
                }
                for (int i = 0; i < count; i++)
                    transformsAfterEntry[i].SetAsLastSibling();
                template.SetActive(false);
                EventPrefix = eventPrefix;
                hasInit = true;
                if (Utilities.IsValid(entryNames)) UpdateEntryState();
            } else {
                if (Utilities.IsValid(entryNames)) UpdateEntryState();
                foreach (var entry in entries) {
                    entry.HasDelete = canDelete;
                    entry.Unlocked = canInteract;
                }
            }
            ScrollToSelected();
        }

        void UpdateEntryState() {
            var size = contentRect.sizeDelta;
            size.y = count * templateRect.rect.height;
            contentRect.sizeDelta = size;
            for (var i = 0; i < entries.Length; i++) {
                var entry = entries[i];
                entry.pooledEntryNames = entryNames;
                entry.selectedEntryIndex = selectedIndex;
                entry.pooledEntryOffset = offset;
                entry.pooledEntryCount = count;
                entry._UpdatePositionAndContent();
            }
        }

        public void SetEntries(string[] entries, int offset, int count) {
            entryNames = entries;
            this.offset = offset;
            this.count = count;
            if (hasInit && gameObject.activeInHierarchy)
                UpdateEntryState();
        }

        public void SetIndexWithoutScroll(int index) {
            selectedIndex = index;
            if (hasInit && gameObject.activeInHierarchy)
                UpdateEntryState();
        }

        public void ScrollToSelected() => ScrollTo(selectedIndex);

        public void ScrollTo(int index) {
            if (!hasInit) return;
            var normalizedPosition = scrollRect.normalizedPosition;
            float pos = count > entriesPerViewport ? Mathf.Clamp01(index / (count - entriesPerViewport)) : 0;
            if (isUpwards == inverseOrder) pos = 1 - pos;
            normalizedPosition.y = pos;
            scrollRect.normalizedPosition = normalizedPosition;
        }

        public void _OnEntryClick() {
            lastClickedIndex = lastInteractIndex;
            if (autoSelect) SetIndexWithoutScroll(lastClickedIndex);
            SendEvent(entryClickEventName);
        }

        public void _OnEntryDelete() {
            lastDeletedIndex = lastInteractIndex;
            SendEvent(entryDeleteEventName);
        }

        public void _OnScroll() {
            if (hasInit) {
                // It jitters in some cases, so we need to filter it.
                var newAnchorPosition = contentRect.anchoredPosition;
                if (Vector2.Distance(prevAnchorPosition, newAnchorPosition) < 0.005F) return;
                prevAnchorPosition = newAnchorPosition;
                foreach (var entry in entries)
                    entry._OnParentScroll();
            }
            SendEvent(scrollEventName);
        }
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    public partial class PooledScrollView : ISelfPreProcess {
        public int Priority => 0;

        public void PreProcess() {
            var contentRT = GetComponent<ScrollRect>().content;
            var templateRT = template.GetComponent<RectTransform>();
            bool isContentAscending = contentRT.pivot.y < 0.5F != inverseOrder;
            templateRT.pivot = new Vector2(templateRT.pivot.x, isContentAscending ? 0F : 1F);
        }
    }
#endif
}