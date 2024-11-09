using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UnityEngine.Serialization;

namespace JLChnToZ.VRC.VVMW {
    public partial class FrontendHandler {
        [SerializeField] string[] playListTitles;
        [SerializeField] int[] playListUrlOffsets;
        [SerializeField] VRCUrl[] playListUrls, playListUrlsQuest;
        [SerializeField] string[] playListEntryTitles;
        [SerializeField] byte[] playListPlayerIndex;
        [SerializeField, FormerlySerializedAs("localPlayListIndex")] int defaultPlayListIndex;
        [UdonSynced] ushort[] playListOrder;
        [UdonSynced] ushort playingIndex;
        [UdonSynced] ushort playListIndex;
        ushort[] localPlayListOrder;
        int localPlayListIndex;
        ushort localPlayingIndex;

        public string[] PlayListTitles => playListTitles;
    
        public int[] PlayListUrlOffsets => playListUrlOffsets;
    
        public VRCUrl[] PlayListUrls => playListUrls;

        public VRCUrl[] PlayListUrlsQuest => playListUrlsQuest;
    
        public string[] PlayListEntryTitles => playListEntryTitles;
    
        public byte[] PlayListPlayerIndex => playListPlayerIndex;

        public int CurrentPlayingIndex => localPlayListIndex > 0 ? localPlayingIndex - playListUrlOffsets[localPlayListIndex - 1] : -1;

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
                SeedRandomBeforeShuffle();
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
            core.PlayUrl(playListUrls[localPlayingIndex], playListUrlsQuest[localPlayingIndex], playListPlayerIndex[localPlayingIndex]);
            core.SetTitle(playListEntryTitles[localPlayingIndex], playListTitles[localPlayListIndex - 1]);
            RequestSync();
            UpdateState();
        }
    }
}