using System.Collections;
using System.Collections.Generic;
using JLChnToZ.VRC.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace JLChnToZ.VRC.VVMW.Designer {
    [EditorOnly]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HideIfIdleTextureAvailable : BaseMeshEffect {
        bool shouldHide;
        ScreenConfigurator screenConfigurator;

        internal bool ShouldHide {
            get => shouldHide;
            set {
                if (shouldHide == value) return;
                shouldHide = value;
                graphic.SetVerticesDirty();
            }
        }

        protected override void OnEnable() {
            screenConfigurator = GetComponentInParent<ScreenConfigurator>(true);
            if (screenConfigurator != null) screenConfigurator.RegisterHideIfIdleTextureAvailable(this);
            base.OnEnable();
        }

        protected override void OnDisable() {
            if (screenConfigurator != null) screenConfigurator.UnregisterHideIfIdleTextureAvailable(this);
            screenConfigurator = null;
            base.OnDisable();
        }

        public override void ModifyMesh(VertexHelper vh) {
            if (shouldHide) vh.Clear();
        }

        protected override void OnTransformParentChanged() {
            var newScreenConfigurator = GetComponentInParent<ScreenConfigurator>(true);
            if (newScreenConfigurator == screenConfigurator) return;
            if (screenConfigurator != null) screenConfigurator.UnregisterHideIfIdleTextureAvailable(this);
            screenConfigurator = newScreenConfigurator;
            if (screenConfigurator != null) screenConfigurator.RegisterHideIfIdleTextureAvailable(this);
        }
    }
}