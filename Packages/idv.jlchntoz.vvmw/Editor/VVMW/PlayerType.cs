namespace JLChnToZ.VRC.VVMW.Editors {
    public enum PlayerType : byte {
        Unknown,
        Unity,
        AVPro,
        Image,
    }

    public static class PlayerTypeExtensions {
        public static TrustedUrlTypes ToTrustUrlType(this PlayerType playerType, bool isAndroid) {
            switch (playerType) {
                case PlayerType.AVPro:
                    return isAndroid ? TrustedUrlTypes.AVProAndroid : TrustedUrlTypes.AVProDesktop;
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
