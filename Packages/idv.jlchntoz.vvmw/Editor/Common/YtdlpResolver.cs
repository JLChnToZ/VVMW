using System.IO;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using VVMW.ThirdParties.LitJson;
using System.Collections.Generic;

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
            if (request.isNetworkError) {
                if (File.Exists(path)) File.Delete(path);
            } else {
                File.Move(path, ytdlpPath);
            }
            EditorUtility.ClearProgressBar();
        }

        public static async UniTask<List<YtdlpPlayListEntry>> GetPlayLists(string url) {
            var results = new List<YtdlpPlayListEntry>();
            await DownLoadYtDlpIfNotExists();
            if (!HasYtDlp()) return results;
            try {
                EditorUtility.DisplayProgressBar("Getting Playlists", "Getting Playlists", 0);
                var startInfo = new ProcessStartInfo(ytdlpPath, $"--flat-playlist --no-write-playlist-metafiles --skip-download --no-exec -ijo - {url}") {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                var process = Process.Start(startInfo);
                var output = await process.StandardError.ReadToEndAsync();
                foreach (var line in output.Split('\n')) {
                    if (!line.StartsWith("{")) continue;
                    try {
                        var json = JsonMapper.ToObject(line);
                        results.Add(new YtdlpPlayListEntry {
                            title = json["title"].ToString(),
                            url = json["url"].ToString()
                        });
                    } catch { }
                }
            } finally {
                EditorUtility.ClearProgressBar();
            }
            return results;
        }
    }

    public struct YtdlpPlayListEntry {
        public string title;
        public string url;
    }
}