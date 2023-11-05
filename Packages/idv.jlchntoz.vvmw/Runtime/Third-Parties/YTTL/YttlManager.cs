/*
 * YTTL - Video title viewer by ureishi
 * https://65536.booth.pm/items/4588619
 * License: CC0 - https://creativecommons.org/publicdomain/zero/1.0/deed
 * Modified by Vistanz
 */
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
        DataList labels;
        DataDictionary listeners;
        DataDictionary cache;

        public void LoadData(VRCUrl url, UdonSharpBehaviour listener) {
            var urlStr = url.Get();

            if (!parser.TryGetSupportedHost(urlStr, out var _discard)) {
                Debug.LogWarning("[YTTL] Unsupported host");
                return;
            }

            if (cache == null) cache = new DataDictionary();
            else if (cache.TryGetValue(urlStr, TokenType.DataDictionary, out var cacheToken)) {
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

            if (listeners == null) {
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

            if (labels == null) labels = new DataList(new DataToken[] { "author", "title", "viewCount", "description" });

            if (!parser.TryGetValue(host, labels, out var resultDict)) {
                Debug.LogWarning("[YTTL] Error when getting information");
                return;
            }

            cache[url] = resultDict;
            
            GetAllData(resultDict, out var author, out var title, out var viewCount, out var description);

            if (listeners.TryGetValue(url, TokenType.DataList, out var listenerToken)) {
                listeners.Remove(url);
                var listenerList = listenerToken.DataList;
                for (int i = 0, count = listenerList.Count; i < count; i++) {
                    var listener = (UdonBehaviour)listenerList[i].Reference;
                    if (listener == null) continue;
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
