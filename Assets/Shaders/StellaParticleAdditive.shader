// 불꽃 파티클용 Additive 셰이더 (URP)
// 최종 색 = 텍스처 × 파티클 색(Start Color, Color over Lifetime) × Intensity
// Particle System의 색은 1을 넘길 수 없으므로, Intensity로 HDR 밝기를 만들어 Bloom이 번지게 한다.
// 로켓 / 폭발 / 섬광처럼 밝기가 다른 부분은 머티리얼을 따로 만들어 Intensity만 다르게 준다.

Shader "Stella/ParticleAdditive"
{
    Properties
    {
        _BaseMap   ("Texture", 2D) = "white" {}
        _Intensity ("Intensity (HDR)", Range(0, 20)) = 2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "RenderPipeline" = "UniversalPipeline" }
        Blend One One      // Additive: 겹칠수록 밝아짐
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half   _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 c = tex * i.color;
                // Additive에서는 알파를 밝기에 곱해서 서서히 사라지게 한다
                return half4(c.rgb * c.a * _Intensity, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
