using System.Collections;
using UnityEngine;

namespace Orlo.UI.Lobby
{
    public class LobbyAudio : MonoBehaviour
    {
        public static LobbyAudio Instance { get; private set; }

        public float MusicVolume { get; private set; } = 0.3f;
        public float AmbienceVolume { get; private set; } = 0.15f;

        private AudioSource _musicSource;
        private AudioSource _ambienceSource;
        private Coroutine _fadeCoroutine;

        private const int SampleRate = 44100;
        private const int DroneDuration = 16;
        private const float BaseFreq = 55f;
        private const float HarmonicFreq = 82.5f;
        private const float BaseAmp = 0.05f;
        private const float HarmonicAmp = 0.03f;
        private const float ModPeriod = 8f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.volume = MusicVolume;

            _ambienceSource = gameObject.AddComponent<AudioSource>();
            _ambienceSource.loop = true;
            _ambienceSource.playOnAwake = false;
            _ambienceSource.volume = AmbienceVolume;
            _ambienceSource.clip = GenerateDroneClip();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private AudioClip GenerateDroneClip()
        {
            int sampleCount = SampleRate * DroneDuration;
            float[] samples = new float[sampleCount];
            float twoPi = Mathf.PI * 2f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float modulation = 1f + 0.3f * Mathf.Sin(twoPi * t / ModPeriod);
                float baseSine = Mathf.Sin(twoPi * BaseFreq * t) * BaseAmp;
                float harmonic = Mathf.Sin(twoPi * HarmonicFreq * t) * HarmonicAmp;
                samples[i] = (baseSine + harmonic) * modulation;
            }

            AudioClip clip = AudioClip.Create("LobbyDrone", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void Play()
        {
            if (_fadeCoroutine != null) { StopCoroutine(_fadeCoroutine); _fadeCoroutine = null; }
            _ambienceSource.volume = AmbienceVolume;
            if (!_ambienceSource.isPlaying) _ambienceSource.Play();
        }

        public void Stop()
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOut());
        }

        public void SetVolumes(float music, float ambience)
        {
            MusicVolume = Mathf.Clamp01(music);
            AmbienceVolume = Mathf.Clamp01(ambience);
            _musicSource.volume = MusicVolume;
            _ambienceSource.volume = AmbienceVolume;
        }

        private IEnumerator FadeOut()
        {
            float startMusic = _musicSource.volume;
            float startAmbience = _ambienceSource.volume;
            float elapsed = 0f;
            const float duration = 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                _musicSource.volume = Mathf.Lerp(startMusic, 0f, t);
                _ambienceSource.volume = Mathf.Lerp(startAmbience, 0f, t);
                yield return null;
            }

            _musicSource.Stop();
            _ambienceSource.Stop();
            _musicSource.volume = MusicVolume;
            _ambienceSource.volume = AmbienceVolume;
            _fadeCoroutine = null;
        }
    }
}
