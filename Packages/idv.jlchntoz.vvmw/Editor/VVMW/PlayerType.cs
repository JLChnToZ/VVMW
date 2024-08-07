using UnityEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    public enum PlayerType : byte {
        Unknown,
        Unity,
        AVPro,
        Image,
    }

    public static class PlayerTypeExtensions {
        public static TrustedUrlTypes ToTrustUrlType(this PlayerType playerType, BuildTarget buildTarget = BuildTarget.NoTarget) {
            switch (playerType) {
                case PlayerType.AVPro:
                    switch (buildTarget) {
                        case BuildTarget.Android: return TrustedUrlTypes.AVProAndroid;
                        case BuildTarget.iOS: return TrustedUrlTypes.AVProIOS;
                        default: return TrustedUrlTypes.AVProDesktop;
                    }
                case PlayerType.Image:
                    return TrustedUrlTypes.ImageUrl;
                default:
                    return TrustedUrlTypes.UnityVideo;
            }
        }

        public static PlayerType GetPlayerType(this AbstractMediaPlayerHandler handler) {
            if (handler is VideoPlayerHandler videoPlayerHandler)
                return videoPlayerHandler.IsAvPro ? PlayerType.AVPro : PlayerType.Unity;
            if (handler is ImageViewerHandler)
                return PlayerType.Image;
            return PlayerType.Unknown;
        }
    }
}
