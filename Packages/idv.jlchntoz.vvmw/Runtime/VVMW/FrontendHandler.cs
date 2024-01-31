using System;
using System.Globalization;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UnityEngine.Serialization;

#if AUDIOLINK_V1
using AudioLink;
#endif

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Frontend Handler")]
    [DefaultExecutionOrder(1)]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#play-list-queue-handler")]
    public class FrontendHandler : UdonSharpEventSender {
        protected const byte NONE = 0, REPEAT_ONE = 0x1, REPEAT_ALL = 0x2, SHUFFLE = 0x4;
        [SerializeField, Locatable, BindUdonSharpEvent, SingletonCoreControl] public Core core;
        [Tooltip("If enabled, while user want to play a video and it is playing other video, the video will be queued. Recommend as this is more polite to everyone.")]
        [SerializeField] bool enableQueueList = true;
        [Tooltip("Locks the player frontend by default, this option must be used with other scripts to control the player.")]
        [FieldChangeCallback(nameof(Locked))]
        [SerializeField] bool locked = false;
        [SerializeField] bool defaultLoop, defaultShuffle;
        [SerializeField] string[] playListTitles;
        [SerializeField] int[] playListUrlOffsets;
        [SerializeField] VRCUrl[] playListUrls, playListUrlsQuest;
        [SerializeField] string[] playListEntryTitles;
        [SerializeField] byte[] playListPlayerIndex;
        [SerializeField, FormerlySerializedAs("localPlayListIndex")] int defaultPlayListIndex;
        [Tooltip("Automatically play the default play list when the player is ready.")]
        [SerializeField] bool autoPlay = true;
        [Tooltip("The delay to start playing the default video when the scene is loaded.\n" +
            "This is to prevent rate limit between video players in same instance.\n" +
            "If you have multiple video players (not limited to VizVid) which will auto plays in the same world, " +
            "you should set this to a value at least in multiple of 5 to stagger the loading time.")]
        [SerializeField] float autoPlayDelay = 0;
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
        string[] localQueuedTitles;
        byte localFlags;
        int localPlayListIndex;
        ushort localPlayingIndex;
        bool afterFirstRun, isDataArrivedBeforeInit;

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

        public string[] QueueTitles {
            get {
                if (localQueuedTitles == null) localQueuedTitles = new string[0];
                return localQueuedTitles;
            }
        }

        public bool Locked {
            get => locked;
            private set {
                if (value) _Lock();
                else _OnUnlock();
            }
        }

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
            if (defaultShuffle) localFlags |= SHUFFLE;
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

        void UpdateAudioLink() {
            #if AUDIOLINK_V1
            var audioLink = core.AudioLink;
            if (audioLink != null) {
                if ((localFlags & REPEAT_ALL) != 0) {
                    if ((localFlags & SHUFFLE) != 0)
                        audioLink.SetMediaLoop(MediaLoop.RandomLoop);
                    else
                        audioLink.SetMediaLoop(MediaLoop.Loop);
                } else if ((localFlags & REPEAT_ONE) != 0)
                    audioLink.SetMediaLoop(MediaLoop.LoopOne);
                else if ((localFlags & SHUFFLE) != 0)
                    audioLink.SetMediaLoop(MediaLoop.Random);
                else
                    audioLink.SetMediaLoop(MediaLoop.None);
            }
            #endif
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

        public override void OnPreSerialization() {
            if (!synced) return;
            queuedUrls = localQueuedUrls == null ? new VRCUrl[0] : localQueuedUrls;
            queuedPlayerIndex = localQueuedPlayerIndex == null ? new byte[0] : localQueuedPlayerIndex;
            playListOrder = localPlayListOrder == null ? new ushort[0] : localPlayListOrder;
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
            localQueuedPlayerIndex = queuedPlayerIndex;
            localPlayListOrder = playListOrder;
            if (localQueuedTitles == null || localQueuedTitles.Length != queuedUrls.Length) {
                localQueuedTitles = new string[queuedUrls.Length];
                for (int i = 0; i < localQueuedTitles.Length; i++)
                    localQueuedTitles[i] = UnescapeUrl(queuedUrls[i]);
            }
            localFlags = flags;
            if (playListIndex > 0 && (localPlayListIndex != playListIndex || localPlayingIndex != playingIndex))
                core.SetTitle(playListEntryTitles[playingIndex], playListTitles[playListIndex - 1]);
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
                localQueuedTitles = null;
                core.Stop();
                shouldRequestSync = true;
            }
            if (enableQueueList && (core.IsReady || core.IsLoading || (localQueuedUrls != null && localQueuedUrls.Length > 0))) {
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
                if (localQueuedTitles == null || localQueuedTitles.Length == 0) {
                    localQueuedTitles = new string[] { UnescapeUrl(url) };
                } else {
                    var newTitles = new string[localQueuedTitles.Length + 1];
                    Array.Copy(localQueuedTitles, newTitles, localQueuedTitles.Length);
                    newTitles[localQueuedTitles.Length] = UnescapeUrl(url);
                    localQueuedTitles = newTitles;
                }
                RequestSync();
                UpdateState();
                return;
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

        public void _Lock() {
            if (locked) return;
            locked = true;
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
            core.SetTitle(playListEntryTitles[localPlayingIndex], playListTitles[localPlayListIndex - 1]);
            RequestSync();
            UpdateState();
        }

        void PlayQueueList(int index, bool deleteOnly) {
            if (localQueuedUrls == null) return;
            int newLength = localQueuedUrls.Length;
            if (index >= newLength || newLength <= 0) return;
            if (index < 0) index = Shuffle ? UnityEngine.Random.Range(0, newLength) : 0;
            newLength--;
            var url = localQueuedUrls[index];
            var playerIndex = localQueuedPlayerIndex[index];
            var newQueue = newLength == localQueuedUrls.Length ? localQueuedUrls : new VRCUrl[newLength];
            var newPlayerIndexQueue = newLength == localQueuedUrls.Length ? localQueuedPlayerIndex : new byte[newLength];
            var newTitles = newLength == localQueuedUrls.Length ? localQueuedTitles : new string[newLength];
            if (index > 0) {
                if (localQueuedUrls != newQueue)
                    Array.Copy(localQueuedUrls, 0, newQueue, 0, index);
                if (localQueuedPlayerIndex != newPlayerIndexQueue)
                    Array.Copy(localQueuedPlayerIndex, 0, newPlayerIndexQueue, 0, index);
                if (localQueuedTitles != newTitles)
                    Array.Copy(localQueuedTitles, 0, newTitles, 0, index);
            }
            int copyCount = Mathf.Min(localQueuedUrls.Length - 1, newLength) - index;
            Array.Copy(localQueuedUrls, index + 1, newQueue, index, copyCount);
            Array.Copy(localQueuedPlayerIndex, index + 1, newPlayerIndexQueue, index, copyCount);
            Array.Copy(localQueuedTitles, index + 1, newTitles, index, copyCount);
            localQueuedUrls = newQueue;
            localQueuedPlayerIndex = newPlayerIndexQueue;
            localQueuedTitles = newTitles;
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

        public void _OnTitleData() => UpdateState();

        string UnescapeUrl(VRCUrl url) {
            if (!Utilities.IsValid(url)) return "";
            var title = url.Get();
            if (string.IsNullOrEmpty(title)) return "";
            int index = 0;
            string result = "";
            while (index >= 0) {
                int offset = title.IndexOf('%', index);
                if (offset < 0 || offset + 3 > title.Length) {
                    result += title.Substring(index);
                    break;
                }
                if (offset > index) {
                    result += title.Substring(index, offset - index);
                    index = offset;
                }
                // Not even a valid hex number
                if (!byte.TryParse(title.Substring(offset + 1, 2), NumberStyles.HexNumber, null, out byte b)) {
                    result += '%';
                    index++;
                    continue;
                }
                int utf32, length;
                if      ((b & 0x80) == 0x00) { utf32 = b;        length = 1; }
                else if ((b & 0xE0) == 0xC0) { utf32 = b & 0x1F; length = 2; }
                else if ((b & 0xF0) == 0xE0) { utf32 = b & 0x0F; length = 3; }
                else if ((b & 0xF8) == 0xF0) { utf32 = b & 0x07; length = 4; }
                else if ((b & 0xFC) == 0xF8) { utf32 = b & 0x03; length = 5; }
                else if ((b & 0xFE) == 0xFC) { utf32 = b & 0x01; length = 6; }
                else { result += '%'; index++; continue; } // Invalid UTF-8
                // Not enough bytes
                if (index + length * 3 > title.Length) {
                    result += '%';
                    index++;
                    continue;
                }
                for (int i = 1; i < length; i++) {
                    offset = index + i * 3;
                    if (title[offset] != '%' ||
                        !byte.TryParse(title.Substring(offset + 1, 2), NumberStyles.HexNumber, null, out b) ||
                        (b & 0xC0) != 0x80) {
                        result += '%';
                        utf32 = 0;
                        break;
                    }
                    utf32 <<= 6;
                    utf32 |= b & 0x3F;
                }
                if (utf32 == 0) {
                    index++;
                    continue;
                }
                result += char.ConvertFromUtf32(utf32);
                index += length * 3;
            }
            return result;
        }
    }
}
