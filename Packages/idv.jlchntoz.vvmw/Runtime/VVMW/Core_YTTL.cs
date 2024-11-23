using System;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
using VVMW.ThirdParties.Yttl;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/Prefabs/Third-Parties/YTTL/YTTL Manager.prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.First
        ), SerializeField, LocalizedLabel] YttlManager yttl;
#if COMPILER_UDONSHARP
        [NonSerialized, FieldChangeCallback(nameof(URL))] public
#endif
        VRCUrl url = VRCUrl.Empty;
        /// <summary>
        /// The author of the video. This may be custom assigned or fetched from the video.
        /// Directly setting this value is unsupported, use <see cref="SetTitle"/> instead.
        /// </summary>
        [NonSerialized, FieldChangeCallback(nameof(Author))]
        public string author = "";
        /// <summary>
        /// The title of the video. This may be custom assigned or fetched from the video.
        /// Directly setting this value is unsupported, use <see cref="SetTitle"/> instead.
        /// </summary>
        [NonSerialized, FieldChangeCallback(nameof(Title))]
        public string title = "";
        /// <summary>
        /// View count of the video. This is fetched from the video.
        /// Directly setting this value is unsupported.
        /// </summary>
        [NonSerialized, FieldChangeCallback(nameof(ViewCount))]
        public string viewCount = "";
        /// <summary>
        /// Description of the video. This is fetched from the video.
        /// Directly setting this value is unsupported.
        /// </summary>
        [NonSerialized, FieldChangeCallback(nameof(Description))]
        public string description = "";
        bool hasCustomTitle;

        VRCUrl URL {
            get => url;
            set => url = value ?? VRCUrl.Empty;
        }
        
        string Title {
            get => title;
            set {
                if (hasCustomTitle || !url.Equals(localUrl)) return;
                title = value;
            }
        }

        string Author {
            get => author;
            set {
                if (hasCustomTitle || !url.Equals(localUrl)) return;
                author = value;
            }
        }

        string ViewCount {
            get => viewCount;
            set {
                if (hasCustomTitle || !url.Equals(localUrl)) return;
                viewCount = value;
            }
        }

        string Description {
            get => description;
            set {
                if (hasCustomTitle || !url.Equals(localUrl)) return;
                description = value;
            }
        }

#if COMPILER_UDONSHARP
        public void Yttl_OnDataLoaded() => SendEvent("_OnTitleData");
#endif

        /// <summary>
        /// Sets the title and author of the video to be displayed.
        /// </summary>
        /// <param name="title">The title of the video.</param>
        /// <param name="author">The author of the video.</param>
        public void SetTitle(string title, string author) {
            hasCustomTitle = true;
            url = VRCUrl.Empty;
            this.title = title;
            this.author = author;
            description = "";
            viewCount = "";
            SendEvent("_OnTitleData");
        }

        /// <summary>
        /// Clears the custom displayed title and author of the video.
        /// </summary>
        public void _ResetTitle() {
            if (!hasCustomTitle) return;
            hasCustomTitle = false;
            url = VRCUrl.Empty;
            if (!Utilities.IsValid(yttl)) {
                author = "";
                title = "";
                viewCount = "";
                description = "";
            } else
                LoadYTTL();
            SendEvent("_OnTitleData");
        }

        void LoadYTTL() {
            if (!Utilities.IsValid(yttl) || hasCustomTitle || url.Equals(localUrl)) return;
            author = "";
            title = "";
            viewCount = "";
            description = "";
            if (Utilities.IsValid(localUrl))
                yttl.LoadData(localUrl, this);
        }
    }
}
