using System;

namespace JLChnToZ.VRC.VVMW {
    public partial class UIHandler {
        public void _OnSeek() {
            core.Progress = progressSlider.value;
        }

        public void _UpdateProgress() {
            if (!core.IsPlaying) {
                hasUpdate = false;
                return;
            }
            UpdateProgressOnce();
            SendCustomEventDelayedSeconds(nameof(_UpdateProgress), 0.25F);
        }

        void UpdateProgressOnce() {
            var duration = core.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) {
                SetStatusEnabled(true);
                SetLocalizedText(timeText, timeTMPro, "TimeIdleFormat");
                SetLocalizedText(durationText, durationTMPro, "TimeIdleFormat");
                if (statusText != null || statusTMPro != null) {
                    if (!string.IsNullOrEmpty(core.title) || !string.IsNullOrEmpty(core.author))
                        SetText(statusText, statusTMPro, string.Format(languageManager.GetLocale("StreamingWithTitle"), core.title, core.author));
                    else if (core.IsStatic)
                        SetLocalizedText(statusText, statusTMPro, "DisplayStatic");
                    else
                        SetLocalizedText(statusText, statusTMPro, "Streaming");
                }
                if (progressSlider != null) {
                    progressSlider.SetValueWithoutNotify(1);
                    progressSlider.interactable = false;
                }
            } else {
                SetStatusEnabled(false);
                var time = TimeSpan.FromSeconds(core.Time);
                var durationTS = TimeSpan.FromSeconds(duration);
                SetText(durationText, durationTMPro, string.Format(languageManager.GetLocale("TimeFormat"), durationTS));
                SetText(timeText, timeTMPro, string.Format(languageManager.GetLocale("TimeFormat"), time));
                if (core.IsPaused)
                    SetText(statusText, statusTMPro, string.Format(languageManager.GetLocale("Paused"), time, durationTS));
                else if (!string.IsNullOrEmpty(core.title) || !string.IsNullOrEmpty(core.author))
                    SetText(statusText, statusTMPro, string.Format(languageManager.GetLocale("PlayingWithTitle"), time, durationTS, core.title, core.author));
                else
                    SetText(statusText, statusTMPro, string.Format(languageManager.GetLocale("Playing"), time, durationTS));
                if (progressSlider != null) {
                    progressSlider.SetValueWithoutNotify(core.Progress);
                    progressSlider.interactable = true;
                }
            }
        }
    }
}