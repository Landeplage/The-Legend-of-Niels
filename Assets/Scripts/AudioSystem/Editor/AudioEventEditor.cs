using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace MordiAudio
{
	[System.Serializable]
	[CanEditMultipleObjects]
	[CustomEditor(typeof(AudioEvent), true)]
	public class AudioEventEditor : Editor
	{
		[SerializeField] private List<AudioSource> auditioningAudioSources;
		[SerializeField] private AudioListener listener;

		bool defaultInspectorFoldout;

		int clipCollectionQueuedForDeletionIndex = -1;
		int spatialCurveSelected = -1;

		/// <summary>
		/// Each axis ranges from -1 to 1.
		/// </summary>
		Vector2 spatialPannerPosition;
		float lastClickedSpatialPanner;

		class Styles
        {
			public Color colCurveVolume, colCurveSpatialBlend, colCurveSpread, colCurveReverbZoneMix, colSliderLabels, colCurveTimelineLabel,
				colClipLabel, colBoxClipBackground;
			public GUIStyle buttonPlaybackControl, curveLegendButton, curveTimelineLabel, curveTimelineLabelRightAlign, curveValueLabel, boxSpatialPanner,
				labelSliderGuide, labelClip, labelClipsEmpty, boxClip;

			public Styles() {
				// Colors
				colCurveVolume = ByteToFloatColor(230, 77, 51);
				colCurveSpatialBlend = ByteToFloatColor(63, 179, 51);
				colCurveSpread = ByteToFloatColor(64, 140, 241);
				colCurveReverbZoneMix = ByteToFloatColor(179, 179, 51);
				colCurveTimelineLabel = new Color(0.6f, 0.6f, 0.6f);
				colSliderLabels = new Color(0.5f, 0.5f, 0.5f);

				// Buttons for play/stop, etc
				buttonPlaybackControl = new GUIStyle(GUI.skin.button) {
					fixedWidth = 50f,
					fixedHeight = 30f
				};

				// Buttons for curve legend
				curveLegendButton = new GUIStyle(GUI.skin.label) {
					fixedWidth = 55f,
					fixedHeight = 30f,
					alignment = TextAnchor.MiddleLeft,
					wordWrap = true,
					fontSize = 10,
					contentOffset = new Vector2(15, 0)
				};

				// Spatial curve timeline labels
				curveTimelineLabel = new GUIStyle(GUI.skin.label) {
					fontSize = 9,
					alignment = TextAnchor.LowerLeft
				};

				curveTimelineLabelRightAlign = new GUIStyle(curveTimelineLabel) {
					alignment = TextAnchor.LowerRight
				};

				curveValueLabel = new GUIStyle(curveTimelineLabel) {
					alignment = TextAnchor.MiddleRight
				};

				boxSpatialPanner = new GUIStyle(GUI.skin.box) {
					fixedWidth = 100, fixedHeight = 100
				};

				// Labels on volume, pan and pitch-sliders
				labelSliderGuide = new GUIStyle(GUI.skin.label) {
					fontSize = 9,
					alignment = TextAnchor.MiddleCenter
				};

				labelClip = new GUIStyle(GUI.skin.label)
				{
					contentOffset = new Vector2(-5f, 0f),
					alignment = TextAnchor.MiddleRight,
					fontSize = 9
				};

				colClipLabel = new Color(1f, 1f, 1f, 0.75f);

				labelClipsEmpty = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Italic
				};

				boxClip = new GUIStyle(GUI.skin.box) {
					margin = new RectOffset(GUI.skin.box.margin.left, GUI.skin.box.margin.right, 1, 0)
				};

				colBoxClipBackground = new Color(0.75f, 0.75f, 0.75f, 0.5f);
			}

			Color ByteToFloatColor(byte r, byte g, byte b, byte a = 255) {
				return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
			}
		}
		static Styles styles;

		class Content
        {
			public GUIContent buttonPlay, buttonStop, buttonClipsSearch, buttonMenu, buttonLoop;

			public Material matColor, matTexture;

			public Texture texPannerBack, texPannerPoint;

			public Content() {
				buttonPlay = new GUIContent(LoadTexture("mas_icon_play"), "Start playback");
				buttonStop = new GUIContent(LoadTexture("mas_icon_stop"), "Stop playback");
				buttonClipsSearch = new GUIContent(LoadTexture("mas_icon_search"), "Find clips");
				buttonMenu = new GUIContent(LoadTexture("mas_icon_menu"), "Open menu");
				buttonLoop = new GUIContent(LoadTexture("mas_icon_loop"), "Loop");

				var shader = Shader.Find("Hidden/Internal-Colored");
				matColor = new Material(shader);

				matTexture = LoadMaterial("mas_mat_texture");

				texPannerBack = LoadTexture("spatial_panner_back");
				texPannerPoint = LoadTexture("spatial_panner_point");
			}

			string GetAssetPath(string name, string type) {
				string[] assetGUIDs = AssetDatabase.FindAssets($"{name} t:{type}");

				if (assetGUIDs == null)
					return null;

				string path = null;
				if (assetGUIDs != null) {
					if (assetGUIDs.Length > 0) {
						path = AssetDatabase.GUIDToAssetPath(assetGUIDs[0]);
					}
				}

				return path;
			}

			Texture LoadTexture(string name) {
				string path = GetAssetPath(name, "Texture");
				if (string.IsNullOrEmpty(path))
					return null;
				return AssetDatabase.LoadAssetAtPath<Texture>(path);
			}

			Material LoadMaterial(string name) {
				string path = GetAssetPath(name, "Material");
				if (string.IsNullOrEmpty(path))
					return null;
				return AssetDatabase.LoadAssetAtPath<Material>(path);
			}
		}
		static Content content;

		struct ClipCollectionDropdownSelection
        {
			public enum FetchType
            {
				basedOnFirstClip,
				clearClips
            }

			public SerializedProperty clipsProp;
			public FetchType fetchType;
        }

		public void OnEnable() {
			UpdateAuditioningAudioSources();

			listener = FindObjectOfType<AudioListener>();
		}

		void UpdateAuditioningAudioSources() {
			if (auditioningAudioSources == null)
				auditioningAudioSources = new List<AudioSource>();
			else
				RemoveAllAuditioningAudioSources();

			AudioEvent audioEvent = (AudioEvent)target;
			if (audioEvent.sounds != null) {
				for (int i = 0; i < audioEvent.sounds.Count; i++) {
                    AudioSource source = UnityEditor.EditorUtility.CreateGameObjectWithHideFlags("Audio preview", HideFlags.HideAndDontSave, typeof(AudioSource)).GetComponent<AudioSource>();
					auditioningAudioSources.Add(source);
				}
			}
        }

		void RemoveAllAuditioningAudioSources() {
			for (int i = auditioningAudioSources.Count - 1; i >= 0; i--) {
				if (auditioningAudioSources[i] != null)
					DestroyImmediate(auditioningAudioSources[i].gameObject);
			}
			auditioningAudioSources.Clear();
		}

		void InitializeContent() {
			if (content == null)
				content = new Content();
		}

		void InitializeStyles() {
			if (styles == null)
				styles = new Styles();
		}

		public void OnDisable() {
			RemoveAllAuditioningAudioSources();
		}

		public override void OnInspectorGUI() {
			InitializeStyles();
			InitializeContent();

			serializedObject.Update();

			SerializedProperty soundsProp = serializedObject.FindProperty("sounds");
			SerializedProperty spatialSettingsProp = serializedObject.FindProperty("spatialSettings");
			SerializedProperty cooldownSettingsProp = serializedObject.FindProperty("cooldownSettings");

			Space();

			// Audition-buttons
			GUILayout.BeginHorizontal();
			GUILayout.BeginHorizontal();
			GUI.enabled = AudioEventHasAnyClips(soundsProp);
			if (GUILayout.Button(content.buttonPlay, styles.buttonPlaybackControl)) {
				AudioEvent audioEvent = (AudioEvent)target;
				audioEvent.Audition(auditioningAudioSources, GetSpatialPannerWorldPosition(spatialSettingsProp.FindPropertyRelative("maxDistance").floatValue));
			}
			GUI.enabled = AnyAuditionSourceIsPlaying();
			if (GUILayout.Button(content.buttonStop, styles.buttonPlaybackControl)) {
				StopAllAuditionSourcesImmediately();
			}
			GUI.enabled = true;

			GUILayout.EndHorizontal();

			// Output
			GUILayout.BeginVertical();
			EditorGUILayout.PropertyField(serializedObject.FindProperty("outputGroup"), new GUIContent() { text = "" });
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();

			Space();

			// Volume, pan, pitch
			GUILayout.BeginVertical();
			RandomizedFloatGUI(serializedObject.FindProperty("volume"), 1f, 0f, 1f);
			Rect rectPan = RandomizedFloatGUI(serializedObject.FindProperty("pan"), 0f, -1f, 1f);
			SliderLabelsGUIPan(rectPan);
			RandomizedFloatGUI(serializedObject.FindProperty("pitch"), 1f, 0.05f, 3f);
			GUILayout.EndVertical();

			Space();

			// Cooldown
			GUILayout.BeginHorizontal(EditorStyles.helpBox);

			SerializedProperty cooldownSettingsEnabledProp = cooldownSettingsProp.FindPropertyRelative("enabled");
			cooldownSettingsEnabledProp.boolValue = EditorGUILayout.ToggleLeft("Cooldown time", cooldownSettingsEnabledProp.boolValue);

			if (cooldownSettingsEnabledProp.boolValue) {
				SerializedProperty cooldownSettingsTimeProp = cooldownSettingsProp.FindPropertyRelative("time");
				cooldownSettingsTimeProp.floatValue = EditorGUILayout.FloatField(cooldownSettingsTimeProp.floatValue, GUILayout.Width(50f));
				GUILayout.Label("seconds", GUILayout.Width(50f));
			}
			
			GUILayout.EndHorizontal();

			Space();

			// 3D settings
			GUILayout.BeginVertical(EditorStyles.helpBox);
			GUILayout.BeginHorizontal();
			SerializedProperty spatialSettingsEnabledProp = spatialSettingsProp.FindPropertyRelative("enabled");
			spatialSettingsEnabledProp.boolValue = EditorGUILayout.ToggleLeft("3D spatialize", spatialSettingsEnabledProp.boolValue);
			if (spatialSettingsEnabledProp.boolValue) {
				GUILayout.FlexibleSpace();
				EditorGUILayout.DropdownButton(new GUIContent() { text = "No preset" }, FocusType.Keyboard, GUILayout.Width(120f));
			}
			GUILayout.EndHorizontal();

			if (spatialSettingsEnabledProp.boolValue) {
				SpatializeGUI(serializedObject.FindProperty("spatialSettings"));
			}

			GUILayout.EndVertical();

			Space();

			// Clip collections
			EditorGUILayout.LabelField("Sounds");
            for (int i = 0; i < soundsProp.arraySize; i++) {
				SoundGUI(soundsProp.GetArrayElementAtIndex(i), i);
			}
			
			Space();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("+", GUILayout.Width(25f), GUILayout.Height(25f))) {
				int index = soundsProp.arraySize;
				soundsProp.InsertArrayElementAtIndex(index);
				soundsProp = serializedObject.FindProperty("sounds");
				SerializedProperty soundProp = soundsProp.GetArrayElementAtIndex(index);
				soundProp.FindPropertyRelative("clips").ClearArray();
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			// Debug: Draw default inspector
			/*Space();
			defaultInspectorFoldout = EditorGUILayout.Foldout(defaultInspectorFoldout, "Default Inspector", true);
			if (defaultInspectorFoldout) {
				DrawDefaultInspector();
			}*/

			// Delete any clip collection queued for deletion
			if (clipCollectionQueuedForDeletionIndex > -1) {
				soundsProp.DeleteArrayElementAtIndex(clipCollectionQueuedForDeletionIndex);
				clipCollectionQueuedForDeletionIndex = -1;
				UpdateAuditioningAudioSources();
            }

			// Apply any modified properties
			serializedObject.ApplyModifiedProperties();

			if (AnyAuditionSourceIsPlaying())
				Repaint();
		}

		#region Inspector GUI methods

		void SpatializeGUI(SerializedProperty spatialSettingsProp) {
			GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(80f));

			if (Event.current.type == EventType.Repaint) {
				Rect rect = GUILayoutUtility.GetLastRect();

				// Add some margin to the rect
				const int MARGIN = 10, LABELS_MARGIN_VALUE = 10, LABELS_MARGIN_TIMELINE = 5;
				rect.x += MARGIN + LABELS_MARGIN_VALUE;
				rect.y += MARGIN;
				rect.width -= MARGIN * 2f + LABELS_MARGIN_TIMELINE;
				rect.height -= MARGIN * 2f + LABELS_MARGIN_TIMELINE;

				// Add timeline labels
				GUI.contentColor = styles.colCurveTimelineLabel;
				CurveGraphLabelsGUI(rect, spatialSettingsProp.FindPropertyRelative("maxDistance").floatValue);
				GUI.contentColor = Color.white;

				BeginDraw(rect);

				DrawSpatialCurveGraphBackground(rect);

				DrawSpatialCurve(rect, styles.colCurveVolume, spatialSettingsProp.FindPropertyRelative("volumeCurve").animationCurveValue, 0);
				DrawSpatialCurve(rect, styles.colCurveSpatialBlend, spatialSettingsProp.FindPropertyRelative("spatialBlendCurve").animationCurveValue, 1);
				DrawSpatialCurve(rect, styles.colCurveSpread, spatialSettingsProp.FindPropertyRelative("stereoSpreadCurve").animationCurveValue, 2);
				DrawSpatialCurve(rect, styles.colCurveReverbZoneMix, spatialSettingsProp.FindPropertyRelative("reverbZoneMixCurve").animationCurveValue, 3);
				DrawListenerIndicator(rect, spatialSettingsProp.FindPropertyRelative("maxDistance").floatValue);

				EndDraw();
			}

			GUILayout.BeginHorizontal();

			GUILayout.BeginVertical();

			GUILayout.BeginHorizontal();
			CurvesLegendGUI("Volume", styles.colCurveVolume, 0);
			CurvesLegendGUI("Spatial blend", styles.colCurveSpatialBlend, 1);
			CurvesLegendGUI("Spread", styles.colCurveSpread, 2);
			CurvesLegendGUI("Reverb zone mix", styles.colCurveReverbZoneMix, 3);
			GUILayout.EndHorizontal();
			Space();

			// Max distance
			SerializedProperty maxDistanceProp = spatialSettingsProp.FindPropertyRelative("maxDistance");
			EditorGUILayout.PropertyField(maxDistanceProp);
			if (maxDistanceProp.floatValue < 0.1f)
				maxDistanceProp.floatValue = 0.1f;

			// Random position settings
			RandomPositionGUI(spatialSettingsProp.FindPropertyRelative("randomPositionSettings"));

			GUILayout.EndVertical();

			// Spatial panner
			if (Screen.width > 375) {
				SpatialPannerGUI();
			}
			
			GUILayout.EndHorizontal();
		}

		void RandomPositionGUI(SerializedProperty prop) {
			GUILayout.BeginHorizontal();
			SerializedProperty typeProp = prop.FindPropertyRelative("type");
			typeProp.enumValueIndex = (int)(AudioEvent.SpatialSettings.RandomPositionSettings.Type)EditorGUILayout.EnumPopup("Random position", (AudioEvent.SpatialSettings.RandomPositionSettings.Type)typeProp.enumValueIndex);
			GUILayout.EndHorizontal();

			if (typeProp.enumValueIndex != (int)AudioEvent.SpatialSettings.RandomPositionSettings.Type.off) {
				GUILayout.BeginHorizontal();
				RandomizedFloatGUI(prop.FindPropertyRelative("offset"), 0f, 0f, 100f);
				GUILayout.EndHorizontal();
			}
		}

		void SpatialPannerGUI() {
			Color back = GUI.backgroundColor;
			GUI.backgroundColor = new Color(1f, 1f, 1f, 0f);
			GUILayout.Box(content.texPannerBack, styles.boxSpatialPanner);
			GUI.backgroundColor = back;

			Rect spatialPannerRect = GUILayoutUtility.GetLastRect();
			
			if (spatialPannerRect.Contains(Event.current.mousePosition)) {
				if (Event.current.type == EventType.MouseDrag) {
					UpdateSpatialPannerPosition(spatialPannerRect, Event.current.mousePosition);
				}
				if (Event.current.type == EventType.MouseDown) {
					UpdateSpatialPannerPosition(spatialPannerRect, Event.current.mousePosition);
					if ((Time.realtimeSinceStartup - lastClickedSpatialPanner) < 0.25f) {
						spatialPannerPosition = Vector2.zero;
						Repaint();
					}
					lastClickedSpatialPanner = Time.realtimeSinceStartup;
                }
			}

			if (Event.current.type == EventType.Repaint) {
				BeginDraw(spatialPannerRect);
				DrawSpatialPannerPosition(spatialPannerRect);
				EndDraw();
			}
		}

		void UpdateSpatialPannerPosition(Rect rect, Vector2 mousePosition) {
			Vector2 normalizedCenter = new Vector2(0.5f, 0.5f);
			Vector2 normalizedMousePos = (mousePosition - rect.position) / rect.size - normalizedCenter;
			
			const float MAX_DISTANCE = 0.4f;
			if (Vector2.Distance(normalizedMousePos, Vector2.zero) > MAX_DISTANCE) {
				float angleRad = Mathf.Atan2(normalizedMousePos.y, normalizedMousePos.x);
				normalizedMousePos = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * (MAX_DISTANCE);
			}

			spatialPannerPosition = normalizedMousePos * 2f;
			Repaint();
		}

		void CurvesLegendGUI(string label, Color color, int spatialCurveIndex) {
			if (GUILayout.Button(label, styles.curveLegendButton)) {
				if (spatialCurveSelected == spatialCurveIndex) {
					spatialCurveSelected = -1;
                } else {
					spatialCurveSelected = spatialCurveIndex;
                }
            }

			if (Event.current.type == EventType.Repaint) {
				Rect rect = GUILayoutUtility.GetLastRect();

				rect.x += 5;

				BeginDraw(rect);
				DrawCurvesLegendColorBox(rect, color, spatialCurveIndex);
				EndDraw();
			}
		}

		void CurveGraphLabelsGUI(Rect rect, float maxDistance) {
			const int LABEL_NUM_TIME = 5, LABEL_NUM_VALUE = 2;

			Rect numRect = new Rect(0, rect.y + rect.height, 25, 15);
            
			// Timeline labels
			for (int i = 0; i < LABEL_NUM_TIME; i++) {
				numRect.x = rect.x + (i * (rect.width / LABEL_NUM_TIME));
				GUI.Label(numRect, Mathf.Round((maxDistance / LABEL_NUM_TIME) * i).ToString(), styles.curveTimelineLabel);
			}
			numRect.x = rect.x + rect.width - numRect.width;
			GUI.Label(numRect, Mathf.Round(maxDistance).ToString(), styles.curveTimelineLabelRightAlign);

			// Value labels
			numRect.x = rect.x - numRect.width - 2;
			for (int i = 0; i <= LABEL_NUM_VALUE; i++) {
				numRect.y = rect.y + rect.height - (i * ((rect.height) / LABEL_NUM_VALUE)) - numRect.height / 2;
				GUI.Label(numRect, (i / (float)LABEL_NUM_VALUE).ToString(), styles.curveValueLabel);
			}
		}

		Rect RandomizedFloatGUI(SerializedProperty randomFloatProp, float defaultValue, float min, float max) {
			SerializedProperty prop = randomFloatProp;
			SerializedProperty minProp = prop.FindPropertyRelative("min");
			SerializedProperty maxProp = prop.FindPropertyRelative("max");

			GUILayout.BeginHorizontal();

			// Slider label
			GUILayout.Label(prop.displayName, GUILayout.Width(50f));

			// Slider
			float minValue = minProp.floatValue, maxValue = maxProp.floatValue;
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.MinMaxSlider(ref minValue, ref maxValue, min, max, GUILayout.Height(25f));
			if (EditorGUI.EndChangeCheck()) {
				minProp.floatValue = minValue;
				maxProp.floatValue = maxValue;
			}

			Rect sliderRect = GUILayoutUtility.GetLastRect();

			// Slider guide lines
			DrawRandomizedFloatSliderLines(sliderRect, defaultValue, min, max);

			// Fields
			Space(5f);
			minProp.floatValue = EditorGUILayout.FloatField(minProp.floatValue, GUILayout.Width(50f));
			maxProp.floatValue = EditorGUILayout.FloatField(maxProp.floatValue, GUILayout.Width(50f));

			// Reset-button
			if (GUILayout.Button("R", GUILayout.Width(20f))) {
				minProp.floatValue = defaultValue;
				maxProp.floatValue = defaultValue;
            }

			// Clamp values
			minProp.floatValue = Mathf.Clamp(minProp.floatValue, min, maxProp.floatValue);
			maxProp.floatValue = Mathf.Clamp(maxProp.floatValue, minProp.floatValue, max);

			// Round values
			const float ROUND = 100f;
			minProp.floatValue = Mathf.Round(minProp.floatValue * ROUND) / ROUND;
			maxProp.floatValue = Mathf.Round(maxProp.floatValue * ROUND) / ROUND;

			GUILayout.EndHorizontal();

			return sliderRect;
		}

		void SliderLabelsGUIPan(Rect rect) {
			Color contentCol = GUI.contentColor;
			GUI.contentColor = styles.colSliderLabels;

			Rect r;
			float y = rect.y + 10f, h = 15f;
			r = new Rect(rect.x - 4f, y, 25f, h);
			GUI.Label(r, "Left", styles.labelSliderGuide);
			r = new Rect(rect.x + rect.width - 23f, y, 25f, h);
			GUI.Label(r, "Right", styles.labelSliderGuide);

			GUI.contentColor = contentCol;
		}

		void SoundGUI(SerializedProperty soundProp, int index) {
			GUILayout.BeginVertical(EditorStyles.helpBox);
			GUILayout.BeginHorizontal();
			int soundNum = index + 1;
			GUILayout.Label($"Sound {(soundNum < 10 ? "0" : "")}{soundNum}");
			
			SerializedProperty soundLoopProp = soundProp.FindPropertyRelative("loop");
			Color backgroundColor = GUI.backgroundColor;
			if (soundLoopProp.boolValue)
				GUI.backgroundColor = Color.green;
			if (GUILayout.Button(content.buttonLoop, GUILayout.Width(25f))) {
				soundLoopProp.boolValue = !soundLoopProp.boolValue;
            }
			GUI.backgroundColor = backgroundColor;

			/*EditorGUILayout.LabelField("Play", GUILayout.Width(30f));
			clipCollection.playTime = (AudioEvent.ClipCollection.PlayTime)EditorGUILayout.EnumPopup(clipCollection.playTime, GUILayout.Width(75f));*/

			SerializedProperty chanceToPlayProp = soundProp.FindPropertyRelative("chanceToPlay");
			chanceToPlayProp.floatValue = Mathf.Clamp01(EditorGUILayout.IntField(Mathf.RoundToInt(chanceToPlayProp.floatValue * 100), GUILayout.Width(40f)) / 100f);
			GUILayout.Label("%", GUILayout.Width(15f));

			GUILayout.FlexibleSpace();
			if (GUILayout.Button("X", GUILayout.Width(25f))) {
				clipCollectionQueuedForDeletionIndex = index;
			}
			GUILayout.EndHorizontal();

			EditorGUI.indentLevel += 1;
			ClipsGUI(soundProp.FindPropertyRelative("clips"));
			EditorGUI.indentLevel -= 1;
			GUILayout.EndVertical();
		}

		void ClipsGUI(SerializedProperty clipsProp) {
			GUILayout.BeginHorizontal();

			if (GUILayout.Button(content.buttonClipsSearch, GUILayout.Width(25f))) {
				AudioClipSearchWindow.Open((AudioEvent)target, clipsProp);
			}

			if (GUILayout.Button(content.buttonMenu, GUILayout.Width(25f))) {
				GenericMenu menu = new GenericMenu();
				menu.AddItem(new GUIContent("Fetch based on first clip"), false, OnClipCollectionDropdownButton, new ClipCollectionDropdownSelection()
				{
					clipsProp = clipsProp,
					fetchType = ClipCollectionDropdownSelection.FetchType.basedOnFirstClip
				});
				menu.AddSeparator("");
				menu.AddItem(new GUIContent("Clear clips"), false, OnClipCollectionDropdownButton, new ClipCollectionDropdownSelection()
				{
					clipsProp = clipsProp,
					fetchType = ClipCollectionDropdownSelection.FetchType.clearClips
				});
				menu.ShowAsContext();
			}

			GUILayout.FlexibleSpace();

			GUILayout.Label($"{clipsProp.arraySize} {(clipsProp.arraySize == 1 ? "clip" : "clips")}");

			GUILayout.EndHorizontal();

			if (clipsProp.arraySize == 0) {
				GUILayout.Label("No clips", styles.labelClipsEmpty);
				return;
            }

			// Find longest clip duration and currently playing clip index
			float longestDuration = 0f;
			int currentlyPlayingClipIndex = -1;
			float currentPlaybackPosition = 0f;
			for (int i = 0; i < clipsProp.arraySize; i++) {
				SerializedProperty clipProp = clipsProp.GetArrayElementAtIndex(i);
				AudioClip clip = clipProp.objectReferenceValue as AudioClip;
				if (clip == null)
					continue;

				if (clip.length > longestDuration)
					longestDuration = clip.length;

                for (int n = 0; n < auditioningAudioSources.Count; n++) {
					if (auditioningAudioSources[n] == null)
						continue;
					if (auditioningAudioSources[n].isPlaying && auditioningAudioSources[n].clip == clip) {
						currentlyPlayingClipIndex = i;
						currentPlaybackPosition = auditioningAudioSources[n].time;
						break;
					}
                }
			}

			for (int i = 0; i < clipsProp.arraySize; i++) {
				SerializedProperty clipProp = clipsProp.GetArrayElementAtIndex(i);
				AudioClip clip = clipProp.objectReferenceValue as AudioClip;
				if (clip == null)
					continue;

				float lengthFactor = clip.length / longestDuration;

				Texture texture = AssetPreview.GetAssetPreview(clip);
				Color backgroundColor = GUI.backgroundColor;
				GUI.backgroundColor = styles.colBoxClipBackground;
				GUILayout.Box("", styles.boxClip, GUILayout.ExpandWidth(true), GUILayout.Height(15f));
				GUI.backgroundColor = backgroundColor;

				// Draw waveform texture
				Rect rect = GUILayoutUtility.GetLastRect();
				BeginDraw(rect);
				SetGLTexture(texture);
				GL.Begin(GL.QUADS);
				DrawTexturedQuad(0f, 0f, rect.width * lengthFactor, rect.height);
				GL.End();
				if (i == currentlyPlayingClipIndex) {
					content.matColor.SetPass(0);
					GL.Begin(GL.QUADS);
					float pos = currentPlaybackPosition / clip.length;

					// Tint
					GL.Color(new Color(0f, 0f, 0f, Mathf.Clamp01(0.5f - currentPlaybackPosition) * 0.35f));
					DrawQuad(0f, 0f, rect.width, rect.height);

					// Playback position indicator
					const int INDICATOR_WIDTH = 100;
                    for (int n = 0; n < INDICATOR_WIDTH; n++) {
						float x = (pos * rect.width * lengthFactor) - n;
						if (x < 0f)
							break;
						float a = (INDICATOR_WIDTH - n) / (float)INDICATOR_WIDTH;
						float aFade = Mathf.Clamp01(3f - pos * 3f); // Fade near the end
						GL.Color(new Color(1f, 1f, 1f, a * a * a * aFade * 0.35f));
						DrawQuad(x, 0f, 1f, rect.height);
					}
					GL.End();
				}
				EndDraw();

				// Clip name label
				GUI.contentColor = styles.colClipLabel;
				GUI.Label(rect, clip.name, styles.labelClip);
				GUI.contentColor = Color.white;
            }
        }

		#endregion

		#region OpenGL draw methods

		void BeginDraw(Rect rect) {
			GUI.BeginClip(rect);
			GL.PushMatrix();
			GL.Clear(true, false, Color.black);
			content.matColor.SetPass(0);
		}

		void SetGLTexture(Texture texture) {
			content.matTexture.mainTexture = texture;
			content.matTexture.SetPass(0);
		}

		void EndDraw() {
			GL.PopMatrix();
			GUI.EndClip();
		}

		void DrawRandomizedFloatSliderLines(Rect rect, float defaultValue, float min, float max) {
			BeginDraw(rect);

			GL.Begin(GL.LINES);

			const float HANDLE_WIDTH = 6f;
			float x = (rect.width - HANDLE_WIDTH * 2) * (defaultValue - min) / (max - min) + HANDLE_WIDTH;
			x = Mathf.Floor(x);

			// Default
			GL.Color(styles.colSliderLabels);
			GL.Vertex3(x, 2, 0);
			GL.Vertex3(x, 16, 0);

			GL.End();

			EndDraw();
        }

		void DrawSpatialPannerPosition(Rect rect) {
			SetGLTexture(content.texPannerPoint);
			GL.Begin(GL.QUADS);

			// Current pan position
			Vector2 pos = (spatialPannerPosition * rect.size / 2f) + rect.size / 2;
			DrawTexturedQuad(pos.x - 5, pos.y - 5, content.texPannerPoint.width, content.texPannerPoint.height);

			GL.End();
		}

		void DrawSpatialCurveGraphBackground(Rect rect) {
			GL.Begin(GL.LINES);
			GL.Color(new Color(0.5f, 0.5f, 0.5f, 0.25f));

			GL.Vertex3(-1, -1, 0f);
			GL.Vertex3(-1, rect.height + 1, 0f);

			GL.Vertex3(-1, rect.height + 1, 0f);
			GL.Vertex3(rect.width + 1, rect.height + 1, 0f);

			GL.Vertex3(rect.width + 1, rect.height + 1, 0f);
			GL.Vertex3(rect.width + 1, -1, 0f);

			GL.Vertex3(rect.width + 1, - 1, 0f);
			GL.Vertex3(-1, -1, 0f);

			GL.End();
		}

		void DrawSpatialCurve(Rect rect, Color color, AnimationCurve curve, int spatialCurveIndex) {
			if (!(spatialCurveSelected == -1 || spatialCurveSelected == spatialCurveIndex))
				return;

			const float SAMPLES = 64;

			GL.Begin(GL.LINES);
			GL.Color(color);

            for (int i = 1; i < SAMPLES; i++) {
				float x = ((i - 1) / SAMPLES) * rect.width;
				float y = (1f - curve.Evaluate(((i - 1) / SAMPLES))) * rect.height;
				GL.Vertex3(x, y, 0);
				x = (i / SAMPLES) * rect.width;
				y = (1f - curve.Evaluate((i / SAMPLES))) * rect.height;
				GL.Vertex3(x, y, 0);
			}

			GL.End();
		}

		void DrawListenerIndicator(Rect rect, float maxDistance) {
			GL.Begin(GL.LINES);
			GL.Color(Color.gray);

			float w = 10, h = 5;

			for (int i = 0; i < auditioningAudioSources.Count; i++) {
				if (!auditioningAudioSources[i].isPlaying)
					continue;

				float dis = Vector3.Distance(listener.transform.position, auditioningAudioSources[i].transform.position);
				float x = (dis / maxDistance) * rect.width;
				float y = rect.height;
				GL.Vertex3(x, y - h, 0);
				GL.Vertex3(x - w/2, y, 0);
				GL.Vertex3(x - w/2, y, 0);
				GL.Vertex3(x + w/2, y, 0);
				GL.Vertex3(x + w / 2, y, 0);
				GL.Vertex3(x, y - h, 0);
				GL.Vertex3(x, 0, 0);
				GL.Vertex3(x, y - h, 0);
			}

			GL.End();
		}

		void DrawCurvesLegendColorBox(Rect rect, Color color, int spatialCurveIndex) {
			if (!(spatialCurveSelected == -1 || spatialCurveSelected == spatialCurveIndex))
				color = Color.gray;

			GL.Begin(GL.QUADS);

			float x, y, w, h;
			w = 8;
			h = 8;
			x = 0;
			y = (rect.height - h) / 2;

			GL.Color(color * 0.25f);
			DrawQuad(x, y, w, h);
			GL.Color(color);
			DrawQuad(x + 1, y + 1, w - 2, h - 2);

			GL.End();
		}

		void DrawQuad(float x, float y, float w, float h) {
			GL.Vertex3(x, y, 0);
			GL.Vertex3(x, y + h, 0);
			GL.Vertex3(x + w, y + h, 0);
			GL.Vertex3(x + w, y, 0);
		}

		void DrawTexturedQuad(float x, float y, float w, float h) {
			GL.Color(Color.white);
			GL.TexCoord2(0, 1);
			GL.Vertex3(x, y, 0);
			GL.TexCoord2(1, 1);
			GL.Vertex3(x + w, y, 0);
			GL.TexCoord2(1, 0);
			GL.Vertex3(x + w, y + h, 0);
			GL.TexCoord2(0, 0);
			GL.Vertex3(x, y + h, 0);
		}

		#endregion

		Vector3 GetSpatialPannerWorldPosition(float maxDistance) {
			Vector3 listenerPos = listener.transform.position;
			Vector3 pannerOffset = new Vector3(spatialPannerPosition.x, 0f, -spatialPannerPosition.y) * maxDistance;

			return listenerPos + pannerOffset;
		}

        void OnClipCollectionDropdownButton(object clipCollectionDropdownSelection) {
			ClipCollectionDropdownSelection selection = (ClipCollectionDropdownSelection)clipCollectionDropdownSelection;
            switch (selection.fetchType) {
                case ClipCollectionDropdownSelection.FetchType.basedOnFirstClip:
					FetchClipsBasedOnFirstClip(selection.clipsProp);
					break;
				case ClipCollectionDropdownSelection.FetchType.clearClips:
					selection.clipsProp.ClearArray();
					serializedObject.ApplyModifiedProperties();
					break;
                default:
                    break;
            }
		}

		void FetchClipsBasedOnFirstClip(SerializedProperty clipsProp) {
			if (clipsProp == null)
				return;

			if (clipsProp.arraySize == 0)
				return;

			if (clipsProp.GetArrayElementAtIndex(0) == null)
				return;

			string clipName = (clipsProp.GetArrayElementAtIndex(0).objectReferenceValue as AudioClip).name;
			for (int i = 0; i < 10; i++) {
				// Remove numbers from end of name
				if (LastCharacterInStringIsNumber(clipName)) {
					clipName = clipName.Substring(0, clipName.Length - 1);
				}
				clipName = clipName.Trim(); // Trim whitespace
			}

			FetchClips(clipsProp, clipName);
		}

		void FetchClips(SerializedProperty clipsProp, string search) {
			AudioClip[] clips = Utility.Editor.FindAndLoadAssets<AudioClip>(search);

			if (clips.Length == 0) {
				Debug.Log("No matching clips found.");
				return;
			}

			// Clear clip-list in AudioEvent
			if (clipsProp != null)
				clipsProp.ClearArray();

			for (int i = 0; i < clips.Length; i++) {
				clipsProp.InsertArrayElementAtIndex(i);
				clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
			}
        }

        #region Utility methods

        void Space(float pixels = 5f) {
			GUILayout.Space(pixels);
		}

		bool LastCharacterInStringIsNumber(string str) {
			string lastChar = str.Substring(str.Length - 1);
			return int.TryParse(lastChar, out _);
		}

		bool AudioEventHasAnyClips(SerializedProperty soundsProp) {
			if (soundsProp == null)
				return false;
            for (int i = 0; i < soundsProp.arraySize; i++) {
				SerializedProperty soundProp = soundsProp.GetArrayElementAtIndex(i);
				SerializedProperty clipsProp = soundProp.FindPropertyRelative("clips");
				if (clipsProp.arraySize > 0)
					return true;
            }
			return false;
        }

		bool AnyAuditionSourceIsPlaying() {
            for (int i = 0; i < auditioningAudioSources.Count; i++) {
				if (auditioningAudioSources[i].isPlaying)
					return true;
			}
			return false;
        }

		void StopAllAuditionSourcesImmediately() {
            for (int i = 0; i < auditioningAudioSources.Count; i++) {
				auditioningAudioSources[i].Stop();
            }
        }

		#endregion
	}
}