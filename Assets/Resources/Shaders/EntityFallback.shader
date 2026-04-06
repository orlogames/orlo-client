Shader "Orlo/EntityFallback"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.5, 0.5, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        CGPROGRAM
        #pragma surface surf Lambert
        #pragma target 3.0

        fixed4 _Color;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallbackOff
}
