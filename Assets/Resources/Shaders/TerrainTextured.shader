Shader "Orlo/TerrainTextured"
{
    Properties
    {
        _Glossiness ("Smoothness", Range(0,1)) = 0.05

        // Terrain textures (set from C# via TerrainTextures.cs)
        _GrassTex ("Grass", 2D) = "white" {}
        _RockTex  ("Rock",  2D) = "white" {}
        _DirtTex  ("Dirt",  2D) = "white" {}
        _SandTex  ("Sand",  2D) = "white" {}

        // Normal maps
        _GrassNorm ("Grass Normal", 2D) = "bump" {}
        _RockNorm  ("Rock Normal",  2D) = "bump" {}
        _DirtNorm  ("Dirt Normal",  2D) = "bump" {}
        _SandNorm  ("Sand Normal",  2D) = "bump" {}

        // Tiling and blending
        _TexScale ("Texture Scale (UV tiling)", Float) = 0.1
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 8)) = 4
        _SlopeThreshold ("Slope Threshold for Triplanar", Range(0, 1)) = 0.7
        _DetailNoiseScale ("Detail Noise Scale", Float) = 0.4
        _DetailNoiseStrength ("Detail Noise Strength", Range(0, 0.3)) = 0.08
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.5

        sampler2D _GrassTex;
        sampler2D _RockTex;
        sampler2D _DirtTex;
        sampler2D _SandTex;

        sampler2D _GrassNorm;
        sampler2D _RockNorm;
        sampler2D _DirtNorm;
        sampler2D _SandNorm;

        half _Glossiness;
        float _TexScale;
        float _TriplanarSharpness;
        float _SlopeThreshold;
        float _DetailNoiseScale;
        half _DetailNoiseStrength;
        half _NormalStrength;

        struct Input
        {
            float4 vertColor;   // splatmap weights: R=grass, G=rock, B=dirt, A=sand
            float3 worldPos;
            float3 worldNormal;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vertColor = v.color;
            o.worldNormal = UnityObjectToWorldNormal(v.normal);
        }

        // Simple hash-based noise for detail variation (no extra texture needed)
        float hash2D(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float valueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f); // smoothstep

            float a = hash2D(i);
            float b = hash2D(i + float2(1, 0));
            float c = hash2D(i + float2(0, 1));
            float d = hash2D(i + float2(1, 1));

            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // Sample a texture+normal using triplanar projection
        void sampleTriplanar(sampler2D tex, sampler2D norm, float3 worldPos, float3 blendWeights,
                             float scale, out half3 color, out half3 normalOut)
        {
            // Three axis-aligned projections
            half3 xCol = tex2D(tex, worldPos.yz * scale).rgb;
            half3 yCol = tex2D(tex, worldPos.xz * scale).rgb;
            half3 zCol = tex2D(tex, worldPos.xy * scale).rgb;

            half3 xNrm = UnpackNormal(tex2D(norm, worldPos.yz * scale));
            half3 yNrm = UnpackNormal(tex2D(norm, worldPos.xz * scale));
            half3 zNrm = UnpackNormal(tex2D(norm, worldPos.xy * scale));

            color = xCol * blendWeights.x + yCol * blendWeights.y + zCol * blendWeights.z;
            normalOut = xNrm * blendWeights.x + yNrm * blendWeights.y + zNrm * blendWeights.z;
        }

        // Sample texture+normal with standard UV (top-down Y projection)
        void samplePlanar(sampler2D tex, sampler2D norm, float3 worldPos,
                          float scale, out half3 color, out half3 normalOut)
        {
            float2 uv = worldPos.xz * scale;
            color = tex2D(tex, uv).rgb;
            normalOut = UnpackNormal(tex2D(norm, uv));
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 wPos = IN.worldPos;
            float3 wNrm = normalize(IN.worldNormal);

            // Splatmap weights from vertex color
            half grassW = IN.vertColor.r;
            half rockW  = IN.vertColor.g;
            half dirtW  = IN.vertColor.b;
            half sandW  = IN.vertColor.a;

            // Normalize weights (they should sum to ~1, but ensure it)
            half totalW = grassW + rockW + dirtW + sandW;
            totalW = max(totalW, 0.001);
            grassW /= totalW;
            rockW  /= totalW;
            dirtW  /= totalW;
            sandW  /= totalW;

            // Determine if this fragment needs triplanar (steep slope)
            float slopeY = abs(wNrm.y);
            float useTriplanar = 1.0 - saturate((slopeY - _SlopeThreshold) * 4.0);

            // Triplanar blend weights from world normal
            float3 triWeights = pow(abs(wNrm), _TriplanarSharpness);
            triWeights /= (triWeights.x + triWeights.y + triWeights.z + 0.001);

            // Sample each terrain layer
            half3 grassCol, rockCol, dirtCol, sandCol;
            half3 grassNrm, rockNrm, dirtNrm, sandNrm;

            // Triplanar path (steep slopes)
            half3 grassColTri, rockColTri, dirtColTri, sandColTri;
            half3 grassNrmTri, rockNrmTri, dirtNrmTri, sandNrmTri;

            // Planar path (flat areas — cheaper, no stretching on flat ground)
            samplePlanar(_GrassTex, _GrassNorm, wPos, _TexScale, grassCol, grassNrm);
            samplePlanar(_RockTex,  _RockNorm,  wPos, _TexScale, rockCol,  rockNrm);
            samplePlanar(_DirtTex,  _DirtNorm,  wPos, _TexScale, dirtCol,  dirtNrm);
            samplePlanar(_SandTex,  _SandNorm,  wPos, _TexScale, sandCol,  sandNrm);

            // Only compute triplanar where needed (steep slopes)
            if (useTriplanar > 0.01)
            {
                sampleTriplanar(_GrassTex, _GrassNorm, wPos, triWeights, _TexScale, grassColTri, grassNrmTri);
                sampleTriplanar(_RockTex,  _RockNorm,  wPos, triWeights, _TexScale, rockColTri,  rockNrmTri);
                sampleTriplanar(_DirtTex,  _DirtNorm,  wPos, triWeights, _TexScale, dirtColTri,  dirtNrmTri);
                sampleTriplanar(_SandTex,  _SandNorm,  wPos, triWeights, _TexScale, sandColTri,  sandNrmTri);

                grassCol = lerp(grassCol, grassColTri, useTriplanar);
                rockCol  = lerp(rockCol,  rockColTri,  useTriplanar);
                dirtCol  = lerp(dirtCol,  dirtColTri,  useTriplanar);
                sandCol  = lerp(sandCol,  sandColTri,  useTriplanar);

                grassNrm = lerp(grassNrm, grassNrmTri, useTriplanar);
                rockNrm  = lerp(rockNrm,  rockNrmTri,  useTriplanar);
                dirtNrm  = lerp(dirtNrm,  dirtNrmTri,  useTriplanar);
                sandNrm  = lerp(sandNrm,  sandNrmTri,  useTriplanar);
            }

            // Blend layers by splatmap weights
            half3 finalColor = grassCol * grassW + rockCol * rockW + dirtCol * dirtW + sandCol * sandW;
            half3 finalNormal = grassNrm * grassW + rockNrm * rockW + dirtNrm * dirtW + sandNrm * sandW;

            // Detail noise for close-up variation (breaks up tiling repetition)
            float detailNoise = valueNoise(wPos.xz * _DetailNoiseScale);
            float detailNoise2 = valueNoise(wPos.xz * _DetailNoiseScale * 3.7); // second octave
            float detail = (detailNoise * 0.7 + detailNoise2 * 0.3); // multi-octave
            finalColor *= (1.0 + (detail - 0.5) * _DetailNoiseStrength * 2.0);

            // Output
            o.Albedo = finalColor;
            o.Normal = normalize(half3(finalNormal.xy * _NormalStrength, finalNormal.z));
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }

    // Fall back to the vertex color version if this shader fails
    Fallback "Orlo/TerrainVertexColor"
}
