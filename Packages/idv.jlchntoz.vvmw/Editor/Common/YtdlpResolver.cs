using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using Cysharp.Threading.Tasks;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.Foundation.I18N.Editors;
using JLChnToZ.VRC.Foundation.ThirdParties.LitJson;

namespace JLChnToZ.VRC.VVMW.Editors {
    public static class YtdlpResolver {
        const string YTDLP_DOWNLOAD_PATH_BASE = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/";
#if UNITY_EDITOR_WIN
        const string YTDLP_DOWNLOAD_PATH = YTDLP_DOWNLOAD_PATH_BASE + "yt-dlp.exe";
#elif UNITY_EDITOR_OSX
        const string YTDLP_DOWNLOAD_PATH = YTDLP_DOWNLOAD_PATH_BASE + "yt-dlp_macos";
#elif UNITY_EDITOR_LINUX
        const string YTDLP_DOWNLOAD_PATH = YTDLP_DOWNLOAD_PATH_BASE + "yt-dlp_linux";
#endif
        static string ytdlpPath;
        static bool hasYtdlp = false;
        static bool hasCheckedYtdlp = false;

        static string YtdlpPath {
            get {
                if (string.IsNullOrEmpty(ytdlpPath))
                    ytdlpPath = Path.Combine(Application.persistentDataPath, "yt-dlp.exe");
                return ytdlpPath;
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

        public static async UniTask<List<YtdlpPlayListEntry>> GetPlayLists(string url) {
            await DownLoadYtDlpIfNotExists();
            if (!HasYtDlp()) return new List<YtdlpPlayListEntry>();
            var text = EditorI18N.Instance["YTDLPResolver.get_playlists"];
            using var progress = new CancellableProgressBar(text, text);
            return await Fetch(url, progress);
        }

        public static async UniTask FetchTitles(YtdlpPlayListEntry[] entries) {
            await DownLoadYtDlpIfNotExists();
            if (!HasYtDlp()) return;
            var text = EditorI18N.Instance["YTDLPResolver.get_titles"];
            using var progress = new CancellableProgressBar(text, text);
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

        static async UniTask<List<YtdlpPlayListEntry>> Fetch(string url, CancellableProgressBar progress, float startProgress = 0F, float endProgress = 1F) {
            var cancelToken = progress.CancelToken;
            var orgInfo = progress.Info;
            var results = new List<YtdlpPlayListEntry>();
            if (cancelToken.IsCancellationRequested) return results;
            var startInfo = new ProcessStartInfo(YtdlpPath, $"--flat-playlist --no-write-playlist-metafiles --no-exec -sijo - {url}") {
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
    }

    public struct YtdlpPlayListEntry {
        public string title;
        public string url;
    }

    class CancellableProgressBar : IDisposable, IProgress<float> {
        readonly CancellationTokenSource cts;
        string title;
        string info;
        float progress;

        public string Title {
            get => title;
            set {
                title = value;
                Report(progress);
            }
        }

        public string Info {
            get => info;
            set {
                info = value;
                Report(progress);
            }
        }

        public CancellationToken CancelToken => cts.Token;

        public CancellableProgressBar(string title, string info, float initialProgress = 0) {
            cts = new CancellationTokenSource();
            this.title = title;
            this.info = info;
            Report(initialProgress);
        }

        public void Report(float value) {
            progress = value;
            if (EditorUtility.DisplayCancelableProgressBar(title, info, value))
                cts.Cancel();
        }

        public void Dispose() {
            EditorUtility.ClearProgressBar();
            cts.Dispose();
        }
    }
}