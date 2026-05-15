using UdonSharp;
using UnityEngine;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// A base class that come with a nice branded inspector.
    /// </summary>
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/")]
    public abstract class VizVidBehaviour : UdonSharpBehaviour { }

    /// <summary>
    /// Indicates this component requires a VizVid Core.
    /// </summary>
    public interface IVizVidCompoonent {
        /// <summary>
        /// The VizVid Core.
        /// </summary>
        Core Core { get; }
    }
}