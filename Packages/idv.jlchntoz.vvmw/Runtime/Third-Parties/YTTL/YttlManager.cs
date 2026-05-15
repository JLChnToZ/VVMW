/*
 * YTTL - Video title viewer by ureishi
 * https://65536.booth.pm/items/4588619
 * License: CC0 - https://creativecommons.org/publicdomain/zero/1.0/deed
 * Modified by Vistanz
 */
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace VVMW.ThirdParties.Yttl {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [AddComponentMenu("VizVid/Third-Parties/YTTL/YTTL Manager")]
    public class YttlManager : UdonSharpBehaviour {
        [SerializeField] YttlParser parser;
        [SerializeField] float loadDelaySeconds = 5.1f;
        DataList labels;
        DataDictionary listeners;
        DataDictionary cache;
        VRCUrl[] postDefineFileLoadUrls;
        bool isDefineFileReady;
    
        private void Start() => SendCustomEventDelayedSeconds(nameof(LoadDefineFile), loadDelaySeconds);

        public void LoadDefineFile() => parser.LoadDefineFile(this);

        public void OnPostLoadDefineFile() {
            isDefineFileReady = true;
            if (Utilities.IsValid(postDefineFileLoadUrls))
                foreach (var url in postDefineFileLoadUrls) {
                    if (!parser.TryGetSupportedHost(url.Get(), out var _discard)) {
                        Debug.LogWarning("[YTTL] Unsupported host");
                        continue;
                    }
                    VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
                }
            postDefineFileLoadUrls = null;
        }

        public void LoadData(VRCUrl url, UdonSharpBehaviour listener) {
            var urlStr = url.Get();

            if (Utilities.IsValid(cache) &&
                cache.TryGetValue(urlStr, TokenType.DataDictionary, out var cacheToken)) {
                Debug.Log("[YTTL] Found cache");
                GetAllData(cacheToken.DataDictionary, out var author, out var title, out var viewCount, out var description);
                listener.SetProgramVariable(nameof(url), url);
                listener.SetProgramVariable(nameof(author), author);
                listener.SetProgramVariable(nameof(title), title);
                listener.SetProgramVariable(nameof(viewCount), viewCount);
                listener.SetProgramVariable(nameof(description), description);
                listener.SendCustomEvent("Yttl_OnDataLoaded");
                return;
            }

            bool isRequesting;
            DataToken token;

            if (!Utilities.IsValid(listeners)) {
                listeners = new DataDictionary();
                isRequesting = false;
                token = default;
            } else
                isRequesting = listeners.TryGetValue(urlStr, TokenType.DataList, out token);

            if (!isRequesting) {
                token = new DataList();
                listeners[urlStr] = token;
            }
            token.DataList.Add(listener);

            if (isRequesting) return;

            if (!isDefineFileReady) {
                if (Utilities.IsValid(postDefineFileLoadUrls)) {
                    int length = postDefineFileLoadUrls.Length;
                    var newQueue = new VRCUrl[length + 1];
                    Array.Copy(postDefineFileLoadUrls, newQueue, length);
                    newQueue[length] = url;
                    postDefineFileLoadUrls = newQueue;
                } else
                    postDefineFileLoadUrls = new VRCUrl[] { url };
                return;
            }

            if (!parser.TryGetSupportedHost(urlStr, out var _discard)) {
                Debug.LogWarning("[YTTL] Unsupported host");
                return;
            }

            VRCStringDownloader.LoadUrl(url, (IUdonEventReceiver)this);
        }

        public override void OnStringLoadSuccess(IVRCStringDownload result) {
            var data = result.Result;

            var url = result.Url.Get();

            if (!parser.TryGetSupportedHost(url, out var host)) {
                Debug.LogWarning("[YTTL] Unsupported host");
                return;
            }

            parser.SetRawDataText(data);

            if (!Utilities.IsValid(labels)) labels = new DataList(new DataToken[] { "author", "title", "viewCount", "description" });

            if (!parser.TryGetValue(host, labels, out var resultDict)) {
                Debug.LogWarning("[YTTL] Error when getting information");
                return;
            }

            if (!Utilities.IsValid(cache)) cache = new DataDictionary();
            cache[url] = resultDict;
            
            GetAllData(resultDict, out var author, out var title, out var viewCount, out var description);

            if (listeners.TryGetValue(url, TokenType.DataList, out var listenerToken)) {
                listeners.Remove(url);
                var listenerList = listenerToken.DataList;
                for (int i = 0, count = listenerList.Count; i < count; i++) {
                    var listener = (UdonBehaviour)listenerList[i].Reference;
                    if (!Utilities.IsValid(listener)) continue;
                    listener.SetProgramVariable(nameof(url), result.Url);
                    listener.SetProgramVariable(nameof(author), author);
                    listener.SetProgramVariable(nameof(title), title);
                    listener.SetProgramVariable(nameof(viewCount), viewCount);
                    listener.SetProgramVariable(nameof(description), description);
                    listener.SendCustomEvent("Yttl_OnDataLoaded");
                }
            }
        }

        public override void OnStringLoadError(IVRCStringDownload result) {
            Debug.LogWarning($"[YTTL] Failed to download video information. Error: {result.Error} (ErrorCode: {result.ErrorCode})");
            listeners.Remove(result.Url.Get());
        }

        void GetAllData(DataDictionary resultDict, out string author, out string title, out string viewCount, out string description) {
            author = string.Empty;
            title = string.Empty;
            viewCount = string.Empty;
            description = string.Empty;

            if (resultDict.TryGetValue("author", TokenType.String, out var authorToken)) {
                author = authorToken.String;
                Debug.Log($"[YTTL] {nameof(author)}: {author}");
            }

            if (resultDict.TryGetValue("title", TokenType.String, out var titleToken)) {
                title = titleToken.String;
                Debug.Log($"[YTTL] {nameof(title)}: {title}");
            }

            if (resultDict.TryGetValue("viewCount", TokenType.String, out var viewCountToken)) {
                viewCount = viewCountToken.String;
                if (int.TryParse(viewCount, out var partInt))
                    viewCount = $"{partInt:#,0}";
                Debug.Log($"[YTTL] {nameof(viewCount)}: {viewCount}");
            }

            if (resultDict.TryGetValue("description", TokenType.String, out var descriptionToken)) {
                description = descriptionToken.String;
                Debug.Log($"[YTTL] {nameof(description)}: {description}");
            }
        }
    }
}
