using UnityEngine;
using System.Collections.Generic;

namespace Orlo.Audio
{
    /// <summary>
    /// Central audio manager — handles music zones, ambient loops, and 3D sound events.
    /// Crossfades between music tracks when entering/leaving audio zones.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private AudioSource musicSourceA;
        private AudioSource musicSourceB;
        private AudioSource ambientSource;
        private bool usingSourceA = true;

        private float musicVolume = 0.7f;
        private float ambientVolume = 1.0f;
        private float crossfadeDuration = 2.0f;
        private float crossfadeTimer = 0f;
        private bool crossfading = false;

        private string currentMusicTrack = "";
        private string currentAmbientTrack = "";
        private string currentZoneId = "";

        // Pool of AudioSources for 3D sound events
        private List<AudioSource> sfxPool = new List<AudioSource>();
        private const int SFX_POOL_SIZE = 16;

        // Placeholder clip cache (in real build, loaded from asset bundles)
        private Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

        private static AudioManager instance;
        public static AudioManager Instance => instance;

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);

            // Create music sources (stereo, no spatial blend)
            musicSourceA = CreateAudioSource("MusicA", false);
            musicSourceA.loop = true;
            musicSourceA.volume = musicVolume;

            musicSourceB = CreateAudioSource("MusicB", false);
            musicSourceB.loop = true;
            musicSourceB.volume = 0f;

            ambientSource = CreateAudioSource("Ambient", false);
            ambientSource.loop = true;
            ambientSource.volume = ambientVolume;

            // Create SFX pool (3D spatial)
            for (int i = 0; i < SFX_POOL_SIZE; i++)
            {
                var src = CreateAudioSource($"SFX_{i}", true);
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1f;
                src.maxDistance = 50f;
                sfxPool.Add(src);
            }
        }

        /// <summary>
        /// Called when server sends AudioZoneEnter
        /// </summary>
        public void OnAudioZoneEnter(string zoneId, string musicTrack, string ambientTrack,
                                      float musicVol, float ambientVol)
        {
            currentZoneId = zoneId;
            musicVolume = musicVol;
            ambientVolume = ambientVol;

            if (musicTrack != currentMusicTrack)
            {
                CrossfadeMusic(musicTrack);
                currentMusicTrack = musicTrack;
            }

            if (ambientTrack != currentAmbientTrack)
            {
                var clip = LoadClip(ambientTrack);
                if (clip != null)
                {
                    ambientSource.clip = clip;
                    ambientSource.volume = ambientVolume;
                    ambientSource.Play();
                }
                currentAmbientTrack = ambientTrack;
            }
        }

        /// <summary>
        /// Called when server sends AudioZoneLeave
        /// </summary>
        public void OnAudioZoneLeave(string zoneId)
        {
            if (zoneId != currentZoneId) return;
            currentZoneId = "";

            // Fade out to silence
            CrossfadeMusic("");
            ambientSource.volume = 0f;
            currentMusicTrack = "";
            currentAmbientTrack = "";
        }

        /// <summary>
        /// Called when server sends SoundEvent — play a 3D positioned sound
        /// </summary>
        public void PlaySoundAt(string soundId, Vector3 position, float volume, float radius)
        {
            var source = GetFreeSfxSource();
            if (source == null) return;

            var clip = LoadClip(soundId);
            if (clip == null) return;

            source.transform.position = position;
            source.maxDistance = radius;
            source.volume = volume;
            source.clip = clip;
            source.Play();
        }

        /// <summary>
        /// Play a UI sound (non-spatial)
        /// </summary>
        public void PlayUISound(string soundId, float volume = 1f)
        {
            var source = GetFreeSfxSource();
            if (source == null) return;

            var clip = LoadClip(soundId);
            if (clip == null) return;

            source.spatialBlend = 0f;
            source.volume = volume;
            source.clip = clip;
            source.Play();
        }

        private void Update()
        {
            if (!crossfading) return;

            crossfadeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(crossfadeTimer / crossfadeDuration);

            var fadeOut = usingSourceA ? musicSourceA : musicSourceB;
            var fadeIn = usingSourceA ? musicSourceB : musicSourceA;

            fadeOut.volume = Mathf.Lerp(musicVolume, 0f, t);
            fadeIn.volume = Mathf.Lerp(0f, musicVolume, t);

            if (t >= 1f)
            {
                crossfading = false;
                fadeOut.Stop();
                usingSourceA = !usingSourceA;
            }
        }

        private void CrossfadeMusic(string newTrack)
        {
            var incoming = usingSourceA ? musicSourceB : musicSourceA;

            if (string.IsNullOrEmpty(newTrack))
            {
                // Fade to silence
                crossfading = true;
                crossfadeTimer = 0f;
                incoming.clip = null;
                return;
            }

            var clip = LoadClip(newTrack);
            if (clip == null) return;

            incoming.clip = clip;
            incoming.volume = 0f;
            incoming.Play();

            crossfading = true;
            crossfadeTimer = 0f;
        }

        private AudioSource GetFreeSfxSource()
        {
            foreach (var src in sfxPool)
            {
                if (!src.isPlaying) return src;
            }
            return null; // All busy
        }

        private AudioSource CreateAudioSource(string name, bool spatial)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = spatial ? 1f : 0f;
            return src;
        }

        private AudioClip LoadClip(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return null;
            if (clipCache.TryGetValue(assetId, out var cached)) return cached;

            // In production: load from AssetBundle
            // For now: try Resources.Load as fallback
            var clip = Resources.Load<AudioClip>($"Audio/{assetId}");
            if (clip != null)
            {
                clipCache[assetId] = clip;
            }
            return clip;
        }
    }
}
