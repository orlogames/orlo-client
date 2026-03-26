using System;
using System.Collections.Generic;
using UnityEngine;
using ProtoCharacter = Orlo.Proto.Character;

namespace Orlo.UI.CharacterCreation
{
    /// <summary>
    /// Client-side data class mirroring the expanded CharacterAppearance proto.
    /// All float sliders are 0-1 range. Colors stored as Unity Color.
    /// </summary>
    [Serializable]
    public class AppearanceData
    {
        // ─── Identity (legacy fields) ──────────────────────────────────────
        public int Gender = 0;        // 0=Male, 1=Female
        public int Race = 0;          // 0=Human, 1=Sylvari, 2=Korathi, 3=Ashborn
        public float Height = 0.5f;
        public float Build = 0.5f;
        public float FaceShape = 0.5f;
        public float JawWidth = 0.5f;
        public float NoseSize = 0.5f;
        public float EarSize = 0.5f;
        public int FacialMarking = 0;

        // Legacy enums (kept for backward compat on proto)
        public int EyeColorLegacy = 0;
        public int HairStyleLegacy = 0;
        public int HairColorLegacy = 1;
        public int SkinToneLegacy = 2;

        // ─── Face Blend Shapes (26 floats, 0-1) ───────────────────────────
        // Eyes group
        public float EyeSpacing = 0.5f;
        public float EyeDepth = 0.5f;
        public float EyeHeight = 0.5f;
        public float EyeWidth = 0.5f;
        public float EyeTilt = 0.5f;
        public float BrowRidge = 0.5f;

        // Nose group
        public float NoseBridgeWidth = 0.5f;
        public float NoseBridgeHeight = 0.5f;
        public float NoseTipWidth = 0.5f;
        public float NoseTipHeight = 0.5f;
        public float NoseNostrilFlare = 0.5f;

        // Mouth group
        public float LipFullnessUpper = 0.5f;
        public float LipFullnessLower = 0.5f;
        public float LipWidth = 0.5f;

        // Jaw group
        public float ChinHeight = 0.5f;
        public float ChinWidth = 0.5f;
        public float ChinDepth = 0.5f;
        public float JawAngle = 0.5f;

        // Cheeks group
        public float CheekboneHeight = 0.5f;
        public float CheekboneWidth = 0.5f;

        // Forehead group
        public float ForeheadSlope = 0.5f;
        public float ForeheadHeight = 0.5f;
        public float BrowHeight = 0.5f;

        // Other group
        public float TempleWidth = 0.5f;
        public float CrownHeight = 0.5f;
        public float JawRoundness = 0.5f;

        // ─── Body Morphs ───────────────────────────────────────────────────
        public float ShoulderWidth = 0.5f;
        public float ChestDepth = 0.5f;
        public float ArmLength = 0.5f;
        public float ArmThickness = 0.5f;
        public float HipWidth = 0.5f;
        public float WaistWidth = 0.5f;
        public float LegLength = 0.5f;
        public float LegThickness = 0.5f;
        public float TorsoLength = 0.5f;
        public float MuscleDefinition = 0.5f;
        public float BodyFat = 0.5f;

        // ─── Skin ──────────────────────────────────────────────────────────
        public Color SkinColor = new Color(0.85f, 0.7f, 0.55f);
        public float FreckleDensity = 0f;
        public float FreckleSize = 0.3f;
        public Color FreckleColor = new Color(0.6f, 0.4f, 0.3f);
        public float Aging = 0f;
        public float Roughness = 0.3f;
        public float SkinTextureVariation = 0f;

        // ─── Hair ──────────────────────────────────────────────────────────
        public int HairStyle = 0;         // Maps to HairStyle enum (0-7)
        public float HairLength = 0.5f;
        public float HairThickness = 0.5f;
        public float HairCurl = 0f;
        public Color HairColor = new Color(0.2f, 0.15f, 0.1f);
        public Color HairHighlightColor = new Color(0.4f, 0.3f, 0.2f);
        public int FacialHairStyle = 0;   // 0 = none
        public float FacialHairLength = 0f;

        // ─── Eyes ──────────────────────────────────────────────────────────
        public Color LeftEyeColor = new Color(0.3f, 0.5f, 0.2f);
        public Color RightEyeColor = new Color(0.3f, 0.5f, 0.2f);
        public bool MatchEyes = true;
        public float IrisSize = 0.5f;
        public float PupilSize = 0.5f;
        public float EyeShapeSlider = 0.5f;

        // ─── Decals (tattoos/markings) ─────────────────────────────────────
        public List<DecalEntry> Decals = new List<DecalEntry>();
        public const int MaxDecals = 10;

        // ─── Race Features ─────────────────────────────────────────────────
        public uint RaceFeatureSetId = 0;
        public List<float> RaceFeatureValues = new List<float>();

        // ─── Name & Skill (stored here for convenience) ────────────────────
        public string FirstName = "";
        public string LastName = "";
        public int SelectedSkill = -1;

        // ─── Decal entry ───────────────────────────────────────────────────
        [Serializable]
        public class DecalEntry
        {
            public uint DecalId;
            public int BodyRegion; // Maps to BodyRegion enum
            public float PositionU = 0.5f;
            public float PositionV = 0.5f;
            public float Rotation = 0f;
            public float Scale = 1f;
            public Color Tint = Color.white;

            public DecalEntry Clone()
            {
                return new DecalEntry
                {
                    DecalId = DecalId,
                    BodyRegion = BodyRegion,
                    PositionU = PositionU,
                    PositionV = PositionV,
                    Rotation = Rotation,
                    Scale = Scale,
                    Tint = Tint
                };
            }
        }

        // ─── Methods ───────────────────────────────────────────────────────

        /// <summary>
        /// Convert to protobuf CharacterAppearance.
        /// </summary>
        public ProtoCharacter.CharacterAppearance ToProto()
        {
            var proto = new ProtoCharacter.CharacterAppearance
            {
                Gender = (ProtoCharacter.Gender)Gender,
                Race = (ProtoCharacter.Race)Race,
                Height = Height,
                Build = Build,
                EyeColor = (ProtoCharacter.EyeColor)EyeColorLegacy,
                HairStyle = (ProtoCharacter.HairStyle)HairStyleLegacy,
                HairColor = (ProtoCharacter.HairColor)HairColorLegacy,
                SkinTone = (ProtoCharacter.SkinTone)SkinToneLegacy,
                FaceShape = FaceShape,
                JawWidth = JawWidth,
                NoseSize = NoseSize,
                EarSize = EarSize,
                FacialMarking = (uint)FacialMarking,

                FaceBlendShapes = new ProtoCharacter.FaceBlendShapes
                {
                    CheekboneHeight = CheekboneHeight,
                    CheekboneWidth = CheekboneWidth,
                    BrowRidge = BrowRidge,
                    BrowHeight = BrowHeight,
                    ChinHeight = ChinHeight,
                    ChinWidth = ChinWidth,
                    ChinDepth = ChinDepth,
                    LipFullnessUpper = LipFullnessUpper,
                    LipFullnessLower = LipFullnessLower,
                    LipWidth = LipWidth,
                    NoseBridgeWidth = NoseBridgeWidth,
                    NoseBridgeHeight = NoseBridgeHeight,
                    NoseTipWidth = NoseTipWidth,
                    NoseTipHeight = NoseTipHeight,
                    NoseNostrilFlare = NoseNostrilFlare,
                    EyeSpacing = EyeSpacing,
                    EyeDepth = EyeDepth,
                    EyeHeight = EyeHeight,
                    EyeWidth = EyeWidth,
                    EyeTilt = EyeTilt,
                    ForeheadSlope = ForeheadSlope,
                    ForeheadHeight = ForeheadHeight,
                    JawAngle = JawAngle,
                    JawRoundness = JawRoundness,
                    TempleWidth = TempleWidth,
                    CrownHeight = CrownHeight
                },

                BodyMorphs = new ProtoCharacter.BodyMorphs
                {
                    ShoulderWidth = ShoulderWidth,
                    TorsoLength = TorsoLength,
                    ArmLength = ArmLength,
                    LegLength = LegLength,
                    ArmThickness = ArmThickness,
                    LegThickness = LegThickness,
                    MuscleDefinition = MuscleDefinition,
                    BodyFat = BodyFat,
                    HipWidth = HipWidth,
                    WaistWidth = WaistWidth,
                    ChestDepth = ChestDepth
                },

                SkinDetail = new ProtoCharacter.SkinDetail
                {
                    SkinTextureVariation = SkinTextureVariation,
                    FreckleDensity = FreckleDensity,
                    FreckleSize = FreckleSize,
                    Aging = Aging,
                    Roughness = Roughness,
                    FreckleColor = ColorToProto(FreckleColor)
                },

                HairParams = new ProtoCharacter.HairParams
                {
                    BaseStyle = (ProtoCharacter.HairStyle)HairStyle,
                    Length = HairLength,
                    Thickness = HairThickness,
                    CurlAmount = HairCurl,
                    PrimaryColor = ColorToProto(HairColor),
                    HighlightColor = ColorToProto(HairHighlightColor),
                    FacialHairStyle = (uint)FacialHairStyle,
                    FacialHairLength = FacialHairLength
                },

                EyeDetail = new ProtoCharacter.EyeDetail
                {
                    LeftIrisColor = ColorToProto(LeftEyeColor),
                    RightIrisColor = ColorToProto(MatchEyes ? LeftEyeColor : RightEyeColor),
                    IrisSize = IrisSize,
                    PupilSize = PupilSize,
                    EyeShape = EyeShapeSlider
                },

                RaceFeatures = new ProtoCharacter.RaceFeatures
                {
                    FeatureSetId = RaceFeatureSetId
                }
            };

            // Add race feature values
            foreach (var v in RaceFeatureValues)
                proto.RaceFeatures.FeatureValues.Add(v);

            // Add decals
            foreach (var d in Decals)
            {
                proto.BodyDecals.Add(new ProtoCharacter.BodyDecal
                {
                    DecalId = d.DecalId,
                    BodyRegion = (ProtoCharacter.BodyRegion)d.BodyRegion,
                    PositionU = d.PositionU,
                    PositionV = d.PositionV,
                    Rotation = d.Rotation,
                    Scale = d.Scale,
                    Tint = ColorToProto(d.Tint)
                });
            }

            return proto;
        }

        /// <summary>
        /// Populate this data from a protobuf CharacterAppearance.
        /// </summary>
        public void FromProto(ProtoCharacter.CharacterAppearance proto)
        {
            if (proto == null) return;

            Gender = (int)proto.Gender;
            Race = (int)proto.Race;
            Height = proto.Height;
            Build = proto.Build;
            EyeColorLegacy = (int)proto.EyeColor;
            HairStyleLegacy = (int)proto.HairStyle;
            HairColorLegacy = (int)proto.HairColor;
            SkinToneLegacy = (int)proto.SkinTone;
            FaceShape = proto.FaceShape;
            JawWidth = proto.JawWidth;
            NoseSize = proto.NoseSize;
            EarSize = proto.EarSize;
            FacialMarking = (int)proto.FacialMarking;

            if (proto.FaceBlendShapes != null)
            {
                var f = proto.FaceBlendShapes;
                CheekboneHeight = f.CheekboneHeight;
                CheekboneWidth = f.CheekboneWidth;
                BrowRidge = f.BrowRidge;
                BrowHeight = f.BrowHeight;
                ChinHeight = f.ChinHeight;
                ChinWidth = f.ChinWidth;
                ChinDepth = f.ChinDepth;
                LipFullnessUpper = f.LipFullnessUpper;
                LipFullnessLower = f.LipFullnessLower;
                LipWidth = f.LipWidth;
                NoseBridgeWidth = f.NoseBridgeWidth;
                NoseBridgeHeight = f.NoseBridgeHeight;
                NoseTipWidth = f.NoseTipWidth;
                NoseTipHeight = f.NoseTipHeight;
                NoseNostrilFlare = f.NoseNostrilFlare;
                EyeSpacing = f.EyeSpacing;
                EyeDepth = f.EyeDepth;
                EyeHeight = f.EyeHeight;
                EyeWidth = f.EyeWidth;
                EyeTilt = f.EyeTilt;
                ForeheadSlope = f.ForeheadSlope;
                ForeheadHeight = f.ForeheadHeight;
                JawAngle = f.JawAngle;
                JawRoundness = f.JawRoundness;
                TempleWidth = f.TempleWidth;
                CrownHeight = f.CrownHeight;
            }

            if (proto.BodyMorphs != null)
            {
                var b = proto.BodyMorphs;
                ShoulderWidth = b.ShoulderWidth;
                TorsoLength = b.TorsoLength;
                ArmLength = b.ArmLength;
                LegLength = b.LegLength;
                ArmThickness = b.ArmThickness;
                LegThickness = b.LegThickness;
                MuscleDefinition = b.MuscleDefinition;
                BodyFat = b.BodyFat;
                HipWidth = b.HipWidth;
                WaistWidth = b.WaistWidth;
                ChestDepth = b.ChestDepth;
            }

            if (proto.SkinDetail != null)
            {
                SkinTextureVariation = proto.SkinDetail.SkinTextureVariation;
                FreckleDensity = proto.SkinDetail.FreckleDensity;
                FreckleSize = proto.SkinDetail.FreckleSize;
                Aging = proto.SkinDetail.Aging;
                Roughness = proto.SkinDetail.Roughness;
                if (proto.SkinDetail.FreckleColor != null)
                    FreckleColor = ColorFromProto(proto.SkinDetail.FreckleColor);
            }

            if (proto.HairParams != null)
            {
                HairStyle = (int)proto.HairParams.BaseStyle;
                HairLength = proto.HairParams.Length;
                HairThickness = proto.HairParams.Thickness;
                HairCurl = proto.HairParams.CurlAmount;
                if (proto.HairParams.PrimaryColor != null)
                    HairColor = ColorFromProto(proto.HairParams.PrimaryColor);
                if (proto.HairParams.HighlightColor != null)
                    HairHighlightColor = ColorFromProto(proto.HairParams.HighlightColor);
                FacialHairStyle = (int)proto.HairParams.FacialHairStyle;
                FacialHairLength = proto.HairParams.FacialHairLength;
            }

            if (proto.EyeDetail != null)
            {
                if (proto.EyeDetail.LeftIrisColor != null)
                    LeftEyeColor = ColorFromProto(proto.EyeDetail.LeftIrisColor);
                if (proto.EyeDetail.RightIrisColor != null)
                    RightEyeColor = ColorFromProto(proto.EyeDetail.RightIrisColor);
                IrisSize = proto.EyeDetail.IrisSize;
                PupilSize = proto.EyeDetail.PupilSize;
                EyeShapeSlider = proto.EyeDetail.EyeShape;
                MatchEyes = (LeftEyeColor == RightEyeColor);
            }

            Decals.Clear();
            foreach (var d in proto.BodyDecals)
            {
                Decals.Add(new DecalEntry
                {
                    DecalId = d.DecalId,
                    BodyRegion = (int)d.BodyRegion,
                    PositionU = d.PositionU,
                    PositionV = d.PositionV,
                    Rotation = d.Rotation,
                    Scale = d.Scale,
                    Tint = d.Tint != null ? ColorFromProto(d.Tint) : Color.white
                });
            }

            if (proto.RaceFeatures != null)
            {
                RaceFeatureSetId = proto.RaceFeatures.FeatureSetId;
                RaceFeatureValues.Clear();
                foreach (var v in proto.RaceFeatures.FeatureValues)
                    RaceFeatureValues.Add(v);
            }
        }

        /// <summary>
        /// Deep copy for undo stack.
        /// </summary>
        public AppearanceData Clone()
        {
            var c = new AppearanceData
            {
                Gender = Gender, Race = Race, Height = Height, Build = Build,
                FaceShape = FaceShape, JawWidth = JawWidth, NoseSize = NoseSize, EarSize = EarSize,
                FacialMarking = FacialMarking,
                EyeColorLegacy = EyeColorLegacy, HairStyleLegacy = HairStyleLegacy,
                HairColorLegacy = HairColorLegacy, SkinToneLegacy = SkinToneLegacy,

                // Face
                EyeSpacing = EyeSpacing, EyeDepth = EyeDepth, EyeHeight = EyeHeight,
                EyeWidth = EyeWidth, EyeTilt = EyeTilt, BrowRidge = BrowRidge,
                NoseBridgeWidth = NoseBridgeWidth, NoseBridgeHeight = NoseBridgeHeight,
                NoseTipWidth = NoseTipWidth, NoseTipHeight = NoseTipHeight, NoseNostrilFlare = NoseNostrilFlare,
                LipFullnessUpper = LipFullnessUpper, LipFullnessLower = LipFullnessLower, LipWidth = LipWidth,
                ChinHeight = ChinHeight, ChinWidth = ChinWidth, ChinDepth = ChinDepth, JawAngle = JawAngle,
                CheekboneHeight = CheekboneHeight, CheekboneWidth = CheekboneWidth,
                ForeheadSlope = ForeheadSlope, ForeheadHeight = ForeheadHeight, BrowHeight = BrowHeight,
                TempleWidth = TempleWidth, CrownHeight = CrownHeight, JawRoundness = JawRoundness,

                // Body
                ShoulderWidth = ShoulderWidth, ChestDepth = ChestDepth,
                ArmLength = ArmLength, ArmThickness = ArmThickness,
                HipWidth = HipWidth, WaistWidth = WaistWidth,
                LegLength = LegLength, LegThickness = LegThickness,
                TorsoLength = TorsoLength, MuscleDefinition = MuscleDefinition, BodyFat = BodyFat,

                // Skin
                SkinColor = SkinColor, FreckleDensity = FreckleDensity, FreckleSize = FreckleSize,
                FreckleColor = FreckleColor, Aging = Aging, Roughness = Roughness,
                SkinTextureVariation = SkinTextureVariation,

                // Hair
                HairStyle = HairStyle, HairLength = HairLength, HairThickness = HairThickness,
                HairCurl = HairCurl, HairColor = HairColor, HairHighlightColor = HairHighlightColor,
                FacialHairStyle = FacialHairStyle, FacialHairLength = FacialHairLength,

                // Eyes
                LeftEyeColor = LeftEyeColor, RightEyeColor = RightEyeColor,
                MatchEyes = MatchEyes, IrisSize = IrisSize, PupilSize = PupilSize,
                EyeShapeSlider = EyeShapeSlider,

                // Race features
                RaceFeatureSetId = RaceFeatureSetId,

                // Name & skill
                FirstName = FirstName, LastName = LastName, SelectedSkill = SelectedSkill
            };

            c.Decals = new List<DecalEntry>();
            foreach (var d in Decals)
                c.Decals.Add(d.Clone());

            c.RaceFeatureValues = new List<float>(RaceFeatureValues);

            return c;
        }

        /// <summary>
        /// Randomize all appearance values to valid random ranges.
        /// </summary>
        public void Randomize()
        {
            Gender = UnityEngine.Random.Range(0, 2);
            Race = UnityEngine.Random.Range(0, 4);
            Height = UnityEngine.Random.Range(0.2f, 0.8f);
            Build = UnityEngine.Random.Range(0.2f, 0.8f);
            FaceShape = UnityEngine.Random.value;
            JawWidth = UnityEngine.Random.value;
            NoseSize = UnityEngine.Random.value;
            EarSize = UnityEngine.Random.value;
            FacialMarking = UnityEngine.Random.Range(0, 9);

            // Face blend shapes
            EyeSpacing = UnityEngine.Random.value;
            EyeDepth = UnityEngine.Random.value;
            EyeHeight = UnityEngine.Random.value;
            EyeWidth = UnityEngine.Random.value;
            EyeTilt = UnityEngine.Random.value;
            BrowRidge = UnityEngine.Random.value;
            NoseBridgeWidth = UnityEngine.Random.value;
            NoseBridgeHeight = UnityEngine.Random.value;
            NoseTipWidth = UnityEngine.Random.value;
            NoseTipHeight = UnityEngine.Random.value;
            NoseNostrilFlare = UnityEngine.Random.value;
            LipFullnessUpper = UnityEngine.Random.value;
            LipFullnessLower = UnityEngine.Random.value;
            LipWidth = UnityEngine.Random.value;
            ChinHeight = UnityEngine.Random.value;
            ChinWidth = UnityEngine.Random.value;
            ChinDepth = UnityEngine.Random.value;
            JawAngle = UnityEngine.Random.value;
            CheekboneHeight = UnityEngine.Random.value;
            CheekboneWidth = UnityEngine.Random.value;
            ForeheadSlope = UnityEngine.Random.value;
            ForeheadHeight = UnityEngine.Random.value;
            BrowHeight = UnityEngine.Random.value;
            TempleWidth = UnityEngine.Random.value;
            CrownHeight = UnityEngine.Random.value;
            JawRoundness = UnityEngine.Random.value;

            // Body morphs
            ShoulderWidth = UnityEngine.Random.Range(0.3f, 0.7f);
            ChestDepth = UnityEngine.Random.Range(0.3f, 0.7f);
            ArmLength = UnityEngine.Random.Range(0.35f, 0.65f);
            ArmThickness = UnityEngine.Random.Range(0.3f, 0.7f);
            HipWidth = UnityEngine.Random.Range(0.3f, 0.7f);
            WaistWidth = UnityEngine.Random.Range(0.3f, 0.7f);
            LegLength = UnityEngine.Random.Range(0.35f, 0.65f);
            LegThickness = UnityEngine.Random.Range(0.3f, 0.7f);
            TorsoLength = UnityEngine.Random.Range(0.35f, 0.65f);
            MuscleDefinition = UnityEngine.Random.value;
            BodyFat = UnityEngine.Random.value;

            // Skin
            SkinColor = new Color(
                UnityEngine.Random.Range(0.4f, 1f),
                UnityEngine.Random.Range(0.3f, 0.85f),
                UnityEngine.Random.Range(0.2f, 0.7f));
            FreckleDensity = UnityEngine.Random.value * 0.5f;
            FreckleSize = UnityEngine.Random.Range(0.1f, 0.5f);
            FreckleColor = new Color(
                UnityEngine.Random.Range(0.4f, 0.8f),
                UnityEngine.Random.Range(0.2f, 0.5f),
                UnityEngine.Random.Range(0.1f, 0.4f));
            Aging = UnityEngine.Random.value * 0.5f;
            Roughness = UnityEngine.Random.Range(0.1f, 0.5f);
            SkinTextureVariation = UnityEngine.Random.value;

            // Hair
            HairStyle = UnityEngine.Random.Range(0, 8);
            HairLength = UnityEngine.Random.value;
            HairThickness = UnityEngine.Random.value;
            HairCurl = UnityEngine.Random.value;
            HairColor = new Color(
                UnityEngine.Random.Range(0.05f, 0.9f),
                UnityEngine.Random.Range(0.05f, 0.7f),
                UnityEngine.Random.Range(0.05f, 0.5f));
            HairHighlightColor = new Color(
                Mathf.Clamp01(HairColor.r + UnityEngine.Random.Range(0.1f, 0.3f)),
                Mathf.Clamp01(HairColor.g + UnityEngine.Random.Range(0.1f, 0.3f)),
                Mathf.Clamp01(HairColor.b + UnityEngine.Random.Range(0.05f, 0.15f)));
            FacialHairStyle = Gender == 0 ? UnityEngine.Random.Range(0, 5) : 0;
            FacialHairLength = Gender == 0 ? UnityEngine.Random.value : 0f;

            // Eyes
            LeftEyeColor = new Color(
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value);
            MatchEyes = UnityEngine.Random.value > 0.15f; // 85% chance matched
            RightEyeColor = MatchEyes ? LeftEyeColor : new Color(
                UnityEngine.Random.value,
                UnityEngine.Random.value,
                UnityEngine.Random.value);
            IrisSize = UnityEngine.Random.Range(0.3f, 0.7f);
            PupilSize = UnityEngine.Random.Range(0.3f, 0.7f);
            EyeShapeSlider = UnityEngine.Random.value;

            // Clear decals on randomize
            Decals.Clear();

            // Race features — add default values
            SetDefaultRaceFeatures();
        }

        /// <summary>
        /// Set default race feature values based on current race.
        /// </summary>
        public void SetDefaultRaceFeatures()
        {
            RaceFeatureValues.Clear();
            switch (Race)
            {
                case 0: // Human — no features
                    RaceFeatureSetId = 0;
                    break;
                case 1: // Sylvari — pattern, glow intensity, glow color (R,G,B)
                    RaceFeatureSetId = 1;
                    RaceFeatureValues.AddRange(new float[] { 0f, 0.5f, 0.2f, 0.8f, 0.4f });
                    break;
                case 2: // Korathi — horn style, horn length, ridge prominence
                    RaceFeatureSetId = 2;
                    RaceFeatureValues.AddRange(new float[] { 0f, 0.5f, 0.5f });
                    break;
                case 3: // Ashborn — pattern, marking intensity, eye glow
                    RaceFeatureSetId = 3;
                    RaceFeatureValues.AddRange(new float[] { 0f, 0.5f, 0.5f });
                    break;
            }
        }

        // ─── Helpers ───────────────────────────────────────────────────────

        private static Orlo.Proto.Color ColorToProto(Color c)
        {
            return new Orlo.Proto.Color { R = c.r, G = c.g, B = c.b };
        }

        private static Color ColorFromProto(Orlo.Proto.Color c)
        {
            return new Color(c.R, c.G, c.B);
        }
    }
}
