using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;
using Cysharp.Threading.Tasks;
using VVMW.ThirdParties.LitJson;

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
        static string ytdlpPath = "";
        static bool hasYtdlp = false;

        public static bool HasYtDlp() {
            if (hasYtdlp) return true;
            if (string.IsNullOrEmpty(ytdlpPath))
                ytdlpPath = Path.Combine(Application.persistentDataPath, "yt-dlp.exe");
            hasYtdlp = File.Exists(ytdlpPath);
            return hasYtdlp;
        }

        public static async UniTask DownLoadYtDlpIfNotExists() {
            if (HasYtDlp()) return;
            if (!EditorUtility.DisplayDialog("Download yt-dlp", $"yt-dlp not found, do you want to download it from {YTDLP_DOWNLOAD_PATH}?", "Yes", "No"))
                return;
            var request = new UnityWebRequest(YTDLP_DOWNLOAD_PATH, "GET");
            var path = Path.GetTempFileName();
            var handler = new DownloadHandlerFile(path) { removeFileOnAbort = true };
            request.downloadHandler = handler;
            _ = request.SendWebRequest();
            while (!request.isDone) {
                await UniTask.Yield();
                if (EditorUtility.DisplayCancelableProgressBar("Downloading", "Downloading yt-dlp", request.downloadProgress)) {
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
                File.Move(path, ytdlpPath);
                #if !UNITY_EDITOR_WIN
                var process = Process.Start(new ProcessStartInfo("chmod", $"+x {ytdlpPath}") {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });
                await process.WaitForExitAsync();
                #endif
            }
            EditorUtility.ClearProgressBar();
        }

        public static async UniTask<List<YtdlpPlayListEntry>> GetPlayLists(string url) {
            await DownLoadYtDlpIfNotExists();
            if (!HasYtDlp()) return new List<YtdlpPlayListEntry>();
            List<YtdlpPlayListEntry> results = null;
            try {
                EditorUtility.DisplayProgressBar("Getting Playlists", "Getting Playlists", 0);
                results = await Fetch(url);
            } finally {
                EditorUtility.ClearProgressBar();
            }
            return results;
        }

        public static async UniTask FetchTitles(YtdlpPlayListEntry[] entries) {
            await DownLoadYtDlpIfNotExists();
            if (!HasYtDlp()) return;
            try {
                EditorUtility.DisplayProgressBar("Getting Titles", "Getting Titles", 0);
                for (int i = 0; i < entries.Length; i++) {
                    var entry = entries[i];
                    if (!string.IsNullOrEmpty(entry.title) ||
                        string.IsNullOrEmpty(entry.url)) continue;
                    var results = await Fetch(entry.url);
                    if (results.Count > 0) {
                        entry.title = results[0].title;
                        entry.url = results[0].url;
                    }
                    entries[i] = entry;
                    EditorUtility.DisplayProgressBar("Getting Titles", $"Getting Titles ({i + 1}/{entries.Length})", (float)(i + 1) / entries.Length);
                }
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }

        static async UniTask<List<YtdlpPlayListEntry>> Fetch(string url) {
            var results = new List<YtdlpPlayListEntry>();
            var startInfo = new ProcessStartInfo(ytdlpPath, $"--flat-playlist --no-write-playlist-metafiles --no-exec -sijo - {url}") {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var process = Process.Start(startInfo);
            var stderr = process.StandardError;
            while (!stderr.EndOfStream)
                try {
                    var line = await stderr.ReadLineAsync();
                    if (!line.StartsWith("{")) continue;
                    var json = JsonMapper.ToObject(line);
                    results.Add(new YtdlpPlayListEntry {
                        title = json["title"].ToString(),
                        url = json.ContainsKey("url") ? json["url"].ToString() : url
                    });
                } catch { }
            await UniTask.SwitchToMainThread();
            return results;
        }
    }

    public struct YtdlpPlayListEntry {
        public string title;
        public string url;
    }
}