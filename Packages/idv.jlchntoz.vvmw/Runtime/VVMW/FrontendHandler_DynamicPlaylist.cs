// This feature will be available when VRChat decided to expose VRCUrl.TryCreateAllowlistedVRCUrl to Udon.
// #if !COMPILER_UDONSHARP
// #define VRC_SUPPORT_TRY_CREATE_URL
// #endif
#if VRC_SUPPORT_TRY_CREATE_URL
using System;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.StringLoading;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Data;

namespace JLChnToZ.VRC.VVMW {
    public partial class FrontendHandler {
        DataDictionary readUrlMap;

        public void LoadDynamicPlaylist(VRCUrl url) {
            VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
        }

        void LoadDynamicPlaylist(int index) {
            if (readUrlMap == null) readUrlMap = new DataDictionary();
            int offset = index > 0 ? playListUrlOffsets[index - 1] : 0;
            if (playListPlayerIndex[offset] != 0) return;
            var url = playListUrls[offset];
            readUrlMap[url.Get()] = offset;
            VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
        }

        public override void OnStringLoadSuccess(IVRCStringDownload result) {
            var url = result.Url.Get();
            if (!VRCJson.TryDeserializeFromJson(result.Result, out var token)) {
                Debug.LogWarning($"[Dynamic Playlist] Failed to parse JSON from {result.Url}: {token}");
                return;
            }
            int index = -1;
            if (readUrlMap != null && readUrlMap.TryGetValue(url, TokenType.Int, out token))
                index = token.Int;
            switch (token.TokenType) {
                case TokenType.DataDictionary:
                    var dict = token.DataDictionary;
                    string hash = null;
                    int hashIndex = url.IndexOf('#');
                    if (hashIndex >= 0) hash = url.Substring(0, hashIndex);
                    if (hash != null) {
                        if (dict.TryGetValue(hash, TokenType.DataList, out token))
                            ApplyDynamicPlaylist(hash, index, token.DataList);
                        break;
                    } else {
                        var keys = dict.GetKeys();
                        for (int i = 0, count = dict.Count; i < count; i++) {
                            var key = keys[i];
                            if (dict.TryGetValue(key, TokenType.DataList, out token))
                                ApplyDynamicPlaylist(key.String, index, token.DataList);
                        }
                    }
                    break;
                case TokenType.DataList:
                    ApplyDynamicPlaylist(UnescapeUrl(result.Url), index, token.DataList);
                    break;
            }
        }

        void ApplyDynamicPlaylist(string title, int index, DataList listData) {
            int length = listData.Count;
            int offset;
            if (index < 0) { // Append to queue list
                offset = localQueuedUrls.Length;
                localQueuedUrls = (VRCUrl[])EnsureArrayLength(typeof(VRCUrl), localQueuedUrls, localQueuedUrls.Length + length);
                localQueuedQuestUrls = (VRCUrl[])EnsureArrayLength(typeof(VRCUrl), localQueuedQuestUrls, localQueuedQuestUrls.Length + length);
                localQueuedTitles = (string[])EnsureArrayLength(typeof(string), localQueuedTitles, localQueuedTitles.Length + length);
                localQueuedPlayerIndex = (byte[])EnsureArrayLength(typeof(byte), localQueuedPlayerIndex, localQueuedPlayerIndex.Length + length);
                ApplyDynamicPlaylist(listData, offset, localQueuedUrls, localQueuedQuestUrls, localQueuedTitles, localQueuedPlayerIndex);
                RequestSync();
                UpdateState();
                return;
            }
            if (index >= playListUrlOffsets.Length) { // Append to playlist list
                index = playListUrlOffsets.Length;
                playListUrlOffsets = (int[])EnsureArrayLength(typeof(int), playListUrlOffsets, playListUrlOffsets.Length + 1);
                playListTitles = (string[])EnsureArrayLength(typeof(string), playListTitles, playListTitles.Length + 1);
                offset = playListUrls.Length;
                playListUrlOffsets[index] = offset;
                playListTitles[index] = title;
                playListUrls = (VRCUrl[])EnsureArrayLength(typeof(VRCUrl), playListUrls, playListUrls.Length + length);
                playListUrlsQuest = (VRCUrl[])EnsureArrayLength(typeof(VRCUrl), playListUrlsQuest, playListUrlsQuest.Length + length);
                playListEntryTitles = (string[])EnsureArrayLength(typeof(string), playListEntryTitles, playListEntryTitles.Length + length);
                playListPlayerIndex = (byte[])EnsureArrayLength(typeof(byte), playListPlayerIndex, playListPlayerIndex.Length + length);
                ApplyDynamicPlaylist(listData, offset, playListUrls, playListUrlsQuest, playListEntryTitles, playListPlayerIndex);
            } else { // Replace playlist list
                offset = index > 0 ? playListUrlOffsets[index - 1] : 0;
                int orgLength = playListUrlOffsets[index] - offset;
                playListUrls = (VRCUrl[])SplitArrayAtMiddle(typeof(VRCUrl), playListUrls, offset, length);
                playListUrlsQuest = (VRCUrl[])SplitArrayAtMiddle(typeof(VRCUrl), playListUrlsQuest, offset, length);
                playListEntryTitles = (string[])SplitArrayAtMiddle(typeof(string), playListEntryTitles, offset, length);
                playListPlayerIndex = (byte[])SplitArrayAtMiddle(typeof(byte), playListPlayerIndex, offset, length);
                playListUrlOffsets = (int[])SplitArrayAtMiddle(typeof(int), playListUrlOffsets, index, 1);
                playListTitles = (string[])SplitArrayAtMiddle(typeof(string), playListTitles, index, 1);
                playListUrlOffsets[index] = offset;
                playListTitles[index] = title;
                for (int i = index + 1, offsetLength = playListUrlOffsets.Length, diff = length - orgLength; i < offsetLength; i++)
                    playListUrlOffsets[i] += diff;
                ApplyDynamicPlaylist(listData, offset, playListUrls, playListUrlsQuest, playListEntryTitles, playListPlayerIndex);
            }
            RequestSync();
            UpdateState();
        }

        void ApplyDynamicPlaylist(DataList datas, int offset, VRCUrl[] pcUrls, VRCUrl[] questUrls, string[] titles, byte[] playerIndex) {
            for (int i = 0, length = datas.Count; i < length; i++) {
                var entry = datas[i];
                if (entry.TokenType != TokenType.DataDictionary) continue;
                var dict = entry.DataDictionary;
                if (dict.TryGetValue("url", TokenType.String, out var token) &&
                    VRCUrl.TryCreateAllowlistedVRCUrl(token.String, out var url)) {
                    pcUrls[offset + i] = url;
                } else
                    pcUrls[offset + i] = url = VRCUrl.Empty;
                if (dict.TryGetValue("questUrl", TokenType.String, out token) &&
                    VRCUrl.TryCreateAllowlistedVRCUrl(token.String, out var questUrl)) {
                    questUrls[offset + i] = questUrl;
                } else
                    questUrls[offset + i] = VRCUrl.Empty;
                if (dict.TryGetValue("title", TokenType.String, out token))
                    titles[offset + i] = token.String;
                else
                    titles[offset + i] = UnescapeUrl(url);
                if (dict.TryGetValue("playerIndex", TokenType.Double, out token)) // Data from json is always double
                    playerIndex[offset + i] = (byte)token.Double;
                else
                    playerIndex[offset + i] = 0;
            }
        }

        Array EnsureArrayLength(Type type, Array array, int length) {
            if (array != null && array.Length >= length) return array;
            var temp = Array.CreateInstance(type, length);
            if (array != null) Array.Copy(array, temp, array.Length);
            return temp;
        }

        Array SplitArrayAtMiddle(Type type, Array array, int index, int length) {
            if (array == null) return Array.CreateInstance(type, length);
            int orgLength = array.Length;
            if (index >= orgLength) index = orgLength;
            else if (index < 0) index = 0;
            var temp = Array.CreateInstance(type, orgLength + length);
            if (index > 0) Array.Copy(array, index, temp, 0, index);
            if (index < orgLength) Array.Copy(array, index, temp, index + length, orgLength - index);
            return temp;
        }
    }
}
#else
using UnityEngine;
using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    public partial class FrontendHandler {
        public void LoadDynamicPlaylist(VRCUrl url) {
            Debug.LogError("[Dynamic Playlist] This feature is not yet supported by current VRChat SDK.");
        }

        void LoadDynamicPlaylist(int index) {}
    }
}
#endif