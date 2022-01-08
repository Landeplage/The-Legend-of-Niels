using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace MordiAudio
{
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
			public Color colCurveVolume, colCurveSpatialBlend, colCurveSpread, colCurveReverbZoneMix, colSliderLabels;
			public Color colCurveTimelineLabel;
			public GUIStyle buttonPlaybackControl, curveLegendButton, curveTimelineLabel, curveTimelineLabelRightAlign, curveValueLabel, boxSpatialPanner,
				labelSliderGuide;

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
			}

			Color ByteToFloatColor(byte r, byte g, byte b, byte a = 255) {
				return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
			}
		}
		static Styles styles;

		class Content
        {
			public GUIContent buttonPlay, buttonStop;

			public Material matColor, matTexture;

			public Texture texPannerBack, texPannerPoint;

			public Content() {
				buttonPlay = new GUIContent(LoadTexture("mas_icon_play"), "Start playback");
				buttonStop = new GUIContent(LoadTexture("mas_icon_stop"), "Stop playback");

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
				basedOnEventName,
				basedOnFirstClip,
				basedOnSearch,
				clearClips
            }

			public AudioEvent audioEvent;
			public AudioEvent.Sound clipCollection;
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
					AudioSource source = EditorUtility.CreateGameObjectWithHideFlags("Audio preview", HideFlags.HideAndDontSave, typeof(AudioSource)).GetComponent<AudioSource>();
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

			// Not sure what this does
			serializedObject.Update();

			AudioEvent audioEvent = (AudioEvent)target;

			Space();

			// Audition-buttons
			GUILayout.BeginHorizontal();
			GUILayout.BeginHorizontal();
			GUI.enabled = AudioEventHasAnyClips(audioEvent);
			if (GUILayout.Button(content.buttonPlay, styles.buttonPlaybackControl)) {
				audioEvent.Audition(auditioningAudioSources, GetSpatialPannerWorldPosition(audioEvent.spatialSettings.maxDistance));
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
			audioEvent.cooldownSettings.enabled = EditorGUILayout.ToggleLeft("Cooldown time", audioEvent.cooldownSettings.enabled);

			if (audioEvent.cooldownSettings.enabled) {
				audioEvent.cooldownSettings.time = EditorGUILayout.FloatField(audioEvent.cooldownSettings.time, GUILayout.Width(50f));
				GUILayout.Label("seconds", GUILayout.Width(50f));
			}
			
			GUILayout.EndHorizontal();

			Space();

			// 3D settings
			GUILayout.BeginVertical(EditorStyles.helpBox);
			GUILayout.BeginHorizontal();
			audioEvent.spatialSettings.enabled = EditorGUILayout.ToggleLeft("3D spatialize", audioEvent.spatialSettings.enabled);
			if (audioEvent.spatialSettings.enabled) {
				GUILayout.FlexibleSpace();
				EditorGUILayout.DropdownButton(new GUIContent() { text = "No preset" }, FocusType.Keyboard, GUILayout.Width(120f));
			}
			GUILayout.EndHorizontal();

			if (audioEvent.spatialSettings.enabled) {
				SpatializeGUI(audioEvent.spatialSettings, serializedObject.FindProperty("spatialSettings"));
			}

			GUILayout.EndVertical();

			Space();

			// Clip collections
			EditorGUILayout.LabelField("Sounds");
			SerializedProperty soundsProp = serializedObject.FindProperty("sounds");
            for (int i = 0; i < soundsProp.arraySize; i++) {
				AudioEvent.Sound sound = audioEvent.sounds[i];

				GUILayout.BeginVertical(EditorStyles.helpBox);
				SerializedProperty soundProp = soundsProp.GetArrayElementAtIndex(i);

				GUILayout.BeginHorizontal();

				if (GUILayout.Button("...", GUILayout.Width(25f))) {
					GenericMenu menu = new GenericMenu();
					menu.AddItem(new GUIContent("Search..."), false, OnClipCollectionDropdownButton, new ClipCollectionDropdownSelection() {
						audioEvent = audioEvent,
						clipCollection = audioEvent.sounds[i],
						fetchType = ClipCollectionDropdownSelection.FetchType.basedOnSearch
					});
					menu.AddItem(new GUIContent("Fetch based on event name"), false, OnClipCollectionDropdownButton, new ClipCollectionDropdownSelection() {
						audioEvent = audioEvent,
						clipCollection = audioEvent.sounds[i],
						fetchType = ClipCollectionDropdownSelection.FetchType.basedOnEventName
					});
					menu.AddItem(new GUIContent("Fetch based on first clip"), false, OnClipCollectionDropdownButton, new ClipCollectionDropdownSelection() {
						audioEvent = audioEvent,
						clipCollection = audioEvent.sounds[i],
						fetchType = ClipCollectionDropdownSelection.FetchType.basedOnFirstClip
					});
					menu.AddSeparator("");
					menu.AddItem(new GUIContent("Clear clips"), false, OnClipCollectionDropdownButton, new ClipCollectionDropdownSelection() {
						audioEvent = audioEvent,
						clipCollection = audioEvent.sounds[i],
						fetchType = ClipCollectionDropdownSelection.FetchType.clearClips
					});
					menu.ShowAsContext();
				}

				/*EditorGUILayout.LabelField("Play", GUILayout.Width(30f));
				clipCollection.playTime = (AudioEvent.ClipCollection.PlayTime)EditorGUILayout.EnumPopup(clipCollection.playTime, GUILayout.Width(75f));*/
				EditorGUILayout.LabelField("Loop", GUILayout.Width(30f));
				sound.loop = EditorGUILayout.Toggle(sound.loop);
				//sound.chanceToPlay = EditorGUILayout.FloatField("Chance to play", sound.chanceToPlay);

				SerializedProperty chanceToPlayProp = soundProp.FindPropertyRelative("chanceToPlay");
				chanceToPlayProp.floatValue = Mathf.Clamp01(EditorGUILayout.IntField("Chance", Mathf.RoundToInt(chanceToPlayProp.floatValue * 100)) / 100f);
				GUILayout.Label("%", GUILayout.Width(25f));
				

				GUILayout.FlexibleSpace();

				if (GUILayout.Button("X", GUILayout.Width(25f))) {
					clipCollectionQueuedForDeletionIndex = i;
				}

				GUILayout.EndHorizontal();
				EditorGUI.indentLevel += 1;
				Color defaultColor = GUI.backgroundColor;
				GUI.backgroundColor = new Color(1f, 1f, 1f, 0.5f);
				EditorGUILayout.PropertyField(soundProp.FindPropertyRelative("clips"));
				GUI.backgroundColor = defaultColor;
				EditorGUI.indentLevel -= 1;
				GUILayout.EndVertical();
			}
			
			Space();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("+", GUILayout.Width(25f), GUILayout.Height(25f))) {
				audioEvent.sounds.Add(new AudioEvent.Sound());
				UpdateAuditioningAudioSources();
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
				audioEvent.sounds.RemoveAt(clipCollectionQueuedForDeletionIndex);
				clipCollectionQueuedForDeletionIndex = -1;
				UpdateAuditioningAudioSources();
            }

			// Apply any modified properties
			serializedObject.ApplyModifiedProperties();
		}

		#region Inspector GUI methods

		void SpatializeGUI(AudioEvent.SpatialSettings settings, SerializedProperty spatialSettingsProp) {
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
				CurveGraphLabelsGUI(rect, settings.maxDistance);
				GUI.contentColor = Color.white;

				BeginDraw(rect);

				DrawSpatialCurveGraphBackground(rect);

				DrawSpatialCurve(rect, styles.colCurveVolume, settings.volumeCurve, 0);
				DrawSpatialCurve(rect, styles.colCurveSpatialBlend, settings.spatialBlendCurve, 1);
				DrawSpatialCurve(rect, styles.colCurveSpread, settings.stereoSpreadCurve, 2);
				DrawSpatialCurve(rect, styles.colCurveReverbZoneMix, settings.reverbZoneMixCurve, 3);
				DrawListenerIndicator(rect, settings.maxDistance);

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
				SpatialPannerGUI(settings);
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
				//EditorGUILayout.MinMaxSlider(ref settings.minOffset, ref settings.maxOffset, 0f, 50f);
				GUILayout.EndHorizontal();
			}
		}

		void SpatialPannerGUI(AudioEvent.SpatialSettings settings) {
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
                case ClipCollectionDropdownSelection.FetchType.basedOnEventName:
					FetchClips(selection.clipCollection, target.name);
					break;
                case ClipCollectionDropdownSelection.FetchType.basedOnFirstClip:
					FetchClipsBasedOnFirstClip(selection.clipCollection);
					break;
                case ClipCollectionDropdownSelection.FetchType.basedOnSearch:
					AudioClipSearchWindow.Open((AudioEvent)target, selection.clipCollection);
                    break;
				case ClipCollectionDropdownSelection.FetchType.clearClips:
					selection.clipCollection.clips.Clear();
					break;
                default:
                    break;
            }
		}

		void FetchClipsBasedOnFirstClip(AudioEvent.Sound clipCollection) {
			if (clipCollection.clips == null)
				return;

			if (clipCollection.clips.Count == 0)
				return;

			if (clipCollection.clips[0] == null)
				return;

			string clipName = clipCollection.clips[0].name;
			for (int i = 0; i < 10; i++) {
				// Remove numbers from end of name
				if (LastCharacterInStringIsNumber(clipName)) {
					clipName = clipName.Substring(0, clipName.Length - 1);
				}
				clipName = clipName.Trim(); // Trim whitespace
			}

			FetchClips(clipCollection, clipName);
		}

		void FetchClips(AudioEvent.Sound clipCollection, string search) {
			string[] assetGUIDs = AssetDatabase.FindAssets($"t:AudioClip {search}");

			if (assetGUIDs.Length == 0) {
				Debug.Log("No matching clips found.");
				return;
			}

			// Clear clip-list in AudioEvent
			if (clipCollection.clips != null)
				clipCollection.clips.Clear();
			else
				clipCollection.clips = new List<AudioClip>();

			for (int i = 0; i < assetGUIDs.Length; i++) {
				string path = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);

				// Add clip to clips-list
				clipCollection.clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
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

		bool AudioEventHasAnyClips(AudioEvent audioEvent) {
			if (audioEvent.sounds == null)
				return false;
            for (int i = 0; i < audioEvent.sounds.Count; i++) {
				if (audioEvent.sounds[i].clips != null) {
					if (audioEvent.sounds[i].clips.Count > 0)
						return true;
                }
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