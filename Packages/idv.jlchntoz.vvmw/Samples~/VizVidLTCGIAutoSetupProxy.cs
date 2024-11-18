using System;
using UnityEngine;
using pi.LTCGI;

public class VizVidLTCGIAutoSetupProxy : ILTCGI_AutoSetup {
    ILTCGI_AutoSetup autoSetup;

    public GameObject AutoSetupEditor(LTCGI_Controller controller) {
        if (autoSetup == null) {
            var type = Type.GetType("%TYPE%", false);
            if (type != null) autoSetup = Activator.CreateInstance(type) as ILTCGI_AutoSetup;
        }
        return autoSetup?.AutoSetupEditor(controller);
    }
}
