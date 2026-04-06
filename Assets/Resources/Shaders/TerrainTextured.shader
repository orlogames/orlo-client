Shader "Orlo/TerrainTextured"
{
    Properties
    {
        _Glossiness ("Smoothness", Range(0,1)) = 0.05
        _GrassTex ("Grass", 2D) = "white" {}
        _RockTex  ("Rock",  2D) = "white" {}
        _DirtTex  ("Dirt",  2D) = "white" {}
        _SandTex  ("Sand",  2D) = "white" {}
        _GrassNorm ("Grass Normal", 2D) = "bump" {}
        _RockNorm  ("Rock Normal",  2D) = "bump" {}
        _DirtNorm  ("Dirt Normal",  2D) = "bump" {}
        _SandNorm  ("Sand Normal",  2D) = "bump" {}
        _TexScale ("Texture Scale", Float) = 0.1
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _GrassTex, _RockTex, _DirtTex, _SandTex;
        sampler2D _GrassNorm, _RockNorm, _DirtNorm, _SandNorm;
        half _Glossiness;
        float _TexScale;
        half _NormalStrength;

        struct Input
        {
            float4 vertColor;
            float3 worldPos;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vertColor = v.color;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.worldPos.xz * _TexScale;

            // Splatmap weights from vertex color (R=grass, G=rock, B=dirt, A=sand)
            half gw = IN.vertColor.r;
            half rw = IN.vertColor.g;
            half dw = IN.vertColor.b;
            half sw = IN.vertColor.a;

            // Sample albedo textures and blend
            half3 col = tex2D(_GrassTex, uv).rgb * gw
                      + tex2D(_RockTex,  uv).rgb * rw
                      + tex2D(_DirtTex,  uv).rgb * dw
                      + tex2D(_SandTex,  uv).rgb * sw;

            // Sample normal maps and blend
            half3 nrm = UnpackNormal(tex2D(_GrassNorm, uv)) * gw
                      + UnpackNormal(tex2D(_RockNorm,  uv)) * rw
                      + UnpackNormal(tex2D(_DirtNorm,  uv)) * dw
                      + UnpackNormal(tex2D(_SandNorm,  uv)) * sw;

            o.Albedo = col;
            o.Normal = normalize(half3(nrm.xy * _NormalStrength, nrm.z));
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }

    Fallback "Diffuse"
}
