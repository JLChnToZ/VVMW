using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UdonSharpEditor;
using JLChnToZ.VRC.Foundation.Editors;
using JLChnToZ.VRC.VVMW.Designer;

using Pool = JLChnToZ.VRC.Foundation.PooledObjectExtensions;

using static UnityEngine.Object;

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
                if (!core.muteOnOutOfRange) core.outOfRangeVolume = 1f;
                UdonSharpEditorUtility.CopyProxyToUdon(core);
            }
            foreach (var screenConfigurator in scene.IterateAllComponents<ScreenConfigurator>())
                if (screenConfigurator.HasIdleTexture)
                    foreach (var hiita in screenConfigurator.gameObject.IterateAllComponents<HideIfIdleTextureAvailable>())
                        using (Pool.Get(out List<Component> components)) {
                            hiita.GetComponents(components);
                            components.Sort(DependencyUtils.DependencyComparer.instance);
                            for (int i = 0, count = components.Count; i < count; i++) {
                                var type = components[i].GetType();
                                if (typeof(Graphic).IsAssignableFrom(type) ||
                                    DependencyUtils.IsRequired(type, typeof(Graphic), deep: true))
                                    DestroyImmediate(components[i]);
                            }
                        }
        }
    }
}