using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    public partial class UIHandler {
        void InitSpeedAndShiftControl() {
            bool isSynced = core.IsSynced;
            if (Utilities.IsValid(shiftControlsRoot)) shiftControlsRoot.SetActive(isSynced);
            else {
                if (Utilities.IsValid(shiftBackLButtonObject)) shiftBackLButtonObject.SetActive(isSynced);
                if (Utilities.IsValid(shiftBackSButtonObject)) shiftBackSButtonObject.SetActive(isSynced);
                if (Utilities.IsValid(shiftForwardSButtonObject)) shiftForwardSButtonObject.SetActive(isSynced);
                if (Utilities.IsValid(shiftForwardLButtonObject)) shiftForwardLButtonObject.SetActive(isSynced);
                if (Utilities.IsValid(shiftResetButtonObject)) shiftResetButtonObject.SetActive(isSynced);
                if (Utilities.IsValid(shiftOffsetObject)) shiftOffsetObject.SetActive(isSynced);
            }
            if (Utilities.IsValid(speedControlsRoot)) speedControlsRoot.SetActive(wasUnlocked);
            else {
                if (Utilities.IsValid(speedDownLButtonObject)) speedDownLButtonObject.SetActive(wasUnlocked);
                if (Utilities.IsValid(speedDownSButtonObject)) speedDownSButtonObject.SetActive(wasUnlocked);
                if (Utilities.IsValid(speedUpSButtonObject)) speedUpSButtonObject.SetActive(wasUnlocked);
                if (Utilities.IsValid(speedUpLButtonObject)) speedUpLButtonObject.SetActive(wasUnlocked);
                if (Utilities.IsValid(speedResetButtonObject)) speedResetButtonObject.SetActive(wasUnlocked);
                if (Utilities.IsValid(speedOffsetObject)) speedOffsetObject.SetActive(wasUnlocked);
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

#if COMPILER_UDONSHARP
        public
#endif
        void _PerformanceModeToggle() {
            var performer = core.Performer;
            core.SetOwnPerformer(!Utilities.IsValid(performer) || !performer.isLocal);
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnPerformerChange() {
            var performer = core.Performer;
            bool isOff = false, isSelf = false, isOthers = false;
            if (Utilities.IsValid(performer)) {
                if (performer.isLocal)
                    isSelf = true;
                else
                    isOthers = true;
                SetText(performerText, performerTMPro, performer.displayName);
            } else {
                isOff = true;
                SetText(performerText, performerTMPro, "");
            }
            if (Utilities.IsValid(performanceModeOff)) performanceModeOff.SetActive(isOff);
            if (Utilities.IsValid(performanceModeSelf)) performanceModeSelf.SetActive(isSelf);
            if (Utilities.IsValid(performanceModeOthers)) performanceModeOthers.SetActive(isOthers);
        }
    }
}