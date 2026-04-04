using System;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Data;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using VRC.SDK3.Persistence;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Events;
using UnityEngine.Pool;
using UnityEditor.Events;
using VRC.Udon;
#endif

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public partial class GlobalAnimatorControlSwitch : UdonSharpBehaviour {
#if COMPILER_UDONSHARP && !UNITY_EDITOR
        [SerializeField, HideInInspector] object option;
#endif
        [SerializeField, HideInInspector] DataDictionary toggle2Group, groups2Id, id2Groups, id2Data;
        DataDictionary stateCache;
        Type toggleType;
        bool initialized, isUpdating;

        void OnEnable() {
            if (initialized) return;
            stateCache = new DataDictionary();
            toggleType = typeof(Toggle);
            var groupKeys = groups2Id.GetKeys();
            for (int i = 0, count = groupKeys.Count; i < count; i++) {
                var entry = groupKeys[i];
                if (!groups2Id.TryGetValue(entry, out var id)) continue;
                if (id2Data.TryGetValue(id, out var tok))
                    id2Data[entry] = tok;
                if (id2Groups.TryGetValue(id, out tok))
                    id2Groups[entry] = tok;
            }
            var toggleKeys = toggle2Group.GetKeys();
            for (int i = 0, count = toggleKeys.Count; i < count; i++) {
                var entry = toggleKeys[i];
                var maybeToggle = entry.Reference;
                if (!Utilities.IsValid(maybeToggle) || maybeToggle.GetType() != toggleType) continue;
                var toggle = (Toggle)maybeToggle;
                stateCache[entry] = toggle.isOn;
            }
            initialized = true;
        }

        public override void OnPlayerRestored(VRCPlayerApi player) {
            if (!player.isLocal) return;
            var dataKeys = id2Data.GetKeys();
            for (int i = 0, count = dataKeys.Count; i < count; i++) {
                var id = dataKeys[i];
                if (id.TokenType != TokenType.String) continue;
                var idStr = id.String;
                if (!id2Data.TryGetValue(id, TokenType.DataDictionary, out var tok) ||
                    !PlayerData.TryGetInt(player, idStr, out var value)) continue;
                var data = tok.DataDictionary;
                UpdateAnimator(data, value);
                UpdateOtherGroups(id, null, value);
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnToggleValueChanged() {
            if (isUpdating) return;
            isUpdating = true;
            SendCustomEventDelayedFrames(nameof(_DeferredProcessUpdatedValue), 0);
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _DeferredProcessUpdatedValue() {
            var allToggles = stateCache.GetKeys();
            for (int i = 0, count = stateCache.Count; i < count; i++) {
                var toggleTok = allToggles[i];
                var toggle = (Toggle)toggleTok.Reference;
                var isOn = toggle.isOn;
                if (!stateCache.TryGetValue(toggleTok, TokenType.Boolean, out var tok) ||
                    tok.Boolean == isOn)
                    continue;
                stateCache[toggleTok] = isOn;
                if (!toggle2Group.TryGetValue(toggleTok, TokenType.Reference, out tok)) continue;
                var group = (ToggleGroup)tok.Reference;
                if (!Utilities.IsValid(group)) continue;
                if (!id2Data.TryGetValue(tok, TokenType.DataList, out var tok2)) continue;
                var data = tok2.DataList;
                if (!toggle2Group.TryGetValue(tok, TokenType.DataList, out tok2)) continue;
                var members = tok2.DataList;
                int selectedIndex = -1;
                for (int j = 0, count2 = members.Count; j < count2; j++) {
                    var memberTok = members[j];
                    if (memberTok.TokenType != TokenType.Reference) continue;
                    var memberToggle = (Toggle)memberTok.Reference;
                    if (!Utilities.IsValid(memberToggle)) continue;
                    var memberIsOn = memberToggle.isOn;
                    stateCache[memberTok] = memberIsOn;
                    if (memberIsOn) selectedIndex = j;
                }
                for (int j = 0, count2 = data.Count; j < count2; j++) {
                    var drivenTok = data[j];
                    if (drivenTok.TokenType != TokenType.DataDictionary) continue;
                    var drivenData = drivenTok.DataDictionary;
                    UpdateAnimator(drivenData, selectedIndex);
                    UpdatePersistency(drivenData, selectedIndex);
                }
                UpdateOtherGroups(tok, group, selectedIndex);
            }
            isUpdating = false;
        }

        void UpdateOtherGroups(DataToken id, ToggleGroup skipGroup, int value) {
            if (!id2Groups.TryGetValue(id, TokenType.DataList, out var tok)) return;
            var otherGroups = tok.DataList;
            for (int i = 0, count = otherGroups.Count; i < count; i++) {
                var groupTok = otherGroups[i];
                if (groupTok.TokenType != TokenType.Reference) continue;
                var group = (ToggleGroup)groupTok.Reference;
                if (!Utilities.IsValid(group) || group == skipGroup) continue;
                if (!toggle2Group.TryGetValue(group, TokenType.DataList, out var tok2)) continue;
                var members = tok2.DataList;
                for (int j = 0, count2 = members.Count; j < count2; j++) {
                    var memberTok = members[j];
                    if (memberTok.TokenType != TokenType.Reference) continue;
                    var memberToggle = (Toggle)memberTok.Reference;
                    if (!Utilities.IsValid(memberToggle)) continue;
                    bool isOn = j == value;
                    memberToggle.SetIsOnWithoutNotify(isOn);
                    stateCache[memberTok] = isOn;
                }
            }
        }

        void UpdateAnimator(DataDictionary drivenData, int value) {
            if (!drivenData.TryGetValue("drivenAnimator", TokenType.Reference, out var tok)) return;
            var drivenAnimator = (Animator)tok.Reference;
            if (!Utilities.IsValid(drivenAnimator)) return;
            if (!drivenData.TryGetValue("drivenParameter", TokenType.String, out tok)) return;
            var drivenParameter = tok.String;
            drivenAnimator.SetInteger(drivenParameter, value);
        }

        void UpdatePersistency(DataDictionary drivenData, int value) {
            if (!drivenData.TryGetValue("persistencyKey", TokenType.String, out var tok)) return;
            var persistencyKey = tok.String;
            if (string.IsNullOrEmpty(persistencyKey)) return;
            PlayerData.SetInt(persistencyKey, value);
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    public partial class GlobalAnimatorControlSwitch : ISingleton<GlobalAnimatorControlSwitch> {
        static readonly FieldInfo backingUdonBehaviourField;
        static readonly MethodInfo sendEventMethod;
        static readonly ConditionalWeakTable<GlobalAnimatorControlSwitch, UnityAction<string>> sendEventActionCache =
            new ConditionalWeakTable<GlobalAnimatorControlSwitch, UnityAction<string>>();
        [SerializeField] GlobalAnimatorControlOption option;

        static GlobalAnimatorControlSwitch() {
            backingUdonBehaviourField = typeof(UdonSharpBehaviour)
                .GetField("_udonSharpBackingUdonBehaviour", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            sendEventMethod = typeof(UdonBehaviour)
                .GetMethod(nameof(UdonBehaviour.SendCustomEvent), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        static UnityAction<string> CreateSendEventAction(GlobalAnimatorControlSwitch instance) {
            var ub = backingUdonBehaviourField.GetValue(instance) as UdonBehaviour;
            return ub == null ? null : Delegate.CreateDelegate(typeof(UnityAction<string>), ub, sendEventMethod, false) as UnityAction<string>;
        }

        public void Merge(GlobalAnimatorControlSwitch[] others) {
            toggle2Group = new DataDictionary();
            groups2Id = new DataDictionary();
            id2Groups = new DataDictionary();
            id2Data = new DataDictionary();
            var mergeState = new MergeState();
            foreach (var toggle in FindObjectsByType<Toggle>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                var parent = toggle.transform;
                bool isEditorOnly = false;
                while (parent != null) {
                    if (parent.CompareTag("EditorOnly")) {
                        isEditorOnly = true;
                        break;
                    }
                    parent = parent.parent;
                }
                if (isEditorOnly) continue;
                var group = toggle.group;
                if (group == null) continue;
                if (!mergeState.groupMembers.TryGetValue(group, out var members))
                    mergeState.groupMembers[group] = members = new List<Toggle>();
                members.Add(toggle);
            }
            foreach (var members in mergeState.groupMembers.Values)
                members.Sort(CompareByHierarchy);
            Merge(this, mergeState);
            foreach (var other in others)
                if (other != this)
                    other.Merge(this, mergeState);
            option = null;
        }

        void Merge(GlobalAnimatorControlSwitch dest, MergeState mergeState) {
            if (option == null || option.parameters == null) return;
            foreach (var opt in option.parameters) {
                var group = opt.toggleGroup;
                if (group == null) continue;
                var members = new DataList();
                dest.toggle2Group[group] = members;
                dest.groups2Id[group] = opt.id;
                DataList list;
                if (dest.id2Groups.TryGetValue(opt.id, TokenType.Reference, out var tok))
                    list = tok.DataList;
                else
                    dest.id2Groups[opt.id] = list = new DataList();
                list.Add(group);
                if (dest.id2Data.TryGetValue(opt.id, TokenType.DataList, out tok))
                    list = tok.DataList;
                else
                    dest.id2Data[opt.id] = list = new DataList();
                var dict = new DataDictionary {
                    ["drivenAnimator"] = opt.drivenAnimator,
                    ["drivenParameter"] = opt.drivenParameter,
                };
                if (!string.IsNullOrEmpty(opt.persistencyKey) && !dest.groups2Id.ContainsKey(opt.persistencyKey)) {
                    dest.groups2Id[opt.persistencyKey] = opt.id;
                    dict["persistencyKey"] = opt.persistencyKey;
                }
                list.Add(dict);
                if (mergeState.groupMembers.TryGetValue(group, out var membersArray))
                    foreach (var toggle in membersArray) {
                        if (toggle == null) continue;
                        dest.toggle2Group[toggle] = group;
                        members.Add(toggle);
                        UnityEventTools.AddStringPersistentListener(
                            toggle.onValueChanged,
                            sendEventActionCache.GetValue(dest, CreateSendEventAction),
                            nameof(_OnToggleValueChanged)
                        );
                    }
            }
        }

        static int CompareByHierarchy(Toggle a, Toggle b) {
            var transformA = a.transform;
            var transformB = b.transform;
            if (transformA == transformB) return 0;
            using (ListPool<Transform>.Get(out var parentsA))
            using (ListPool<Transform>.Get(out var parentsB)) {
                var current = transformA;
                while (current != null) {
                    parentsA.Add(current);
                    current = current.parent;
                }
                current = transformB;
                while (current != null) {
                    parentsB.Add(current);
                    current = current.parent;
                }
                int indexA = parentsA.Count - 1, indexB = parentsB.Count - 1;
                while (indexA >= 0 && indexB >= 0 && parentsA[indexA] == parentsB[indexB]) {
                    indexA--;
                    indexB--;
                }
                if (indexA < 0) return -1;
                if (indexB < 0) return 1;
                return parentsA[indexA].GetSiblingIndex().CompareTo(parentsB[indexB].GetSiblingIndex());
            }
        }

        [Serializable]
        class GlobalAnimatorControlOption {
            public AnimatorControlParameter[] parameters;
        }

        [Serializable]
        struct AnimatorControlParameter {
            public string id;
            public ToggleGroup toggleGroup;
            public Animator drivenAnimator;
            public string drivenParameter;
            public string persistencyKey;
        }

        class MergeState {
            public readonly Dictionary<ToggleGroup, List<Toggle>> groupMembers = new Dictionary<ToggleGroup, List<Toggle>>();
        }
    }
#endif
}