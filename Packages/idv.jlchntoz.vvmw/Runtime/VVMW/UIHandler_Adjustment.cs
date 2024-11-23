using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    public partial class UIHandler {
        void InitShiftControl() {
            bool isSynced = core.IsSynced;
            if (Utilities.IsValid(shiftControlsRoot)) shiftControlsRoot.SetActive(isSynced);
            else {
                if (Utilities.IsValid(shiftBackLButton)) shiftBackLButton.gameObject.SetActive(isSynced);
                if (Utilities.IsValid(shiftBackSButton)) shiftBackSButton.gameObject.SetActive(isSynced);
                if (Utilities.IsValid(shiftForwardSButton)) shiftForwardSButton.gameObject.SetActive(isSynced);
                if (Utilities.IsValid(shiftForwardLButton)) shiftForwardLButton.gameObject.SetActive(isSynced);
                if (Utilities.IsValid(shiftResetButton)) shiftResetButton.gameObject.SetActive(isSynced);
                if (Utilities.IsValid(shiftOffsetText)) shiftOffsetText.gameObject.SetActive(isSynced);
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _ShiftBackL() => core.SyncOffset -= 0.1F;

#if COMPILER_UDONSHARP
        public
#endif
        void _ShiftBackS() => core.SyncOffset -= 0.05F;

#if COMPILER_UDONSHARP
        public
#endif
        void _ShiftForwardS() => core.SyncOffset += 0.05F;

#if COMPILER_UDONSHARP
        public
#endif
        void _ShiftForwardL() => core.SyncOffset += 0.1F;

#if COMPILER_UDONSHARP
        public
#endif
        void _ShiftReset() => core.SyncOffset = 0;

#if COMPILER_UDONSHARP
        public
#endif
        void _OnSyncOffsetChange() {
            if (!afterFirstRun) return;
            SetText(shiftOffsetText, shiftOffsetTMPro, string.Format(languageManager.GetLocale("TimeDrift"), core.SyncOffset));
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _SpeedDownL() => core.Speed -= 0.25F;

#if COMPILER_UDONSHARP
        public
#endif
        void _SpeedDownS() => core.Speed -= 0.1F;

#if COMPILER_UDONSHARP
        public
#endif
        void _SpeedUpS() => core.Speed += 0.1F;

#if COMPILER_UDONSHARP
        public
#endif
        void _SpeedUpL() => core.Speed += 0.25F;

#if COMPILER_UDONSHARP
        public
#endif
        void _SpeedReset() => core.Speed = 1;

#if COMPILER_UDONSHARP
        public
#endif
        void _OnSpeedChange() {
            if (!afterFirstRun) return;
            SetText(speedOffsetText, speedOffsetTMPro, string.Format(languageManager.GetLocale("SpeedOffset"), core.Speed));
        }
    }
}