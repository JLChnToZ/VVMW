using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class FrontendHandler {
        [SerializeField, LocalizedLabel] int historySize = 5;
        [UdonSynced] VRCUrl[] historyUrls, historyQuestUrls;
        [UdonSynced] byte[] historyPlayerIndex;
        [UdonSynced] string historyTitles;
        VRCUrl[] localHistoryUrls, localHistoryQuestUrls;
        byte[] localHistoryPlayerIndex;
        string[] localHistoryTitles;

        public int HistorySize => historySize;

        public VRCUrl[] HistoryUrls {
            get {
                if (!Utilities.IsValid(localHistoryUrls)) localHistoryUrls = new VRCUrl[0];
                return localHistoryUrls;
            }
        }

        public string[] HistoryTitles {
            get {
                if (!Utilities.IsValid(localHistoryTitles)) localHistoryTitles = new string[0];
                return localHistoryTitles;
            }
        }

        public void PlayHistory(int index) {
            if (!Utilities.IsValid(localHistoryUrls) || index < 0 || index >= localHistoryUrls.Length) return;
            var pcUrl = localHistoryUrls[index];
            var questUrl = IsArrayNullOrEmpty(localHistoryQuestUrls) ? pcUrl : localHistoryQuestUrls[index];
            PlayUrl(pcUrl, questUrl, localHistoryTitles[index], localHistoryPlayerIndex[index]);
        }
        void RecordPlaybackHistory(VRCUrl pcUrl, VRCUrl questUrl, byte playerIndex, string title) {
            if (historySize <= 0) return;
            bool hasQuestUrl = !VRCUrl.IsNullOrEmpty(questUrl) && !pcUrl.Equals(questUrl);
            if (IsArrayNullOrEmpty(localHistoryUrls)) {
                localHistoryUrls = new VRCUrl[1] { pcUrl };
                if (hasQuestUrl) localHistoryQuestUrls = new VRCUrl[1] { questUrl };
                localHistoryPlayerIndex = new byte[1] { playerIndex };
                localHistoryTitles = new string[1] { title };
                return;
            }
            VRCUrl[] tempUrls, tempQuestUrls = null;
            byte[] tempPlayerIndex;
            string[] tempTitles;
            int currentSize;
            if (localHistoryUrls.Length < historySize) {
                tempUrls = new VRCUrl[localHistoryUrls.Length + 1];
                if (hasQuestUrl) tempQuestUrls = new VRCUrl[localHistoryUrls.Length + 1];
                tempPlayerIndex = new byte[localHistoryPlayerIndex.Length + 1];
                tempTitles = new string[localHistoryTitles.Length + 1];
                currentSize = localHistoryUrls.Length;
            } else {
                tempUrls = localHistoryUrls;
                tempQuestUrls = localHistoryQuestUrls;
                tempPlayerIndex = localHistoryPlayerIndex;
                tempTitles = localHistoryTitles;
                currentSize = historySize - 1;
            }
            Array.Copy(localHistoryUrls, 0, tempUrls, 1, currentSize);
            if (!IsArrayNullOrEmpty(tempQuestUrls)) Array.Copy(localHistoryQuestUrls, 0, tempQuestUrls, 1, currentSize);
            Array.Copy(localHistoryPlayerIndex, 0, tempPlayerIndex, 1, currentSize);
            Array.Copy(localHistoryTitles, 0, tempTitles, 1, currentSize);
            tempUrls[0] = pcUrl;
            if (!IsArrayNullOrEmpty(tempQuestUrls)) tempQuestUrls[0] = VRCUrl.IsNullOrEmpty(questUrl) ? pcUrl : questUrl;
            tempPlayerIndex[0] = playerIndex;
            tempTitles[0] = title;
            localHistoryUrls = tempUrls;
            localHistoryQuestUrls = tempQuestUrls;
            localHistoryPlayerIndex = tempPlayerIndex;
            localHistoryTitles = tempTitles;
        } 
    }
}