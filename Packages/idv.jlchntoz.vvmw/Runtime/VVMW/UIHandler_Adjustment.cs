namespace JLChnToZ.VRC.VVMW {
    public partial class UIHandler {
        void InitShiftControl() {
            bool isSynced = core.IsSynced;
            if (shiftControlsRoot != null) shiftControlsRoot.SetActive(isSynced);
            else {
                if (shiftBackLButton != null) shiftBackLButton.gameObject.SetActive(isSynced);
                if (shiftBackSButton != null) shiftBackSButton.gameObject.SetActive(isSynced);
                if (shiftForwardSButton != null) shiftForwardSButton.gameObject.SetActive(isSynced);
                if (shiftForwardLButton != null) shiftForwardLButton.gameObject.SetActive(isSynced);
                if (shiftResetButton != null) shiftResetButton.gameObject.SetActive(isSynced);
                if (shiftOffsetText != null) shiftOffsetText.gameObject.SetActive(isSynced);
            }
        }

        public void _ShiftBackL() => core.SyncOffset -= 0.1F;

        public void _ShiftBackS() => core.SyncOffset -= 0.05F;

        public void _ShiftForwardS() => core.SyncOffset += 0.05F;

        public void _ShiftForwardL() => core.SyncOffset += 0.1F;

        public void _ShiftReset() => core.SyncOffset = 0;

        public void _OnSyncOffsetChange() {
            if (!afterFirstRun) return;
            SetText(shiftOffsetText, shiftOffsetTMPro, string.Format(languageManager.GetLocale("TimeDrift"), core.SyncOffset));
        }

        public void _SpeedDownL() => core.Speed -= 0.25F;

        public void _SpeedDownS() => core.Speed -= 0.1F;

        public void _SpeedUpS() => core.Speed += 0.1F;

        public void _SpeedUpL() => core.Speed += 0.25F;

        public void _SpeedReset() => core.Speed = 1;

        public void _OnSpeedChange() {
            if (!afterFirstRun) return;
            SetText(speedOffsetText, speedOffsetTMPro, string.Format(languageManager.GetLocale("SpeedOffset"), core.Speed));
        }
    }
}