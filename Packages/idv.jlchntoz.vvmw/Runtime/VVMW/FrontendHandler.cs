using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Frontend Handler")]
    [DefaultExecutionOrder(1)]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#playlist-queue-handler")]
    public partial class FrontendHandler : UdonSharpEventSender {
        protected const byte NONE = 0, REPEAT_ONE = 0x1, REPEAT_ALL = 0x2, SHUFFLE = 0x4;
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core"), Locatable, BindUdonSharpEvent, SingletonCoreControl] public Core core;
        [FieldChangeCallback(nameof(Locked))]
        [SerializeField, LocalizedLabel] bool locked = false;
        [SerializeField, LocalizedLabel] bool defaultLoop, defaultShuffle;
        [SerializeField, LocalizedLabel] bool autoPlay = true;
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.autoPlayDelay")] float autoPlayDelay = 0;
        [SerializeField, LocalizedLabel] bool seedRandomBeforeShuffle = true;
        [UdonSynced] byte flags;
        bool synced;
        byte localFlags;
        bool afterFirstRun, isDataArrivedBeforeInit;

        public bool Locked {
            get => locked;
            private set {
                if (value) _Lock();
                else _OnUnlock();
            }
        }


        public int PlayListIndex {
            get {
                if (localPlayListIndex > 0) return localPlayListIndex;
                if (!enableQueueList) return defaultPlayListIndex;
                return 0;
            }
        }
    
        public int PendingCount => localPlayListIndex > 0 ?
            localPlayListOrder != null ? localPlayListOrder.Length : 0 :
            localQueuedUrls != null ? localQueuedUrls.Length : 0;

        public bool RepeatOne {
            get => (localFlags & REPEAT_ONE) == REPEAT_ONE;
            set {
                byte newFlags = localFlags;
                if (value)
                    newFlags = (byte)((localFlags | REPEAT_ONE) & ~REPEAT_ALL & 0xFF);
                else
                    newFlags &= ~REPEAT_ONE & 0xFF;
                core.Loop = value;
                if (newFlags != localFlags) {
                    localFlags = newFlags;
                    if (localPlayListIndex > 0) RefreshPlayListQueue(-1);
                    RequestSync();
                }
                UpdateState();
            }
        }

        public bool RepeatAll {
            get => (localFlags & REPEAT_ALL) == REPEAT_ALL;
            set {
                byte newFlags = localFlags;
                if (value) {
                    newFlags = (byte)((localFlags | REPEAT_ALL) & ~REPEAT_ONE & 0xFF);
                    core.Loop = false;
                } else
                    newFlags &= ~REPEAT_ALL & 0xFF;
                if (newFlags != localFlags) {
                    localFlags = newFlags;
                    if (localPlayListIndex > 0) RefreshPlayListQueue(-1);
                    RequestSync();
                }
                UpdateState();
            }
        }

        public bool Shuffle {
            get => (localFlags & SHUFFLE) == SHUFFLE;
            set {
                var newFlags = localFlags;
                if (value)
                    newFlags |= SHUFFLE;
                else
                    newFlags &= ~SHUFFLE & 0xFF;
                if (newFlags != localFlags) {
                    localFlags = newFlags;
                    if (localPlayListIndex > 0) RefreshPlayListQueue(-1);
                    RequestSync();
                }
                UpdateState();
            }
        }

        public void NoRepeat() {
            byte newFlags = localFlags;
            newFlags &= ~(REPEAT_ONE | REPEAT_ALL) & 0xFF;
            if (newFlags != localFlags) {
                localFlags = newFlags;
                if (localPlayListIndex > 0) RefreshPlayListQueue(-1);
                RequestSync();
            }
            UpdateState();
        }

        void OnEnable() => _Init();

        public void _Init() {
            if (afterFirstRun) return;
            if (!core.afterFirstRun) {
                Debug.LogWarning("[VVMW] It seems FrontendHandler initialized before Core, and this should not happened (Hence the script execution order).\nWaiting for Core to initialize...");
                if (gameObject.activeInHierarchy && enabled)
                    SendCustomEventDelayedFrames(nameof(_Init), 0);
                return;
            }
            synced = core.IsSynced;
            for (int i = 0; i < playListUrlOffsets.Length; i++)
                LoadDynamicPlaylist(i);
            if (!synced || Networking.IsOwner(gameObject)) {
                if (core.Loop) localFlags |= REPEAT_ONE;
                if (defaultPlayListIndex > 0 && defaultPlayListIndex <= playListUrlOffsets.Length && autoPlay)
                    SendCustomEventDelayedSeconds(nameof(_AutoPlay), autoPlayDelay);
                else {
                    RequestSync();
                    UpdateState();
                }
            }
            afterFirstRun = true;
            if (isDataArrivedBeforeInit)
                OnDeserialization();
        }

        public void _AutoPlay() {
            core.Loop = RepeatOne;
            if (defaultLoop) localFlags |= REPEAT_ALL;
            if (defaultShuffle) {
                localFlags |= SHUFFLE;
                SeedRandomBeforeShuffle();
            }
            localPlayListIndex = defaultPlayListIndex;
            int length = (localPlayListIndex == playListUrlOffsets.Length ?
                playListUrls.Length : playListUrlOffsets[localPlayListIndex]
            ) - playListUrlOffsets[localPlayListIndex - 1];
            PlayPlayList(defaultShuffle && length > 0 ? UnityEngine.Random.Range(0, length) : 0);
        }
        
        protected void UpdateState() {
            SendEvent("_OnUIUpdate");
            UpdateAudioLink();
        }

        public void _Play() {
            if (locked) return;
            core.Play();
            SendEvent("_OnPlay");
        }

        public void _Pause() {
            if (locked) return;
            core.Pause();
            SendEvent("_OnPause");
        }

        public void _Stop() {
            if (locked) return;
            if (core.ActivePlayer == 0 || core.State < 3) // Manually trigger UI update
                SendCustomEventDelayedFrames(nameof(_TriggerUIUpdate), 0);
            core.Stop();
            localQueuedUrls = new VRCUrl[0];
            localQueuedQuestUrls = null;
            localQueuedPlayerIndex = new byte[0];
            localPlayListOrder = new ushort[0];
            localQueuedTitles = new string[0];
            localPlayingIndex = 0;
            localPlayListIndex = 0;
            RequestSync();
            SendEvent("_OnStop");
        }

        public void _TriggerUIUpdate() => SendEvent("_OnUIUpdate");

        public void _Skip() {
            if (locked) return;
            if (core.ActivePlayer == 0 || core.State < 3) { // Stop() will not work if there is no active player (nothing is playing)
                if (!Networking.IsOwner(gameObject))
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);
                SendCustomEventDelayedFrames(nameof(_PlayNext), 0);
            } else
                core.Stop();
            SendEvent("_OnSkip");
        }

        public void _LocalSync() {
            core.LocalSync();
            SendEvent("_OnLocalSync");
        }

        public void _GlobalSync() {
            if (locked) return;
            core.GlobalSync();
            SendEvent("_OnGlobalSync");
        }

        public override void OnVideoReady() => UpdateState();
        public override void OnVideoStart() => UpdateState();
        public override void OnVideoPlay() => UpdateState();
        public override void OnVideoPause() => UpdateState();
        public override void OnVideoEnd() {
            UpdateState();
            SendCustomEventDelayedFrames(nameof(_PlayNext), 0);
        }

        public void _OnVideoError() {
            UpdateState();
            // If already gave up, try next one
            if (!core.IsLoading) SendCustomEventDelayedFrames(nameof(_PlayNext), 0);
        }

        public void _OnVideoBeginLoad() => UpdateState();

        public void _OnVolumeChange() => SendEvent("_OnVolumeChange");

        public void _OnSyncOffsetChange() => SendEvent("_OnSyncOffsetChange");

        public void _OnSpeedChange() => SendEvent("_OnSpeedChange");

        public void _OnScreenSharedPropertiesChanged() => SendEvent("_OnScreenSharedPropertiesChanged");

        public override void OnPreSerialization() {
            if (!synced) return;
            queuedUrls = localQueuedUrls == null ? new VRCUrl[0] : localQueuedUrls;
            queuedQuestUrls = localQueuedQuestUrls == null ? new VRCUrl[0] : localQueuedQuestUrls;
            queuedPlayerIndex = localQueuedPlayerIndex == null ? new byte[0] : localQueuedPlayerIndex;
            playListOrder = localPlayListOrder == null ? new ushort[0] : localPlayListOrder;
            queuedTitles = localQueuedTitles == null ? "" : string.Join("\u2029", localQueuedTitles);
            historyUrls = localHistoryUrls == null ? new VRCUrl[0] : localHistoryUrls;
            historyQuestUrls = localHistoryQuestUrls == null ? new VRCUrl[0] : localHistoryQuestUrls;
            historyPlayerIndex = localHistoryPlayerIndex == null ? new byte[0] : localHistoryPlayerIndex;
            historyTitles = localHistoryTitles == null ? "" : string.Join("\u2029", localHistoryTitles);
            flags = localFlags;
            playListIndex = (ushort)localPlayListIndex;
            playingIndex = localPlayingIndex;
            bool shouldLoop = RepeatOne;
            if (core.Loop != shouldLoop) {
                core.Loop = shouldLoop;
                UpdateAudioLink();
            }
        }

        public override void OnDeserialization() {
            if (!afterFirstRun) {
                isDataArrivedBeforeInit = true;
                return;
            }
            isDataArrivedBeforeInit = false;
            if (!synced) return;
            localQueuedUrls = queuedUrls;
            localQueuedQuestUrls = IsArrayNullOrEmpty(queuedQuestUrls) ? null : queuedQuestUrls;
            localQueuedPlayerIndex = queuedPlayerIndex;
            localPlayListOrder = playListOrder;
            localQueuedTitles = string.IsNullOrEmpty(queuedTitles) && IsArrayNullOrEmpty(queuedUrls) ?
                new string[0] : queuedTitles.Split('\u2029');
            localHistoryUrls = historyUrls;
            localHistoryQuestUrls = IsArrayNullOrEmpty(historyQuestUrls) ? null : historyQuestUrls;
            localHistoryPlayerIndex = historyPlayerIndex;
            localHistoryTitles = string.IsNullOrEmpty(historyTitles) && IsArrayNullOrEmpty(historyUrls) ?
                new string[0] : historyTitles.Split('\u2029');
            localFlags = flags;
            if (playListIndex > 0) {
                if (localPlayListIndex != playListIndex || localPlayingIndex != playingIndex)
                    core.SetTitle(playListEntryTitles[playingIndex], playListTitles[playListIndex - 1]);
            } else core._ResetTitle();
            localPlayListIndex = playListIndex;
            localPlayingIndex = playingIndex;
            core.Loop = RepeatOne;
            UpdateState();
        }

        public void _PlayNext() {
            if (synced && !Networking.IsOwner(gameObject)) return;
            PlayAt(localPlayListIndex, -1, false);
        }

        [Obsolete("Use PlayAt instead.")]
        public void _PlayAt(int playListIndex, int entryIndex, bool deleteOnly) =>
            PlayAt(playListIndex, entryIndex, deleteOnly);

        public void PlayAt(int playListIndex, int entryIndex, bool deleteOnly) {
            int actualPlayListIndex = playListIndex;
            if (actualPlayListIndex < 0) actualPlayListIndex = 0;
            if (actualPlayListIndex != localPlayListIndex) {
                localQueuedUrls = null;
                localQueuedQuestUrls = null;
                localQueuedPlayerIndex = null;
                localQueuedTitles = null;
                localPlayListOrder = null;
                localPlayListIndex = actualPlayListIndex;
            }
            if (playListIndex > 0)
                PlayPlayList(entryIndex);
            else if (playListIndex == -1)
                PlayHistory(entryIndex);
            else
                PlayQueueList(entryIndex, deleteOnly);
        }

        // API used with UdonAuth or other capability systems
        public void _OnUnlock() {
            if (!locked) return;
            locked = false;
            UpdateState();
        }

        public void _Lock() {
            if (locked) return;
            locked = true;
            UpdateState();
        }

        void SeedRandomBeforeShuffle() {
            if (!seedRandomBeforeShuffle) return;
            UnityEngine.Random.InitState((int)(DateTime.Now.Ticks & int.MaxValue));
            seedRandomBeforeShuffle = false;
        }

        bool RequestSync() {
            if (!synced) return false;
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();
            return true;
        }

        public void _OnTitleData() => UpdateState();

        bool IsArrayNullOrEmpty(Array array) => array == null || array.Length == 0;
    }
}