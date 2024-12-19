using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class FrontendHandler {
        [SerializeField, LocalizedLabel] bool enableQueueList = true;
        [UdonSynced] VRCUrl[] queuedUrls, queuedQuestUrls;
        [UdonSynced] string queuedTitles;
        [UdonSynced] byte[] queuedPlayerIndex;
        VRCUrl[] localQueuedUrls, localQueuedQuestUrls;
        byte[] localQueuedPlayerIndex;
        string[] localQueuedTitles;

        /// <summary>
        /// The URLs of the queued items.
        /// </summary>
        /// <remarks>The returned array is meant to be read only, do not modify.</remarks>
        public VRCUrl[] QueueUrls {
            get {
                if (!Utilities.IsValid(localQueuedUrls)) localQueuedUrls = new VRCUrl[0];
                return localQueuedUrls;
            }
        }

        /// <summary>
        /// The player backend index of the queued items.
        /// </summary>
        /// <remarks>
        /// This is 1-based index, 0 is invalid.
        /// </remarks>
        /// <remarks>The returned array is meant to be read only, do not modify.</remarks>
        public byte[] QueuePlayerIndex {
            get {
                if (!Utilities.IsValid(localQueuedPlayerIndex)) localQueuedPlayerIndex = new byte[0];
                return localQueuedPlayerIndex;
            }
        }

        /// <summary>
        /// The titles of the queued items.
        /// </summary>
        /// <remarks>The returned array is meant to be read only, do not modify.</remarks>
        public string[] QueueTitles {
            get {
                if (!Utilities.IsValid(localQueuedTitles)) localQueuedTitles = new string[0];
                return localQueuedTitles;
            }
        }

        /// <summary>
        /// Whether the queue list is enabled.
        /// </summary>
        public bool HasQueueList => enableQueueList;

        /// <inheritdoc cref="PlayUrl(VRCUrl, VRCUrl, string, byte)"/>
        public void PlayUrl(VRCUrl url, byte index) => PlayUrl(url, null, null, index);

        /// <inheritdoc cref="PlayUrl(VRCUrl, VRCUrl, string, byte)"/>
        public void PlayUrl(VRCUrl pcUrl, VRCUrl questUrl, byte index) => PlayUrl(pcUrl, questUrl, null, index);

        /// <summary>
        /// Play or enqueue a video URL.
        /// </summary>
        /// <param name="url">The URL of the video.</param>
        /// <param name="pcUrl">The URL of the video for PC.</param>
        /// <param name="questUrl">The URL of the video for Quest (mobile).</param>
        /// <param name="queuedTitle">The title of the video to be displayed in the queue list.</param>
        /// <param name="index">The player index (1-based) to play the video.</param>
        /// <remarks>
        /// If queue list is enabled and the player is playing or loading, the URL will be enqueued.
        /// Except if it is playing an entry of a playlist, in this case will always intrrupts the current playback.
        /// </remarks>
        public void PlayUrl(VRCUrl pcUrl, VRCUrl questUrl, string queuedTitle, byte index) {
            if (VRCUrl.IsNullOrEmpty(pcUrl)) return;
            if (VRCUrl.IsNullOrEmpty(questUrl)) questUrl = pcUrl;
            bool shouldRequestSync = false;
            if (localPlayListIndex > 0) {
                localPlayListIndex = 0;
                localQueuedUrls = null;
                localQueuedQuestUrls = null;
                localQueuedPlayerIndex = null;
                localQueuedTitles = null;
                core.Stop();
                shouldRequestSync = true;
            }
            if (string.IsNullOrEmpty(queuedTitle))
                queuedTitle = $"{Networking.LocalPlayer.displayName}:\n{UnescapeUrl(pcUrl)}";
            if (enableQueueList) {
                if (core.IsReady || core.IsLoading || Utilities.IsValid(localQueuedUrls) && localQueuedUrls.Length != 0) {
                    EnqueueToQueueList(pcUrl, questUrl, queuedTitle, index);
                    UpdateState();
                    RequestSync();
                    return;
                }
                if (RepeatAll) {
                    EnqueueToQueueList(pcUrl, questUrl, queuedTitle, index);
                    UpdateState();
                    shouldRequestSync = true;
                }
            }
            RecordPlaybackHistory(pcUrl, questUrl, index, queuedTitle);
            if (shouldRequestSync || historySize > 0) RequestSync();
            core.PlayUrl(pcUrl, questUrl, index);
            core._ResetTitle();
            UpdateLoop();
        }

        void EnqueueToQueueList(VRCUrl pcUrl, VRCUrl questUrl, string queuedTitle, byte index) {
            if (IsArrayNullOrEmpty(localQueuedUrls))
                localQueuedUrls = new VRCUrl[] { pcUrl };
            else {
                var newQueue = new VRCUrl[localQueuedUrls.Length + 1];
                Array.Copy(localQueuedUrls, newQueue, localQueuedUrls.Length);
                newQueue[localQueuedUrls.Length] = pcUrl;
                localQueuedUrls = newQueue;
            }
            bool isQuestQueueEmpty = IsArrayNullOrEmpty(localQueuedQuestUrls);
            if (!pcUrl.Equals(questUrl) || !isQuestQueueEmpty) {
                if (isQuestQueueEmpty)
                    localQueuedQuestUrls = new VRCUrl[] { questUrl };
                else {
                    var newAltQueue = new VRCUrl[localQueuedQuestUrls.Length + 1];
                    Array.Copy(localQueuedQuestUrls, newAltQueue, localQueuedQuestUrls.Length);
                    newAltQueue[localQueuedQuestUrls.Length] = questUrl;
                    localQueuedQuestUrls = newAltQueue;
                }
            }
            if (IsArrayNullOrEmpty(localQueuedPlayerIndex))
                localQueuedPlayerIndex = new byte[] { index };
            else {
                var newPlayerIndexQueue = new byte[localQueuedPlayerIndex.Length + 1];
                Array.Copy(localQueuedPlayerIndex, newPlayerIndexQueue, localQueuedPlayerIndex.Length);
                newPlayerIndexQueue[localQueuedPlayerIndex.Length] = index;
                localQueuedPlayerIndex = newPlayerIndexQueue;
            }
            if (IsArrayNullOrEmpty(localQueuedTitles))
                localQueuedTitles = new string[] { queuedTitle };
            else {
                var newTitles = new string[localQueuedTitles.Length + 1];
                Array.Copy(localQueuedTitles, newTitles, localQueuedTitles.Length);
                newTitles[localQueuedTitles.Length] = queuedTitle;
                localQueuedTitles = newTitles;
            }
            UpdateLoop();
        }

        void PlayQueueList(int index, bool deleteOnly) {
            if (!Utilities.IsValid(localQueuedUrls)) return;
            int newLength = localQueuedUrls.Length;
            if (index >= newLength || newLength <= 0) return;
            if (index < 0) {
                if (deleteOnly) {
                    localPlayListIndex = 0;
                    localQueuedUrls = null;
                    localQueuedQuestUrls = null;
                    localQueuedPlayerIndex = null;
                    localQueuedTitles = null;
                    UpdateLoop();
                    RequestSync();
                    UpdateState();
                    return;
                }
                index = Shuffle ? UnityEngine.Random.Range(0, newLength) : 0;
            }
            bool addToEnd = !deleteOnly && RepeatAll;
            if (!addToEnd) newLength--;
            var url = localQueuedUrls[index];
            bool hasQuestUrl = !IsArrayNullOrEmpty(localQueuedQuestUrls);
            var questUrl = hasQuestUrl ? localQueuedQuestUrls[index] : url;
            var playerIndex = localQueuedPlayerIndex[index];
            var title = localQueuedTitles[index];
            var newQueue = newLength == localQueuedUrls.Length ? localQueuedUrls : new VRCUrl[newLength];
            var newQuestQueue = hasQuestUrl ? newLength == localQueuedQuestUrls.Length ? localQueuedQuestUrls : new VRCUrl[newLength] : null;
            var newPlayerIndexQueue = newLength == localQueuedUrls.Length ? localQueuedPlayerIndex : new byte[newLength];
            var newTitles = newLength == localQueuedUrls.Length ? localQueuedTitles : new string[newLength];
            if (index > 0) {
                if (localQueuedUrls != newQueue)
                    Array.Copy(localQueuedUrls, 0, newQueue, 0, index);
                if (hasQuestUrl && localQueuedQuestUrls != newQuestQueue)
                    Array.Copy(localQueuedQuestUrls, 0, newQuestQueue, 0, index);
                if (localQueuedPlayerIndex != newPlayerIndexQueue)
                    Array.Copy(localQueuedPlayerIndex, 0, newPlayerIndexQueue, 0, index);
                if (localQueuedTitles != newTitles)
                    Array.Copy(localQueuedTitles, 0, newTitles, 0, index);
            }
            int copyCount = Mathf.Min(localQueuedUrls.Length - 1, newLength) - index;
            Array.Copy(localQueuedUrls, index + 1, newQueue, index, copyCount);
            if (hasQuestUrl) Array.Copy(localQueuedQuestUrls, index + 1, newQuestQueue, index, copyCount);
            Array.Copy(localQueuedPlayerIndex, index + 1, newPlayerIndexQueue, index, copyCount);
            Array.Copy(localQueuedTitles, index + 1, newTitles, index, copyCount);
            if (addToEnd) {
                int lastIndex = newLength - 1;
                newQueue[lastIndex] = url;
                if (hasQuestUrl) newQuestQueue[lastIndex] = questUrl;
                newPlayerIndexQueue[lastIndex] = playerIndex;
                newTitles[lastIndex] = title;
            }
            localQueuedUrls = newQueue;
            localQueuedQuestUrls = newQuestQueue;
            localQueuedPlayerIndex = newPlayerIndexQueue;
            localQueuedTitles = newTitles;
            if (!deleteOnly) {
                core.PlayUrl(url, questUrl, playerIndex);
                core._ResetTitle();
                RecordPlaybackHistory(url, questUrl, playerIndex, title);
            }
            UpdateLoop();
            RequestSync();
            UpdateState();
        }
    }
}