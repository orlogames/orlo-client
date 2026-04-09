using UnityEngine;

namespace Orlo.UI.TMD
{
    /// <summary>
    /// Procedural audio for TMD holographic UI interactions.
    /// Generates all sound effects at runtime using sine wave synthesis — no audio assets needed.
    /// Singleton with cached AudioClips for each sound type.
    /// </summary>
    public class TMDSoundDesigner : MonoBehaviour
    {
        public static TMDSoundDesigner Instance { get; private set; }

        private const int SampleRate = 44100;

        private AudioSource _sfxSource;
        private AudioSource _humSource;

        // Cached clips
        private AudioClip _panelOpen;
        private AudioClip _panelClose;
        private AudioClip _tabSwitch;
        private AudioClip _select;
        private AudioClip _error;
        private AudioClip _hover;
        private AudioClip _precursorHumClip;

        private float _humTargetVolume;
        private float _humCurrentVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // SFX source — short one-shot sounds
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f; // 2D

            // Hum source — continuous looping subsonic throb
            _humSource = gameObject.AddComponent<AudioSource>();
            _humSource.playOnAwake = false;
            _humSource.spatialBlend = 0f;
            _humSource.loop = true;
            _humSource.volume = 0f;

            GenerateAllClips();
        }

        private void Update()
        {
            // Smooth hum volume transitions
            _humCurrentVolume = Mathf.MoveTowards(_humCurrentVolume, _humTargetVolume, Time.deltaTime * 2f);
            _humSource.volume = _humCurrentVolume;

            if (_humCurrentVolume > 0.001f && !_humSource.isPlaying)
                _humSource.Play();
            else if (_humCurrentVolume <= 0.001f && _humSource.isPlaying)
                _humSource.Stop();
        }

        /// <summary>Rising frequency sweep (200Hz to 800Hz over 0.15s).</summary>
        public void PlayPanelOpen()
        {
            if (_panelOpen != null)
                _sfxSource.PlayOneShot(_panelOpen, 0.4f);
        }

        /// <summary>Falling frequency sweep (800Hz to 200Hz over 0.1s).</summary>
        public void PlayPanelClose()
        {
            if (_panelClose != null)
                _sfxSource.PlayOneShot(_panelClose, 0.35f);
        }

        /// <summary>Short chirp (1200Hz, 0.05s) with slight random pitch variation.</summary>
        public void PlayTabSwitch()
        {
            if (_tabSwitch != null)
            {
                _sfxSource.pitch = 1f + Random.Range(-0.05f, 0.05f);
                _sfxSource.PlayOneShot(_tabSwitch, 0.3f);
                _sfxSource.pitch = 1f;
            }
        }

        /// <summary>Two-note harmony (440Hz + 660Hz, 0.1s).</summary>
        public void PlaySelect()
        {
            if (_select != null)
                _sfxSource.PlayOneShot(_select, 0.35f);
        }

        /// <summary>Dissonant buzz (180Hz + 193Hz, 0.15s).</summary>
        public void PlayError()
        {
            if (_error != null)
                _sfxSource.PlayOneShot(_error, 0.4f);
        }

        /// <summary>Subtle tick (2000Hz, 0.02s, very low volume).</summary>
        public void PlayHover()
        {
            if (_hover != null)
                _sfxSource.PlayOneShot(_hover, 0.15f);
        }

        /// <summary>
        /// Continuous low subsonic throb (35Hz). Intensity controls volume.
        /// Called every frame by PrecursorDetector.
        /// </summary>
        public void PrecursorHum(float intensity)
        {
            _humTargetVolume = Mathf.Clamp01(intensity) * 0.5f;
        }

        private void GenerateAllClips()
        {
            _panelOpen = GenerateSweep("TMD_PanelOpen", 200f, 800f, 0.15f);
            _panelClose = GenerateSweep("TMD_PanelClose", 800f, 200f, 0.1f);
            _tabSwitch = GenerateTone("TMD_TabSwitch", 1200f, 0.05f);
            _select = GenerateHarmony("TMD_Select", 440f, 660f, 0.1f);
            _error = GenerateHarmony("TMD_Error", 180f, 193f, 0.15f);
            _hover = GenerateTone("TMD_Hover", 2000f, 0.02f);
            _precursorHumClip = GenerateHum("TMD_PrecursorHum", 35f, 2f);

            if (_precursorHumClip != null)
            {
                _humSource.clip = _precursorHumClip;
            }
        }

        /// <summary>Generate a frequency sweep (linear interpolation from startHz to endHz).</summary>
        private AudioClip GenerateSweep(string name, float startHz, float endHz, float duration)
        {
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = Mathf.Lerp(startHz, endHz, t);

                // Amplitude envelope: quick attack, natural decay
                float envelope = (1f - t) * Mathf.Min(t * 20f, 1f);

                phase += freq / SampleRate;
                samples[i] = Mathf.Sin(phase * 2f * Mathf.PI) * envelope;
            }

            return CreateClip(name, samples);
        }

        /// <summary>Generate a single-frequency tone with attack/release envelope.</summary>
        private AudioClip GenerateTone(string name, float hz, float duration)
        {
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float envelope = (1f - t) * Mathf.Min(t * 40f, 1f);
                samples[i] = Mathf.Sin(i * hz * 2f * Mathf.PI / SampleRate) * envelope;
            }

            return CreateClip(name, samples);
        }

        /// <summary>Generate a two-frequency harmony (additive synthesis).</summary>
        private AudioClip GenerateHarmony(string name, float hz1, float hz2, float duration)
        {
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float envelope = (1f - t) * Mathf.Min(t * 30f, 1f);
                float s1 = Mathf.Sin(i * hz1 * 2f * Mathf.PI / SampleRate);
                float s2 = Mathf.Sin(i * hz2 * 2f * Mathf.PI / SampleRate);
                samples[i] = (s1 + s2) * 0.5f * envelope;
            }

            return CreateClip(name, samples);
        }

        /// <summary>Generate a looping low-frequency hum for Precursor proximity.</summary>
        private AudioClip GenerateHum(string name, float hz, float duration)
        {
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                // Base sine + subtle harmonic for presence
                float s1 = Mathf.Sin(t * hz * 2f * Mathf.PI);
                float s2 = Mathf.Sin(t * hz * 2f * 2f * Mathf.PI) * 0.3f;
                // Slow amplitude modulation for throb feel
                float modulation = 0.7f + 0.3f * Mathf.Sin(t * 1.5f * 2f * Mathf.PI);
                samples[i] = (s1 + s2) * modulation * 0.6f;
            }

            return CreateClip(name, samples);
        }

        private AudioClip CreateClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
