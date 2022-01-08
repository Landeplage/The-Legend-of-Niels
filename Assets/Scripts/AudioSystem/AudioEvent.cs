using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace MordiAudio
{
    [System.Serializable, CreateAssetMenu(fileName = "AudioEvent", menuName = "AudioEvents/AudioEvent")]
    public class AudioEvent : ScriptableObject
    {
        [System.Serializable]
        public class RandomizableFloat
        {
            public float min;
            public float max;

            public float GetRandom() {
                return Random.Range(min, max);
            }
        }

        [System.Serializable]
        public class CooldownSettings
        {
            public bool enabled;
            public float time;
        }

        [System.Serializable]
        public class SpatialSettings
        {
            [System.Serializable]
            public class RandomPositionSettings
            {
                public enum Type
                {
                    off,
                    cubic,
                    spherical
                }

                public Type type;
                public RandomizableFloat offset;

                public Vector3 GetRandomOffset() {
                    Vector3 _offset = Vector3.zero;
                    switch (type) {
                        case Type.cubic:
                            _offset.x = offset.GetRandom() - offset.max * 0.5f;
                            _offset.y = offset.GetRandom() - offset.max * 0.5f;
                            _offset.z = offset.GetRandom() - offset.max * 0.5f;
                            break;
                        case Type.spherical:
                            Quaternion rotation = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                            _offset = rotation * Vector3.forward * offset.GetRandom();
                            break;
                        default:
                            break;
                    }
                    return _offset;
                }
            }
            public RandomPositionSettings randomPositionSettings;
            public bool enabled;
            public float maxDistance = 50f;
            public AnimationCurve volumeCurve, spatialBlendCurve, stereoSpreadCurve, reverbZoneMixCurve;

            public SpatialSettings() {
                // Set default values
                Keyframe[] keyframes;

                keyframes = new Keyframe[2];
                keyframes[0] = new Keyframe(0f, 1f, 0f, -3f);
                keyframes[1] = new Keyframe(1f, 0f);
                volumeCurve = new AnimationCurve(keyframes);

                keyframes = new Keyframe[3];
                keyframes[0] = new Keyframe(0f, 0f, 0f, 5f);
                keyframes[1] = new Keyframe(0.1f, 1f);
                keyframes[2] = new Keyframe(1f, 1f);
                spatialBlendCurve = new AnimationCurve(keyframes);

                stereoSpreadCurve = AnimationCurve.Constant(0f, 1f, 0f);
                reverbZoneMixCurve = AnimationCurve.Constant(0f, 1f, 1f);
            }
        }

        [System.Serializable]
        public class Sound
        {
            public class Fade
            {
                public float time;
                public float curve;
            }
            public Fade fadeIn, fadeOut;

            public enum PlayTime
            {
                OnStart,
                OnStop
            }
            public PlayTime playTime;
            
            public bool loop;
            public float chanceToPlay = 1f;
            public List<AudioClip> clips;

            [HideInInspector] public int clipIndex;
            [HideInInspector] public int lastPlayedIndex;

            public void RandomizeIndex() { RandomizeIndex(0, clips.Count); }

            /// <summary>
            /// Randomize the next audio clip index from min (inclusive) to max (exclusive).
            /// </summary>
            /// <param name="min">Index from (inclusive)</param>
            /// <param name="max">Index to (exclusive)</param>
            public void RandomizeIndex(int min, int max) {
                int newIndex;
                do {
                    newIndex = Random.Range(min, max);
                } while (clips.Count > 1 && newIndex == lastPlayedIndex);

                clipIndex = newIndex;
            }
        }

        public RandomizableFloat volume = new RandomizableFloat() { min = 1f, max = 1f };
        public RandomizableFloat pan = new RandomizableFloat() { min = 0f, max = 0f };
        public RandomizableFloat pitch = new RandomizableFloat() { min = 1f, max = 1f };
        public CooldownSettings cooldownSettings;
        public AudioMixerGroup outputGroup;
        public SpatialSettings spatialSettings;
        public List<Sound> sounds;

        private float lastPlayedTime, nextAvailablePlayTime; // Used for cooldown

        static GameObject audioSourcePool;

        private void UpdateAudioSourceProperties(AudioSource src, Sound clipCollection) {

            if (clipCollection.clipIndex >= clipCollection.clips.Count)
                clipCollection.RandomizeIndex();

            src.clip = clipCollection.clips[clipCollection.clipIndex];
            src.loop = clipCollection.loop;

            src.volume = Mathf.Clamp01(volume.GetRandom());
            src.panStereo = Mathf.Clamp(pan.GetRandom(), -1f, 1f);
            src.outputAudioMixerGroup = outputGroup;
            src.pitch = pitch.GetRandom();
            src.playOnAwake = false;
            src.spatialBlend = spatialSettings.enabled ? 1f : 0f;

            // 3D sound settings
            if (spatialSettings.enabled) {
                src.maxDistance = spatialSettings.maxDistance;
                src.rolloffMode = AudioRolloffMode.Custom;
                src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, spatialSettings.volumeCurve);
                src.SetCustomCurve(AudioSourceCurveType.SpatialBlend, spatialSettings.spatialBlendCurve);
                src.SetCustomCurve(AudioSourceCurveType.Spread, spatialSettings.stereoSpreadCurve);
                src.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix, spatialSettings.reverbZoneMixCurve);
            }
        }

        private AudioSource GetAvailableSourceFromPool() {
            if (audioSourcePool == null) {
                audioSourcePool = GameObject.Find("AudioSourcePool");
                if (audioSourcePool == null) {
                    Debug.LogError($"No AudioSourcePool was found in the scene for audio event: {name}.");
                    return null;
                }
            }

            for (int i = 0; i < audioSourcePool.transform.childCount; i++) {
                AudioSource src = audioSourcePool.transform.GetChild(i).GetComponent<AudioSource>();
                if (!src.isPlaying)
                    return src;
            }

            // If none are available, pick random one
            return audioSourcePool.transform.GetChild(Random.Range(0, audioSourcePool.transform.childCount)).GetComponent<AudioSource>();
        }

        #region Validation methods

        bool ValidateCooldown() {
            return lastPlayedTime <= nextAvailablePlayTime;
        }

        bool ValidateAudioSource(AudioSource source) {
            if (source == null) {
                Debug.Log($"AudioEvent: Tried to play {name} with source equal to null.");
                return false;
            }

            // Check if AudioSource is active
            if (!source.gameObject.activeSelf) {
                Debug.Log("Sound did not play because the source gameobject was not active.");
                return false;
            }

            return true;
        }

        bool ValidateClipCollection(Sound clipCollection) {
            if (clipCollection.clips.Count == 0)
                return false;

            if (clipCollection.chanceToPlay < Random.Range(0f, 1f))
                return false;

            return true;
        }

        #endregion

        #region Playback methods

        /// <summary>
        /// Used for auditioning in the inspector.
        /// </summary>
        public void Audition(List<AudioSource> audioSources, Vector3 position) {
            if (!ValidateCooldown()) {
                return;
            }

            for (int i = 0; i < sounds.Count; i++) {
                if (audioSources.Count < i)
                    break;

                Play(audioSources[i], sounds[i], position);
            }

            lastPlayedTime = Time.time;
            nextAvailablePlayTime = lastPlayedTime + cooldownSettings.time;
        }

        public virtual void Play(Vector3 position) {
            if (!ValidateCooldown()) {
                return;
            }

            for (int i = 0; i < sounds.Count; i++) {
                Play(GetAvailableSourceFromPool(), sounds[i], position);
            }

            lastPlayedTime = Time.time;
            nextAvailablePlayTime = lastPlayedTime + cooldownSettings.time;
        }

        public virtual void Play(AudioSource source, Sound clipCollection, Vector3 position) {
            if (!ValidateClipCollection(clipCollection))
                return;

            if (!ValidateAudioSource(source))
                return;

            UpdateAudioSourceProperties(source, clipCollection);

            // Move AudioSource to position
            if (spatialSettings.enabled)
                source.transform.position = position + spatialSettings.randomPositionSettings.GetRandomOffset();

            // Play
            source.Play();

            clipCollection.lastPlayedIndex = clipCollection.clipIndex;
            clipCollection.RandomizeIndex();
        }

        public void Stop() {
            // TODO: Implement
        }

        public void StopImmediately() {
            // TODO: Implement
        }

        #endregion
    }
}