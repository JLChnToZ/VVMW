using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// The playlist, queue list and history list handler for VizVid.
    /// </summary>
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

        /// <summary>
        /// Whether the frontend is locked.
        /// </summary>
        /// <remarks>
        /// This is meant to integrate with external permission systems.
        /// </remarks>
        public bool Locked {
            get => locked;
            private set {
                if (value) _Lock();
                else _OnUnlock();
            }
        }

        /// <summary>
        /// The playlist index (1-based) of the current playing item.
        /// If the value is 0, it means it is not playing from a playlist.
        /// </summary>
        public int PlayListIndex {
            get {
                if (localPlayListIndex > 0) return localPlayListIndex;
                if (!enableQueueList) return defaultPlayListIndex;
                return 0;
            }
        }
    
        /// <summary>
        /// How masny items left on the queue list or playlist.
        /// </summary>
        public int PendingCount => localPlayListIndex > 0 ?
            Utilities.IsValid(localPlayListOrder) ? localPlayListOrder.Length : 0 :
            Utilities.IsValid(localQueuedUrls) ? localQueuedUrls.Length : 0;

        /// <summary>
        /// Should the frontend repeats the current item.
        /// </summary>
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

        /// <summary>
        /// Should the frontend repeats all items.
        /// </summary>
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

        /// <summary>
        /// Should the frontend shuffles the playlist.
        /// </summary>
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

        /// <summary>
        /// Disables all repeat modes.
        /// </summary>
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

        /// <summary>
        /// Play or resume the current item.
        /// </summary>
        public void _Play() {
            if (locked) return;
            core.Play();
            SendEvent("_OnPlay");
        }

        /// <summary>
        /// Pause the current item.
        /// </summary>
        public void _Pause() {
            if (locked) return;
            core.Pause();
            SendEvent("_OnPause");
        }

        /// <summary>
        /// Stops current item and clears the queue list.
        /// </summary>
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

        /// <summary>
        /// Skips current item and play the next one.
        /// </summary>
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

        /// <inheritdoc cref="Core.LocalSync" />
        public void _LocalSync() {
            core.LocalSync();
            SendEvent("_OnLocalSync");
        }

        /// <inheritdoc cref="Core.GlobalSync" />
        public void _GlobalSync() {
            if (locked) return;
            core.GlobalSync();
            SendEvent("_OnGlobalSync");
        }

        /// <inheritdoc cref="Core.OnVideoReady" />
        public override void OnVideoReady() => UpdateState();
        /// <inheritdoc cref="Core.OnVideoStart" />
        public override void OnVideoStart() => UpdateState();
        /// <inheritdoc cref="Core.OnVideoPlay" />
        public override void OnVideoPlay() => UpdateState();
        /// <inheritdoc cref="Core.OnVideoPause" />
        public override void OnVideoPause() => UpdateState();
        /// <inheritdoc cref="Core.OnVideoEnd" />
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

        /// <inheritdoc cref="Core.OnPreSerialization" />
        public override void OnPreSerialization() {
            if (!synced) return;
            queuedUrls = !Utilities.IsValid(localQueuedUrls) ? new VRCUrl[0] : localQueuedUrls;
            queuedQuestUrls = !Utilities.IsValid(localQueuedQuestUrls) ? new VRCUrl[0] : localQueuedQuestUrls;
            queuedPlayerIndex = !Utilities.IsValid(localQueuedPlayerIndex) ? new byte[0] : localQueuedPlayerIndex;
            playListOrder = !Utilities.IsValid(localPlayListOrder) ? new ushort[0] : localPlayListOrder;
            queuedTitles = !Utilities.IsValid(localQueuedTitles) ? "" : string.Join("\u2029", localQueuedTitles);
            historyUrls = !Utilities.IsValid(localHistoryUrls) ? new VRCUrl[0] : localHistoryUrls;
            historyQuestUrls = !Utilities.IsValid(localHistoryQuestUrls) ? new VRCUrl[0] : localHistoryQuestUrls;
            historyPlayerIndex = !Utilities.IsValid(localHistoryPlayerIndex) ? new byte[0] : localHistoryPlayerIndex;
            historyTitles = !Utilities.IsValid(localHistoryTitles) ? "" : string.Join("\u2029", localHistoryTitles);
            flags = localFlags;
            playListIndex = (ushort)localPlayListIndex;
            playingIndex = localPlayingIndex;
            bool shouldLoop = RepeatOne;
            if (core.Loop != shouldLoop) {
                core.Loop = shouldLoop;
                UpdateAudioLink();
            }
        }

        /// <inheritdoc cref="Core.OnDeserialization" />
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

        /// <summary>
        /// Play the next item in the queue list or playlist.
        /// </summary>
        public void _PlayNext() {
            if (synced && !Networking.IsOwner(gameObject)) return;
            PlayAt(localPlayListIndex, -1, false);
        }

        /// <inheritdoc cref="PlayAt(int, int, bool)" />
        [Obsolete("Use PlayAt instead.")]
        public void _PlayAt(int playListIndex, int entryIndex, bool deleteOnly) =>
            PlayAt(playListIndex, entryIndex, deleteOnly);

        /// <summary>
        /// Play (and/or delete) the item at the specified index of the specified list.
        /// </summary>
        /// <param name="playListIndex">
        /// The index of the playlist (1-based).
        /// 0 for queue list, -1 for history list
        /// </param>
        /// <param name="entryIndex">
        /// The index of the entry in the playlist or queue list.
        /// -1 to play the next item or random if shuffle is enabled.
        /// </param>
        /// <param name="deleteOnly">
        /// Instead of playing, just delete the item.
        /// Only works with queue list.
        /// </param>
        /// <remarks>
        /// If requested entry is at the queue list, it will be removed.
        /// </remarks>
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

        bool IsArrayNullOrEmpty(Array array) => !Utilities.IsValid(array) || array.Length == 0;
    }
}