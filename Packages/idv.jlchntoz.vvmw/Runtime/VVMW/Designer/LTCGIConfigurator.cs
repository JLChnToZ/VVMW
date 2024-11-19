#if LTCGI_IMPORTED && UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using JLChnToZ.VRC.Foundation;
using pi.LTCGI;

namespace JLChnToZ.VRC.VVMW.Designer {
    [ExecuteInEditMode]
    [EditorOnly]
    internal class LTCGIConfigurator : MonoBehaviour, ISelfPreProcess {
        internal static Action<LTCGIConfigurator, Core, LTCGI_Controller, List<LTCGI_Screen>> OnPreProcess;
        [HideInInspector] public Core core;
        [HideInInspector] public LTCGI_Controller controller;
        [HideInInspector] public List<LTCGI_Screen> screens;

        public int Priority => 0;

        void ISelfPreProcess.PreProcess() =>
            OnPreProcess?.Invoke(this, core, controller, screens);

        void OnDestroy() {
            if (Application.isPlaying) return;
            foreach (var screen in screens)
                if (screen != null)
                    DestroyImmediate(screen);
        }
    }
}
#endif