using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DisallowMultipleComponent]
    public class FrontendHandler : UdonSharpEventSender {
        protected const byte NONE = 0, REPEAT_ONE = 0x1, REPEAT_ALL = 0x2, SHUFFLE = 0x4;
        [SerializeField, Locatable] public Core core;
        [Tooltip("If enabled, while user want to play a video and it is playing other video, the video will be queued. Recommend as this is more polite to everyone.")]
        [SerializeField] bool enableQueueList = true;
        [Tooltip("Locks the player frontend by default, this option must be used with other scripts to control the player.")]
        [SerializeField] bool locked = false;
        [SerializeField] bool defaultLoop, defaultShuffle;
        [SerializeField] string[] playListTitles;
        [SerializeField] int[] playListUrlOffsets;
        [SerializeField] VRCUrl[] playListUrls, playListUrlsQuest;
        [SerializeField] string[] playListEntryTitles;
        [SerializeField] byte[] playListPlayerIndex;
        [UdonSynced] VRCUrl[] queuedUrls;
        [UdonSynced] byte[] queuedPlayerIndex;
        [UdonSynced] byte flags;
        [UdonSynced] ushort[] playListOrder;
        [UdonSynced] ushort playingIndex;
        [UdonSynced] ushort playListIndex;
        bool synced;
        ushort[] localPlayListOrder;
        VRCUrl[] localQueuedUrls;
        byte[] localQueuedPlayerIndex;
        byte localFlags;
        [SerializeField] int localPlayListIndex;
        ushort localPlayingIndex;

        public VRCUrl[] QueueUrls {
            get {
                if (localQueuedUrls == null) localQueuedUrls = new VRCUrl[0];
                return localQueuedUrls;
            }
        }

        public byte[] QueuePlayerIndex {
            get {
                if (localQueuedPlayerIndex == null) localQueuedPlayerIndex = new byte[0];
                return localQueuedPlayerIndex;
            }
        }

        public bool Locked => locked;

        public bool HasQueueList => enableQueueList;

        public int PlayListIndex => localPlayListIndex;
    
        public string[] PlayListTitles => playListTitles;
    
        public int[] PlayListUrlOffsets => playListUrlOffsets;
    
        public VRCUrl[] PlayListUrls => playListUrls;

        public VRCUrl[] PlayListUrlsQuest => playListUrlsQuest;
    
        public string[] PlayListEntryTitles => playListEntryTitles;
    
        public byte[] PlayListPlayerIndex => playListPlayerIndex;

        public int CurrentPlayingIndex => localPlayListIndex > 0 ? localPlayingIndex - playListUrlOffsets[localPlayListIndex - 1] : -1;

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

        void Start() {
            synced = core.IsSynced;
            core._AddListener(this);
            if (!synced || Networking.IsOwner(gameObject)) {
                if (core.Loop) RepeatOne = true;
                if (localPlayListIndex > 0)
                    SendCustomEventDelayedFrames(nameof(_AutoPlay), 0);
            }
        }

        public void _AutoPlay() {
            var playIndex = localPlayListIndex;
            localPlayListIndex = 0;
            core.Loop = RepeatOne;
            if (defaultLoop) RepeatAll = true;
            if (defaultShuffle) Shuffle = true;
            _PlayAt(playIndex, -1, false);
        }
        
        protected void UpdateState() => SendEvent("_OnUIUpdate");

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
            core.Stop();
            localQueuedUrls = new VRCUrl[0];
            localQueuedPlayerIndex = new byte[0];
            localPlayListOrder = new ushort[0];
            localPlayingIndex = 0;
            localPlayListIndex = 0;
            RequestSync();
            SendEvent("_OnStop");
        }

        public void _Skip() {
            if (locked) return;
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

        public void _OnVideoError() => UpdateState();

        public void _OnVideoBeginLoad() => UpdateState();

        public void _OnVolumeChange() => SendEvent("_OnVolumeChange");

        public void _OnSyncOffsetChange() => SendEvent("_OnSyncOffsetChange");

        public override void OnPreSerialization() {
            if (!synced) return;
            queuedUrls = localQueuedUrls == null ? new VRCUrl[0] : localQueuedUrls;
            queuedPlayerIndex = localQueuedPlayerIndex == null ? new byte[0] : localQueuedPlayerIndex;
            playListOrder = localPlayListOrder == null ? new ushort[0] : localPlayListOrder;
            flags = localFlags;
            playListIndex = (ushort)localPlayListIndex;
            playingIndex = localPlayingIndex;
            core.Loop = RepeatOne;
        }

        public override void OnDeserialization() {
            if (!synced) return;
            localQueuedUrls = queuedUrls;
            localQueuedPlayerIndex = queuedPlayerIndex;
            localPlayListOrder = playListOrder;
            localFlags = flags;
            localPlayListIndex = playListIndex;
            localPlayingIndex = playingIndex;
            core.Loop = RepeatOne;
            UpdateState();
        }

        public void PlayUrl(VRCUrl url, byte index) {
            if (!Utilities.IsValid(url)) return;
            bool shouldRequestSync = false;
            if (localPlayListIndex > 0) {
                localPlayListIndex = 0;
                localQueuedUrls = null;
                localQueuedPlayerIndex = null;
                core.Stop();
                shouldRequestSync = true;
            }
            if (enableQueueList) {
                bool isPlaying = core.IsReady || core.IsLoading || (localQueuedUrls != null && localQueuedUrls.Length > 0);
                bool isRepeat = RepeatAll;
                if (isRepeat && isPlaying) {
                    var lastUrl = core.Url;
                    if (Utilities.IsValid(lastUrl) && !lastUrl.Equals(VRCUrl.Empty)) {
                        bool isCurrentQueued = false;
                        if (localQueuedUrls == null || localQueuedUrls.Length < 2) {
                            localQueuedUrls = new VRCUrl[] { url, lastUrl };
                        } else {
                            isCurrentQueued = lastUrl.Equals(localQueuedUrls.Length - 1);
                            var newQueue = new VRCUrl[localQueuedUrls.Length + (isCurrentQueued ? 1 : 2)];
                            Array.Copy(localQueuedUrls, newQueue, localQueuedUrls.Length);
                            newQueue[newQueue.Length - 2] = url;
                            newQueue[newQueue.Length - 1] = lastUrl;
                            localQueuedUrls = newQueue;
                        }
                        var lastIndex = core.ActivePlayer;
                        if (localQueuedPlayerIndex == null || localQueuedPlayerIndex.Length < 2) {
                            localQueuedPlayerIndex = new byte[] { index, lastIndex };
                        } else {
                            var newPlayerIndexQueue = new byte[localQueuedPlayerIndex.Length + (isCurrentQueued ? 1 : 2)];
                            Array.Copy(localQueuedPlayerIndex, newPlayerIndexQueue, localQueuedPlayerIndex.Length);
                            newPlayerIndexQueue[newPlayerIndexQueue.Length - 2] = index;
                            newPlayerIndexQueue[newPlayerIndexQueue.Length - 1] = lastIndex;
                            localQueuedPlayerIndex = newPlayerIndexQueue;
                        }
                        RequestSync();
                        UpdateState();
                        return;
                    }
                }
                if (isPlaying || isRepeat) {
                    if (localQueuedUrls == null || localQueuedUrls.Length == 0) {
                        localQueuedUrls = new VRCUrl[] { url };
                    } else {
                        var newQueue = new VRCUrl[localQueuedUrls.Length + 1];
                        Array.Copy(localQueuedUrls, newQueue, localQueuedUrls.Length);
                        newQueue[localQueuedUrls.Length] = url;
                        localQueuedUrls = newQueue;
                    }
                    if (localQueuedPlayerIndex == null || localQueuedPlayerIndex.Length == 0) {
                        localQueuedPlayerIndex = new byte[] { index };
                    } else {
                        var newPlayerIndexQueue = new byte[localQueuedPlayerIndex.Length + 1];
                        Array.Copy(localQueuedPlayerIndex, newPlayerIndexQueue, localQueuedPlayerIndex.Length);
                        newPlayerIndexQueue[localQueuedPlayerIndex.Length] = index;
                        localQueuedPlayerIndex = newPlayerIndexQueue;
                    }
                    RequestSync();
                    UpdateState();
                    if (!isPlaying) core.PlayUrl(url, index);
                    return;
                }
            }
            if (shouldRequestSync) RequestSync();
            core.PlayUrl(url, index);
        }

        public void _PlayNext() {
            if (synced && !Networking.IsOwner(gameObject)) return;
            _PlayAt(localPlayListIndex, -1, false);
        }

        public void _PlayAt(int playListIndex, int entryIndex, bool deleteOnly) {
            if (playListIndex != localPlayListIndex) {
                localQueuedUrls = null;
                localQueuedPlayerIndex = null;
                localPlayListOrder = null;
                localPlayListIndex = playListIndex;
            }
            if (localPlayListIndex > 0)
                PlayPlayList(entryIndex);
            else
                PlayQueueList(entryIndex, deleteOnly);
        }

        // API used with UdonAuth or other capability systems
        public void _OnUnlock() {
            if (!locked) return;
            locked = false;
            UpdateState();
        }

        void RefreshPlayListQueue(int startIndex) {
            if (localPlayListIndex <= 0 || localPlayListIndex > playListUrlOffsets.Length) {
                localPlayListOrder = new ushort[0];
                return;
            }
            int currentOffset = playListUrlOffsets[localPlayListIndex - 1];
            int nextOffset = localPlayListIndex == playListUrlOffsets.Length ? playListUrls.Length : playListUrlOffsets[localPlayListIndex];
            int length = nextOffset - currentOffset;
            if (length == 0) {
                localPlayListOrder = new ushort[0];
                return;
            }
            bool isRepeat = RepeatAll, isShuffle = Shuffle, skipped = false;
            if (startIndex < 0) {
                startIndex = localPlayingIndex - currentOffset + 1;
                skipped = true;
            }
            if (startIndex >= length) {
                if (!isRepeat && !isShuffle) {
                    localPlayListOrder = new ushort[0];
                    return;
                }
                startIndex %= length;
            }
            int remainCount = length;
            if (!isRepeat) {
                if (!isShuffle) remainCount -= startIndex;
                else if (skipped) remainCount--;
            }
            localPlayListOrder = new ushort[remainCount];
            for (int i = 0; i < remainCount; i++)
                localPlayListOrder[i] = (ushort)(currentOffset + (i + startIndex) % length);
            if (isShuffle) {
                int startFrom = skipped ? 0 : 1;
                for (int i = startFrom + 1; i < remainCount; i++) {
                    int j = UnityEngine.Random.Range(startFrom, remainCount);
                    var tmp = localPlayListOrder[i];
                    localPlayListOrder[i] = localPlayListOrder[j];
                    localPlayListOrder[j] = tmp;
                }
            }
        }

        void PlayPlayList(int index) {
            if (index >= 0) RefreshPlayListQueue(index);
            if (localPlayListOrder == null) {
                localPlayListIndex = 0;
                RequestSync();
                UpdateState();
                return;
            }
            int newLength = localPlayListOrder.Length;
            if (newLength <= 0) {
                localPlayListIndex = 0;
                RequestSync();
                UpdateState();
                return;
            }
            localPlayingIndex = localPlayListOrder[0];
            newLength--;
            if (RepeatAll) {
                Array.Copy(localPlayListOrder, 1, localPlayListOrder, 0, newLength);
                localPlayListOrder[newLength] = localPlayingIndex;
            } else {
                var newOrderList = new ushort[newLength];
                Array.Copy(localPlayListOrder, 1, newOrderList, 0, newLength);
                localPlayListOrder = newOrderList;
            }
            core.PlayUrlMP(playListUrls[localPlayingIndex], playListUrlsQuest[localPlayingIndex], playListPlayerIndex[localPlayingIndex]);
            RequestSync();
            UpdateState();
        }

        void PlayQueueList(int index, bool deleteOnly) {
            if (localQueuedUrls == null) return;
            int newLength = localQueuedUrls.Length;
            if (index >= newLength || newLength <= 0) return;
            if (index < 0) index = Shuffle ? UnityEngine.Random.Range(0, newLength) : 0;
            bool isRepeatAll = RepeatAll;
            if (deleteOnly || !isRepeatAll) newLength--;
            var url = localQueuedUrls[index];
            var playerIndex = localQueuedPlayerIndex[index];
            var newQueue = newLength == localQueuedUrls.Length ? localQueuedUrls : new VRCUrl[newLength];
            var newPlayerIndexQueue = newLength == localQueuedUrls.Length ? localQueuedPlayerIndex : new byte[newLength];
            if (index > 0) {
                if (localQueuedUrls != newQueue)
                    Array.Copy(localQueuedUrls, 0, newQueue, 0, index);
                if (localQueuedPlayerIndex != newPlayerIndexQueue)
                    Array.Copy(localQueuedPlayerIndex, 0, newPlayerIndexQueue, 0, index);
            }
            Array.Copy(localQueuedUrls, index + 1, newQueue, index, Mathf.Min(localQueuedUrls.Length - 1, newLength) - index);
            Array.Copy(localQueuedPlayerIndex, index + 1, newPlayerIndexQueue, index, Mathf.Min(localQueuedUrls.Length - 1, newLength) - index);
            if (isRepeatAll && !deleteOnly) {
                newLength--;
                newQueue[newLength] = url;
                newPlayerIndexQueue[newLength] = playerIndex;
            }
            localQueuedUrls = newQueue;
            localQueuedPlayerIndex = newPlayerIndexQueue;
            RequestSync();
            if (!deleteOnly) core.PlayUrl(url, playerIndex);
            UpdateState();
        }

        bool RequestSync() {
            if (!synced) return false;
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();
            return true;
        }
    }
}
