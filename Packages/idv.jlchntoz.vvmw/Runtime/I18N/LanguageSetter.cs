using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace JLChnToZ.VRC.VVMW.I18N {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("VizVid/Locales/Language Setter")]
    public class LanguageSetter : UdonSharpBehaviour {
        [SerializeField, HideInInspector] LanguageManager manager;
        [SerializeField] GameObject entryTemplate;
        Toggle[] spawnedEntries;
        bool hasInit = false;

        void Start() {
            manager._AddListener(this);
            entryTemplate.SetActive(false);
            SendCustomEventDelayedFrames(nameof(_DetectLanguageInit), 0);
        }

        public void _DetectLanguageInit() {
            var keys = manager.LanguageKeys;
            var names = manager.LanguageNames;
            if (keys == null || names == null) {
                SendCustomEventDelayedFrames(nameof(_DetectLanguageInit), 0);
                return;
            }
            spawnedEntries = new Toggle[keys.Length];
            for (int i = 0; i < keys.Length; i++) {
                var entry = Instantiate(entryTemplate);
                entry.transform.SetParent(transform, false);
                entry.GetComponentInChildren<Text>().text = names[i];
                entry.SetActive(true);
                spawnedEntries[i] = entry.GetComponent<Toggle>();
            }
            hasInit = true;
            _OnLanguageChanged();
        }

        public void _OnLanguageChanged() {
            if (!hasInit) return;
            int index = Array.IndexOf(manager.LanguageKeys, manager.LanguageKey);
            for (int i = 0; i < spawnedEntries.Length; i++)
                spawnedEntries[i].SetIsOnWithoutNotify(i == index);
        }

        public void _OnToggleClick() {
            if (!hasInit) return;
            for (int i = 0; i < spawnedEntries.Length; i++) {
                if (spawnedEntries[i].isOn) {
                    manager.LanguageKey = manager.LanguageKeys[i];
                    break;
                }
            }
        }
    }
}