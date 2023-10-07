/*
 * YTTL - Video title viewer by ureishi
 * https://65536.booth.pm/items/4588619
 * License: CC0 - https://creativecommons.org/publicdomain/zero/1.0/deed
 */
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace VVMW.ThirdParties.Yttl {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class YttlParser : UdonSharpBehaviour {
        private VRCUrl defineFileUrl = new VRCUrl("https://nextnex.com/?url=https://raw.githubusercontent.com/ureishi/yttl-data/v2/yttl.txt");

        private string rawDataText;

        private string RawDataText => rawDataText;

        private DataDictionary dataJson;

        private void Start() {
            LoadDefineFile();
        }

        public void LoadDefineFile() {
            VRCStringDownloader.LoadUrl(defineFileUrl, (IUdonEventReceiver)this);
        }

        public override void OnStringLoadSuccess(IVRCStringDownload result) {
            Init(result.Result);
        }

        public override void OnStringLoadError(IVRCStringDownload result) {
            Debug.LogWarning($"[YTTL] Definition data cannot be downloaded. Error: {result.Error} (ErrorCode: {result.ErrorCode})");
        }

        private bool inited = false;

        private void Init(string defineText) {
            if (inited) {
                return;
            }

            if (string.IsNullOrEmpty(defineText)) {
                Debug.LogWarning("[YTTL] Definition data is empty");
                return;
            }

            if (!TryParseDefine(defineText)) {
                Debug.LogWarning("[YTTL] Definition data cannot be parsed");
                return;
            }

            Debug.Log("[YTTL] Definition data loaded successfully");

            inited = true;
        }

        private bool parsed = false;

        private bool TryParseDefine(string tagDefineJsonText) {
            if (parsed) {
                return true;
            }

            if (!VRCJson.TryDeserializeFromJson(tagDefineJsonText, out var token)) {
                Debug.LogWarning($"[YTTL] {token.Error}: {token.String}");
                return false;
            }

            dataJson = token.DataDictionary;

            parsed = true;
            return true;
        }

        public void SetRawDataText(string rawDataText) {
            this.rawDataText = rawDataText;
        }

        private bool TryGetExtractedHost(string urlStr, out string host) {
            var index1 = urlStr.IndexOf("://");

            if (index1 == -1) {
                Debug.LogWarning("[YTTL] Invalid URL `://`");
                host = default;
                return false;
            }

            index1 += "://".Length;

            if (urlStr.Substring(index1).StartsWith("www.")) {
                index1 += "www.".Length;
            }

            var index2 = urlStr.IndexOf("/", index1);

            if (index2 == -1) {
                index2 = urlStr.IndexOf("?", index1);
            }

            if (index2 == -1) {
                Debug.LogWarning("[YTTL] Unsupported URL `/` or `?`");
                host = default;
                return false;
            }

            host = urlStr.Substring(index1, index2 - index1);
            return true;
        }

        private bool TryGetResolvedUrl(string urlStr, DataDictionary resolverParameters, out string resolvedUrl) {
            if (!resolverParameters.TryGetValue("s", TokenType.String, out var sToken)) {
                Debug.LogWarning("[YTTL] Parameter start information is missing");
                resolvedUrl = default;
                return false;
            }

            var s = sToken.String;
            resolverParameters.TryGetValue("t", TokenType.String, out var tToken);

            var t = tToken.TokenType == TokenType.String ? tToken.String : string.Empty;

            var index1 = urlStr.IndexOf(s);

            if (index1 == -1) {
                resolvedUrl = default;
                return false;
            }

            index1 += s.Length;

            if (string.IsNullOrEmpty(t)) {
                resolvedUrl = urlStr.Substring(index1);
                return true;
            } else {
                var index2 = urlStr.IndexOf(t, index1);

                if (index2 == -1) {
                    resolvedUrl = default;
                    return false;
                }

                resolvedUrl = urlStr.Substring(index1, index2 - index1);
                return true;
            }
        }

        public bool TryGetSupportedHost(string urlStr, out string supportedHost) {
            if (!inited) {
                Debug.LogWarning("[YTTL] Not initialized");
                supportedHost = default;
                return false;
            }

            if (!dataJson.TryGetValue("resolver", TokenType.DataDictionary, out var resolversToken)) {
                Debug.LogWarning("[YTTL] Unable to get resolver list");
                supportedHost = default;
                return false;
            }

            var resolvers = resolversToken.DataDictionary;

            if (!TryGetExtractedHost(urlStr, out var host)) {
                Debug.LogWarning($"[YTTL] Invalid URL `{urlStr}`");
                supportedHost = default;
                return false;
            }

            bool isResolver = resolvers.TryGetValue(host, TokenType.DataDictionary, out var resolverToken);

            if (!isResolver) {
                if (!resolvers.TryGetValue("", TokenType.DataDictionary, out resolverToken)) {
                    Debug.LogWarning("[YTTL] Resolver definition is invalid non use resolver");
                    supportedHost = default;
                    return false;
                }
            }

            var resolver = resolverToken.DataDictionary;

            if (isResolver) {
                if (!resolver.TryGetValue("parameter", TokenType.DataDictionary, out var parametersToken)) {
                    Debug.LogWarning("[YTTL] Unable to get resolver parameter information");
                    supportedHost = default;
                    return false;
                }

                var parameters = parametersToken.DataDictionary;

                if (!TryGetResolvedUrl(urlStr, parameters, out var resolvedUrl)) {
                    Debug.LogWarning("[YTTL] Unexpected resolver expression");
                    supportedHost = default;
                    return false;
                }

                if (!TryGetExtractedHost(resolvedUrl, out host)) {
                    Debug.Log($"[YTTL] Invalid URL to resolve `{resolvedUrl}`");
                    supportedHost = default;
                    return false;
                }
            }

            if (!resolver.TryGetValue("site", TokenType.DataList, out var sitesToken)) {
                Debug.LogWarning($"[YTTL] Unable to get supported site information");
            }

            var sites = sitesToken.DataList;

            if (sites.Contains(host)) {
                supportedHost = host;
                return true;
            } else {
                supportedHost = default;
                return false;
            }
        }

        public bool TryGetValue(string host, DataList labels, out DataDictionary result) {
            if (!inited) {
                Debug.LogWarning("[YTTL] Not initialized");
                result = default;
                return false;
            }

            if (string.IsNullOrEmpty(RawDataText)) {
                Debug.LogWarning("[YTTL] DataText is empty");
                result = default;
                return false;
            }

            if (!dataJson.TryGetValue("site", TokenType.DataDictionary, out var sitesToken)) {
                Debug.LogWarning("[YTTL] Unable to retrieve supported site information");
                result = default;
                return false;
            }

            var sites = sitesToken.DataDictionary;

            if (!sites.TryGetValue(host, TokenType.DataDictionary, out var hostDataToken)) {
                Debug.Log($"[YTTL] Unsupported site `{host}`");
                result = default;
                return false;
            }

            var hostData = hostDataToken.DataDictionary;

            var resultDict = new DataDictionary();

            for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++) {
                if (!labels.TryGetValue(labelIndex, TokenType.String, out var label)) {
                    Debug.LogWarning($"[YTTL] Unable to retrieve label `{label}`");
                    continue;
                }

                if (!hostData.TryGetValue(label, TokenType.DataDictionary, out var labelDataToken)) {
                    Debug.LogWarning($"[YTTL] Unsupported label `{label}`");
                    continue;
                }

                var labelData = labelDataToken.DataDictionary;

                var currentIndex = 0;

                var isSucceed = true;

                if (labelData.TryGetValue("middle", TokenType.DataList, out var middleTagsToken)) {
                    var middleTags = middleTagsToken.DataList;

                    for (var tagIndex = 0; tagIndex < middleTags.Count; tagIndex++) {
                        if (!middleTags.TryGetValue(tagIndex, TokenType.String, out var middleTagToken)) {
                            Debug.Log($"[YTTL] Unable to retrieve middle tag list `{tagIndex}`");
                            isSucceed = false;
                            break;
                        }

                        var middleTag = middleTagToken.String;

                        while (true) {
                            var middleTagIndex = RawDataText.IndexOf(middleTag, currentIndex);

                            if (middleTagIndex != -1) {
                                currentIndex = middleTagIndex + middleTag.Length;

                                if (currentIndex > 0 && RawDataText[currentIndex - 1] == '\\') {
                                    continue;
                                }

                                break;
                            } else {
                                Debug.Log($"[YTTL] Middle element not found in string `{middleTag}`");
                                isSucceed = false;
                                break;
                            }
                        }
                    }
                }

                if (!isSucceed) {
                    Debug.LogWarning($"[YTTL] Unsupported label `{label}`");
                    continue;
                }

                if (!labelData.TryGetValue("end", TokenType.DataDictionary, out var endTagsToken)) {
                    Debug.LogWarning($"[YTTL] Unable to retrieve final tag list `{label}`");
                    continue;
                }

                var endTags = endTagsToken.DataDictionary;

                if (!endTags.TryGetValue("s", TokenType.String, out var sTagToken)) {
                    Debug.LogWarning($"[YTTL] Unable to retrieve start tag `{label}`");
                    continue;
                }

                var sTag = sTagToken.String;

                int sIndex;

                while (true) {
                    sIndex = RawDataText.IndexOf(sTag, currentIndex);

                    if (sIndex == -1) {
                        Debug.LogWarning($"[YTTL] Start tag not found `{sTag}`");
                        isSucceed = false;
                        break;
                    } else if (sIndex != 0 && RawDataText[sIndex - 1] == '\\') {
                        currentIndex = sIndex + 1;
                        continue;
                    } else {
                        sIndex += sTag.Length;
                        currentIndex = sIndex;
                        break;
                    }
                }

                if (!isSucceed) {
                    Debug.LogWarning($"[YTTL] Unsupported label `{label}`");
                    continue;
                }

                if (!endTags.TryGetValue("t", TokenType.String, out var tTagToken)) {
                    Debug.LogWarning($"[YTTL] Unable to retrieve end tag `{label}`");
                    continue;
                }

                var tTag = tTagToken.String;

                int tIndex;

                while (true) {
                    tIndex = RawDataText.IndexOf(tTag, currentIndex);

                    if (tIndex == -1) {
                        Debug.LogWarning($"[YTTL] End tag not found `{tTag}`");
                        isSucceed = false;
                        break;
                    } else if (tIndex != 0 && RawDataText[tIndex - 1] == '\\') {
                        currentIndex = tIndex + 1;
                        continue;
                    } else {
                        break;
                    }
                }

                if (!isSucceed) {
                    Debug.LogWarning($"[YTTL] Unsupported label `{label}`");
                    continue;
                }

                var resultStr = RawDataText.Substring(sIndex, tIndex - sIndex);

                if (VRCJson.TryDeserializeFromJson($@"[""{resultStr}""]", out var resultToken)) {
                    resultStr = resultToken.DataList[0].String;
                }

                resultDict[label] = resultStr;
            }

            result = resultDict;
            return true;
        }
    }
}
