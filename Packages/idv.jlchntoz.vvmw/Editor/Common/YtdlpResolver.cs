using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Networking;
using UnityEditor;
using Cysharp.Threading.Tasks;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.Foundation.I18N.Editors;
using JLChnToZ.VRC.Foundation.ThirdParties.LitJson;

namespace JLChnToZ.VRC.VVMW.Editors {
    public static class YtdlpResolver {
        const string YTDLP_PREF_KEY = "VVMW_YTDLP_PATH";
        const string YTDLP_LOCALE_PREF_KEY = "VVMW_YTDLP_LOCALE";
        const string YTDLP_DOWNLOAD_PATH_BASE = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/";
#if UNITY_EDITOR_WIN
        const string YTDLP_DOWNLOAD_PATH = YTDLP_DOWNLOAD_PATH_BASE + "yt-dlp.exe";
#elif UNITY_EDITOR_OSX
        const string YTDLP_DOWNLOAD_PATH = YTDLP_DOWNLOAD_PATH_BASE + "yt-dlp_macos";
#elif UNITY_EDITOR_LINUX
        const string YTDLP_DOWNLOAD_PATH = YTDLP_DOWNLOAD_PATH_BASE + "yt-dlp_linux";
#endif
        static readonly IReadOnlyDictionary<string, string> specialLocalMappings = new Dictionary<string, string> {
            ["es-MX"] = "es-419",
            ["es-AR"] = "es-419",
            ["es-CL"] = "es-419",
            ["es-CO"] = "es-419",
            ["es-CR"] = "es-419",
            ["es-DO"] = "es-419",
            ["es-EC"] = "es-419",
            ["es-SV"] = "es-419",
            ["es-GT"] = "es-419",
            ["es-HN"] = "es-419",
            ["es-NI"] = "es-419",
            ["es-PA"] = "es-419",
            ["es-PY"] = "es-419",
            ["es-PE"] = "es-419",
            ["es-PR"] = "es-419",
            ["es-UY"] = "es-419",
            ["es-VE"] = "es-419",
            ["zh-CHS"] = "zh-CN",
            ["zh-Hans"] = "zh-CN",
            ["zh-SG"] = "zh-CN",
            ["zh-MY"] = "zh-CN",
            ["zh-CHT"] = "zh-CN",
            ["zh-Hant"] = "zh-TW",
            ["zh-MO"] = "zh-HK",
        };
        static string ytdlpPath;
        static Dictionary<string, string> locales;
        static string selectedLocale;
        static bool hasYtdlp = false;
        static bool hasCheckedYtdlp = false;

        public static string YtdlpPath {
            get {
                if (!string.IsNullOrEmpty(ytdlpPath))
                    return ytdlpPath;
                if (EditorPrefs.HasKey(YTDLP_PREF_KEY)) {
                    var path = EditorPrefs.GetString(YTDLP_PREF_KEY);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        ytdlpPath = path;
                }
                if (string.IsNullOrEmpty(ytdlpPath)) {
                    ytdlpPath = Path.Combine(Application.persistentDataPath, "yt-dlp.exe");
                    EditorPrefs.SetString(YTDLP_PREF_KEY, ytdlpPath);
                }
                return ytdlpPath;
            }
            set {
                if (string.IsNullOrEmpty(value) || !File.Exists(value)) return;
                ytdlpPath = value;
                EditorPrefs.SetString("VVMW_YTDLP_PATH", ytdlpPath);
            }
        }

        public static string SelectedLocale {
            get {
                if (!string.IsNullOrEmpty(selectedLocale))
                    return selectedLocale;
                if (EditorPrefs.HasKey(YTDLP_LOCALE_PREF_KEY)) {
                    var locale = EditorPrefs.GetString(YTDLP_LOCALE_PREF_KEY);
                    if (!string.IsNullOrEmpty(locale) && locales.ContainsKey(locale))
                        selectedLocale = locale;
                }
                if (string.IsNullOrEmpty(selectedLocale)) {
                    selectedLocale = GetDefaultLocaleCode();
                    EditorPrefs.SetString(YTDLP_LOCALE_PREF_KEY, selectedLocale);
                }
                return selectedLocale;
            }
            set {
                if (string.IsNullOrEmpty(value) || !locales.ContainsKey(value)) return;
                selectedLocale = value;
                EditorPrefs.SetString(YTDLP_LOCALE_PREF_KEY, selectedLocale);
            }
        }

        public static Dictionary<string, string> AvailableLocales {
            get {
                LoadLocales();
                return locales;
            }
        }

        public static bool HasYtDlp() {
            if (hasYtdlp) return true;
            hasYtdlp = File.Exists(YtdlpPath);
            return hasYtdlp;
        }

        public static UniTask DownLoadYtDlpIfNotExists() {
            var hasYtdlp = HasYtDlp();
            if (hasYtdlp && hasCheckedYtdlp) return UniTask.CompletedTask;
            if (!EditorI18N.Instance.DisplayLocalizedDialog2("YTDLPResolver.download_confirm")) {
                if (hasYtdlp) hasCheckedYtdlp = true;
                return UniTask.CompletedTask;
            }
            hasCheckedYtdlp = true;
            return DownLoadYtDlp();
        }

        public static async UniTask DownLoadYtDlp() {
            var request = new UnityWebRequest(YTDLP_DOWNLOAD_PATH, "GET");
            var path = Path.GetTempFileName();
            var handler = new DownloadHandlerFile(path) { removeFileOnAbort = true };
            request.downloadHandler = handler;
            _ = request.SendWebRequest();
            var i18n = EditorI18N.Instance;
            while (!request.isDone) {
                await UniTask.Yield();
                if (EditorUtility.DisplayCancelableProgressBar(i18n["YTDLPResolver.download_progress:title"], i18n["YTDLPResolver.download_progress:content"], request.downloadProgress)) {
                    request.Abort();
                    EditorUtility.ClearProgressBar();
                    return;
                }
            }
            if (
#if UNITY_2020_1_OR_NEWER
                request.result != UnityWebRequest.Result.Success
#else
                request.isNetworkError || request.isHttpError
#endif
            ) {
                if (File.Exists(path)) File.Delete(path);
            } else {
                var ytdlpPath = YtdlpPath;
                if (File.Exists(ytdlpPath)) File.Delete(ytdlpPath);
                File.Move(path, ytdlpPath);
#if !UNITY_EDITOR_WIN
                var process = Process.Start(new ProcessStartInfo("chmod", $"+x {ytdlpPath}") {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                process.WaitForExit();
#endif
            }
            EditorUtility.ClearProgressBar();
        }

        static void LoadLocales() {
            if (locales != null && locales.Count > 0) return;
            var file = File.ReadAllText("Packages/idv.jlchntoz.vvmw/Resources/ytdlp-regions.json");
            var reader = new JsonReader(file);
            var json = JsonMapper.ToObject(reader);
            locales = new Dictionary<string, string>();
            foreach (var key in json.Keys)
                locales[key] = json[key].ToString();
        }

        public static async UniTask<List<YtdlpPlayListEntry>> GetPlayLists(string url) {
            await DownLoadYtDlpIfNotExists();
            if (!HasYtDlp()) return new List<YtdlpPlayListEntry>();
            var text = EditorI18N.Instance["YTDLPResolver.get_playlists"];
            using var progress = new CancelableProgressBar(text, text);
            return await Fetch(url, progress);
        }

        public static async UniTask FetchTitles(YtdlpPlayListEntry[] entries) {
            await DownLoadYtDlpIfNotExists();
            if (!HasYtDlp()) return;
            var text = EditorI18N.Instance["YTDLPResolver.get_titles"];
            using var progress = new CancelableProgressBar(text, text);
            var token = progress.CancelToken;
            if (token.IsCancellationRequested) return;
            for (int i = 0; i < entries.Length; i++) {
                progress.Info = $"{text} ({i + 1}/{entries.Length})";
                var entry = entries[i];
                if (!string.IsNullOrEmpty(entry.title) ||
                    string.IsNullOrEmpty(entry.url) ||
                    token.IsCancellationRequested) continue;
                var results = await Fetch(entry.url, progress, (float)i / entries.Length, (float)(i + 1) / entries.Length);
                if (results.Count > 0) {
                    entry.title = results[0].title;
                    entry.url = results[0].url;
                }
                entries[i] = entry;
            }
        }

        static async UniTask<List<YtdlpPlayListEntry>> Fetch(string url, CancelableProgressBar progress, float startProgress = 0F, float endProgress = 1F) {
            var cancelToken = progress.CancelToken;
            var orgInfo = progress.Info;
            var results = new List<YtdlpPlayListEntry>();
            if (cancelToken.IsCancellationRequested) return results;
            string args;
            using (ListPool<string>.Get(out var argsList)) {
                argsList.Add("--flat-playlist");
                argsList.Add("--no-write-playlist-metafiles");
                argsList.Add("--no-exec");
                if (!string.IsNullOrEmpty(selectedLocale)) {
                    argsList.Add("--extractor-args");
                    argsList.Add($"\"youtube:lang={selectedLocale}\"");
                }
                argsList.Add("-sijo");
                argsList.Add("-");
                argsList.Add(url);
                args = string.Join(" ", argsList);
            }
            var startInfo = new ProcessStartInfo(YtdlpPath, args) {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var process = Process.Start(startInfo);
            var stderr = process.StandardError;
            while (!stderr.EndOfStream)
                try {
                    cancelToken.ThrowIfCancellationRequested();
                    var line = await stderr.ReadLineAsync().AsUniTask().AttachExternalCancellation(cancelToken);
                    if (!line.StartsWith("{")) continue;
                    var json = JsonMapper.ToObject(line);
                    results.Add(new YtdlpPlayListEntry {
                        title = json["title"].ToString(),
                        url = json.ContainsKey("url") ? json["url"].ToString() : url
                    });
                    var index = json.ContainsKey("playlist_index") ? (int)json["playlist_index"] : 1;
                    var count = json.ContainsKey("n_entries") ? (int)json["n_entries"] : 1;
                    progress.Report(Mathf.Lerp(startProgress, endProgress, (float)index / count));
                    progress.Info = $"{orgInfo} ({index}/{count})";
                } catch (OperationCanceledException) {
                    try {
                        if (!process.HasExited) process.Kill();
                    } catch { }
                    break;
                } catch { }
            await UniTask.SwitchToMainThread();
            return results;
        }

        static string GetDefaultLocaleCode() {
            LoadLocales();
            var culture = CultureInfo.CurrentUICulture;
            while (culture != CultureInfo.InvariantCulture) {
                var langName = culture.Name;
                if (locales.ContainsKey(langName)) return langName;
                if (specialLocalMappings.TryGetValue(langName, out var mapped) &&
                    locales.ContainsKey(mapped))
                    return mapped;
                culture = culture.Parent;
            }
            return "en";
        }
    }

    public struct YtdlpPlayListEntry {
        public string title;
        public string url;
    }

    class CancelableProgressBar : IDisposable, IProgress<float> {
        readonly CancellationTokenSource cts;
        string title, info;
        float progress;

        public string Title {
            get => title;
            set {
                title = value;
                Report();
            }
        }

        public string Info {
            get => info;
            set {
                info = value;
                Report();
            }
        }

        public CancellationToken CancelToken => cts.Token;

        public CancelableProgressBar(string title, string info, float initialProgress = 0) {
            cts = new CancellationTokenSource();
            this.title = title;
            this.info = info;
            Report(initialProgress);
        }

        public void Report(float value) {
            progress = value;
            Report();
        }

        void Report() {
            if (EditorUtility.DisplayCancelableProgressBar(title, info, progress))
                cts.Cancel();
        }

        public void Dispose() {
            EditorUtility.ClearProgressBar();
            cts.Dispose();
        }
    }
}