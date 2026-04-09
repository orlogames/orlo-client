namespace Orlo.UI.TMD
{
    /// <summary>
    /// Visual parameters for each TMD upgrade tier.
    /// Controls scanline intensity, chromatic aberration, noise, glitch frequency, dot-grid density.
    /// </summary>
    [System.Serializable]
    public struct TMDTierSettings
    {
        /// <summary>Scanline contrast [0=invisible, 1=heavy]</summary>
        public float ScanlineIntensity;
        /// <summary>Scanline scroll speed in UV/sec</summary>
        public float ScanlineSpeed;
        /// <summary>Chromatic aberration offset in pixels</summary>
        public float ChromaticAberration;
        /// <summary>Noise grain opacity [0..1]</summary>
        public float NoiseIntensity;
        /// <summary>Seconds between full-frame glitch events. 0=disabled.</summary>
        public float GlitchInterval;
        /// <summary>Glow multiplier [0..2]</summary>
        public float GlowMultiplier;
        /// <summary>Dot-grid spacing in pixels</summary>
        public float DotGridSpacing;
        /// <summary>Text jitter in pixels per frame</summary>
        public float TextJitter;
        /// <summary>Whether dot-grid animates (breathing pattern)</summary>
        public bool DotGridAnimates;
        /// <summary>Whether secondary glow halo is shown</summary>
        public bool SecondaryHalo;
        /// <summary>Whether edge particle effects are active</summary>
        public bool EdgeParticles;

        /// <summary>Tier 1 — Salvaged (starter TMD)</summary>
        public static TMDTierSettings Tier1 => new TMDTierSettings
        {
            ScanlineIntensity = 0.35f,
            ScanlineSpeed = 0.4f,
            ChromaticAberration = 3f,
            NoiseIntensity = 0.15f,
            GlitchInterval = 25f,
            GlowMultiplier = 0.6f,
            DotGridSpacing = 16f,
            TextJitter = 0.5f,
            DotGridAnimates = false,
            SecondaryHalo = false,
            EdgeParticles = false,
        };

        /// <summary>Tier 2 — Standard</summary>
        public static TMDTierSettings Tier2 => new TMDTierSettings
        {
            ScanlineIntensity = 0.2f,
            ScanlineSpeed = 0.6f,
            ChromaticAberration = 2f,
            NoiseIntensity = 0.10f,
            GlitchInterval = 50f,
            GlowMultiplier = 0.8f,
            DotGridSpacing = 12f,
            TextJitter = 0f,
            DotGridAnimates = false,
            SecondaryHalo = false,
            EdgeParticles = false,
        };

        /// <summary>Tier 3 — Military</summary>
        public static TMDTierSettings Tier3 => new TMDTierSettings
        {
            ScanlineIntensity = 0.1f,
            ScanlineSpeed = 0.8f,
            ChromaticAberration = 1f,
            NoiseIntensity = 0.05f,
            GlitchInterval = 100f,
            GlowMultiplier = 1.0f,
            DotGridSpacing = 8f,
            TextJitter = 0f,
            DotGridAnimates = false,
            SecondaryHalo = false,
            EdgeParticles = false,
        };

        /// <summary>Tier 4 — Precursor-Enhanced</summary>
        public static TMDTierSettings Tier4 => new TMDTierSettings
        {
            ScanlineIntensity = 0.03f,
            ScanlineSpeed = 1.0f,
            ChromaticAberration = 0f,
            NoiseIntensity = 0.02f,
            GlitchInterval = 0f,
            GlowMultiplier = 1.3f,
            DotGridSpacing = 4f,
            TextJitter = 0f,
            DotGridAnimates = false,
            SecondaryHalo = true,
            EdgeParticles = false,
        };

        /// <summary>Tier 5 — Awakened (hidden class)</summary>
        public static TMDTierSettings Tier5 => new TMDTierSettings
        {
            ScanlineIntensity = 0f,
            ScanlineSpeed = 0f,
            ChromaticAberration = 0f,
            NoiseIntensity = 0f,
            GlitchInterval = 0f,
            GlowMultiplier = 1.6f,
            DotGridSpacing = 4f,
            TextJitter = 0f,
            DotGridAnimates = true,
            SecondaryHalo = true,
            EdgeParticles = true,
        };

        public static TMDTierSettings ForTier(int tier)
        {
            switch (tier)
            {
                case 1: return Tier1;
                case 2: return Tier2;
                case 3: return Tier3;
                case 4: return Tier4;
                case 5: return Tier5;
                default: return tier < 1 ? Tier1 : Tier5;
            }
        }
    }
}
