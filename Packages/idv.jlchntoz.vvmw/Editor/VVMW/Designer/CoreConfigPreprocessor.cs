using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UdonSharpEditor;
using JLChnToZ.VRC.Foundation.Editors;

namespace JLChnToZ.VRC.VVMW.Editors {
    internal class CoreConfigPreprocessor : IPreprocessor {
        public int Priority => 99;

        public void OnPreprocess(Scene scene) {
            foreach (var core in scene.IterateAllComponents<Core>()) {
#if VRC_ENABLE_PLAYER_PERSISTENCE
                if (core.enablePersistence) {
                    var pathStack = new Stack<string>();
                    for (var transform = core.transform; transform; transform = transform.parent)
                        pathStack.Push(transform.name);
                    var path = string.Join("/", pathStack);
                    core.volumePersistenceKey = $"VVMW:{path}:Volume";
                    core.mutedPersistenceKey = $"VVMW:{path}:Muted";
                }
#endif
                var audioControllers = new List<AbstractAudioController>();
                var audioSources = new HashSet<AudioSource>(core.audioSources);
                foreach (var audioSource in core.audioSources) {
                    if (audioSource == null || !audioSource.TryGetComponent(out AbstractAudioController controller))
                        continue;
                    controller.core = core;
                    audioSources.Remove(audioSource);
                    audioControllers.Add(controller);
                    UdonSharpEditorUtility.CopyProxyToUdon(controller);
                }
                core.audioSources = new AudioSource[audioSources.Count];
                audioSources.CopyTo(core.audioSources);
                core.audioControllers = audioControllers.ToArray();
                core.hasRegion = ActiveRegionConfig.GetRegionConfigs(core).Count > 0;
                UdonSharpEditorUtility.CopyProxyToUdon(core);
            }
        }
    }
}