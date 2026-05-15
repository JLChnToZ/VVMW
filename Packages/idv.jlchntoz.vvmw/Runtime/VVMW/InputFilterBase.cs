using System;
using VRC.SDKBase;
using UdonSharp;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// Base class for URL input filters.
    /// </summary>
    /// <remarks>
    /// Implement this class to filter/replace URLs before they are loaded by the video player.
    /// </remarks>
    public abstract class InputFilterBase : UdonSharpBehaviour {
        /// <summary>
        /// The URL (PC) to be filtered.
        /// </summary>
        /// <remarks>
        /// This will be the requested URL from the user before the video player loads it.
        /// To reject the URL, set this to <see cref="VRCUrl.Empty"/>.
        /// </remarks>
        [NonSerialized] public VRCUrl pcUrl;

        /// <summary>
        /// The URL (Quest) to be filtered.
        /// </summary>
        /// <remarks>
        /// This will be the requested URL from the user before the video player loads it.
        /// To reject the URL, set this to <see cref="VRCUrl.Empty"/>.
        /// </remarks>
        [NonSerialized] public VRCUrl questUrl;

        /// <summary>
        /// This will be called when the user requests a URL.
        /// </summary>
        /// <remarks>
        /// Implement your filtering logic here.
        /// </remarks>
        public abstract void _ValidateUrls();
    }
}