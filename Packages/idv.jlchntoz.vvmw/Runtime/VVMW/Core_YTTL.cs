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
        [NonSerialized, FieldChangeCallback(nameof(URL))]
        public VRCUrl url = VRCUrl.Empty;
        [NonSerialized, FieldChangeCallback(nameof(Author))]
        public string author = "";
        [NonSerialized, FieldChangeCallback(nameof(Title))]
        public string title = "";
        [NonSerialized, FieldChangeCallback(nameof(ViewCount))]
        public string viewCount = "";
        [NonSerialized, FieldChangeCallback(nameof(Description))]
        public string description = "";
        bool hasCustomTitle;
        
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

        public void Yttl_OnDataLoaded() => SendEvent("_OnTitleData");

        public void SetTitle(string title, string author) {
            hasCustomTitle = true;
            url = VRCUrl.Empty;
            this.title = title;
            this.author = author;
            description = "";
            viewCount = "";
            SendEvent("_OnTitleData");
        }

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
