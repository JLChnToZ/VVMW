using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UdonSharpEditor;
using JLChnToZ.VRC.Foundation.Editors;

namespace JLChnToZ.VRC.VVMW.Editors {
    internal class CoreConfigPreprocessor : IPreprocessor {
        public int Priority => 100;

        public void OnPreprocess(Scene scene) {
            foreach (var core in scene.IterateAllComponents<Core>()) {
                bool changed = false;
#if VRC_ENABLE_PLAYER_PERSISTENCE
                if (core.enablePersistence) {
                    var pathStack = new Stack<string>();
                    for (var transform = core.transform; transform; transform = transform.parent)
                        pathStack.Push(transform.name);
                    var path = string.Join("/", pathStack);
                    core.volumePersistenceKey = $"VVMW:{path}:Volume";
                    core.mutedPersistenceKey = $"VVMW:{path}:Muted";
                    changed = true;
                }
#endif
                if (changed) UdonSharpEditorUtility.CopyProxyToUdon(core);
            }
        }
    }
}