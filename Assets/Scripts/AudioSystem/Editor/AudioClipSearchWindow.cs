using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace MordiAudio
{
    public class AudioClipSearchWindow : EditorWindow
    {
        static AudioClipSearchWindow window;
        static AudioEvent target;
        static AudioEvent.Sound targetClipCollection;

        public static void Open(AudioEvent audioEvent, AudioEvent.Sound clipCollection) {
            window = (AudioClipSearchWindow)GetWindow(typeof(AudioClipSearchWindow), true, "Clip searcher");
            window.Show();
            target = audioEvent;
            targetClipCollection = clipCollection;
        }

        string search;
        AudioClip[] clipsFound;
        AudioClip[] ClipsFound {
            get { return clipsFound; }
            set {
                clipsFound = value;
                UpdateClipSelectionAndFoldoutArray();
            }
        }
        bool[] clipSelected;
        bool[] clipGroupSelected;
        bool[] clipFoldout;
        bool hasOpened;
        Vector2 clipsScrollPosition;

        private void OnGUI() {
            if (target == null)
                window.Close();

            GUILayout.Label(target.name);
            GUILayout.Space(10f);

            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName("searchField");
            search = GUILayout.TextField(search);
            if (EditorGUI.EndChangeCheck()) {
                ClipsFound = FetchClips(search);
            }

            if (!hasOpened) {
                hasOpened = true;
                ClipsFound = FetchClips(search);
                GUI.FocusControl("searchField");
            }

            ButtonsGUI();

            GUILayout.Space(5f);

            ClipsGUI();
        }

        void ButtonsGUI() {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select all", GUILayout.Width(100f))) {
                SelectAll();
            }
            if (GUILayout.Button("Select none", GUILayout.Width(100f))) {
                SelectNone();
            }
            GUILayout.Space(10f);

            int selected = CountSelected();
            GUI.enabled = selected > 0;
            if (GUILayout.Button($"Assign {selected} {(selected == 1 ? "clip" : "clips")}")) {
                AssignClips();
                window.Close();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        void ClipsGUI() {
            if (ClipsFound == null) {
                GUILayout.Label("No clips found.");
                return;
            }

            if (ClipsFound.Length == 0) {
                GUILayout.Label("No clips found.");
                return;
            }

            GUILayout.Label($"{ClipsFound.Length} clips found.");

            clipsScrollPosition = GUILayout.BeginScrollView(clipsScrollPosition);

            ClipListGUI();

            GUILayout.EndScrollView();
        }

        void ClipListGUI() {
            int groupCountPrev = 0;
            for (int i = 0; i < ClipsFound.Length; i++) {

                // Count similarly named clips
                int groupCount = 0;
                string truncatedName = TruncateTrailingNumbers(ClipsFound[i].name);
                for (int n = 1; n < ClipsFound.Length - i; n++) {
                    if (truncatedName == TruncateTrailingNumbers(ClipsFound[i + n].name)) {
                        groupCount += 1;
                    } else {
                        break;
                    }
                }

                if (groupCountPrev == 0 && groupCount > 0) {
                    GUILayout.BeginHorizontal();

                    // Check if all or no clips in the group are checked
                    int selectedNum = 0;
                    for (int n = 0; n <= groupCount; n++) {
                        if (clipSelected[i + n])
                            selectedNum++;
                    }
                    if (selectedNum == 0)
                        clipGroupSelected[i] = false;
                    if (selectedNum > 0)
                        clipGroupSelected[i] = true;

                    EditorGUI.BeginChangeCheck();
                    clipGroupSelected[i] = GUILayout.Toggle(clipGroupSelected[i], "", GUILayout.Width(20f));
                    if (EditorGUI.EndChangeCheck()) {
                        for (int n = 0; n <= groupCount; n++) {
                            clipSelected[i + n] = clipGroupSelected[i];
                        }
                    }

                    clipFoldout[i] = EditorGUILayout.Foldout(clipFoldout[i], TruncateTrailingNumbers(ClipsFound[i].name), true);
                    GUILayout.EndHorizontal();
                    if (clipFoldout[i]) {
                        for (int m = 0; m <= groupCount; m++) {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(20f);
                            clipSelected[i + m] = GUILayout.Toggle(clipSelected[i + m], ClipsFound[i + m].name);
                            GUILayout.EndHorizontal();
                        }
                    }
                    i += groupCount;
                    groupCount = 0;
                } else {
                    GUILayout.BeginHorizontal();
                    clipSelected[i] = GUILayout.Toggle(clipSelected[i], "       " + ClipsFound[i].name);
                    GUILayout.EndHorizontal();
                }

                groupCountPrev = groupCount;
            }
        }

        AudioClip[] FetchClips(string search) {
            string[] assetGUIDs = AssetDatabase.FindAssets($"t:AudioClip {search}");

            if (assetGUIDs == null)
                return null;

            if (assetGUIDs.Length == 0)
                return null;

            AudioClip[] clips = new AudioClip[assetGUIDs.Length];
            for (int i = 0; i < assetGUIDs.Length; i++) {
                string path = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            return clips;
        }

        void UpdateClipSelectionAndFoldoutArray() {
            if (ClipsFound == null)
                return;

            if (ClipsFound.Length == 0)
                return;

            clipSelected = new bool[ClipsFound.Length];
            clipFoldout = new bool[ClipsFound.Length];
            clipGroupSelected = new bool[ClipsFound.Length];

            if (string.IsNullOrEmpty(search))
                SelectNone();
            else
                SelectAll();
        }

        void SelectAll() {
            for (int i = 0; i < clipSelected.Length; i++) {
                clipSelected[i] = true;
            }
        }

        void SelectNone() {
            for (int i = 0; i < clipSelected.Length; i++) {
                clipSelected[i] = false;
            }
        }

        int CountSelected() {
            if (ClipsFound == null)
                return 0;

            int count = 0;
            for (int i = 0; i < ClipsFound.Length; i++) {
                if (clipSelected[i])
                    count++;
            }
            return count;
        }

        void AssignClips() {
            if (clipSelected == null)
                return;

            if (clipSelected.Length == 0)
                return;

            List<AudioClip> selectedClipsList = new List<AudioClip>();
            for (int i = 0; i < clipSelected.Length; i++) {
                if (clipSelected[i])
                    selectedClipsList.Add(ClipsFound[i]);
            }
            targetClipCollection.clips = selectedClipsList;
        }

        #region Utility methods

        string TruncateTrailingNumbers(string str) {
            bool LastCharacterInStringIsNumber(string str) {
                string lastChar = str.Substring(str.Length - 1);
                return int.TryParse(lastChar, out _);
            }

            while (LastCharacterInStringIsNumber(str)) {
                str = str.Substring(0, str.Length - 1);
            }

            return str.Trim(); // Trim whitespace
        }

        #endregion
    }
}

