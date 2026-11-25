// 밤하늘 Skybox (URP)
// - 위(짙은 남색) → 지평선(옅은 보라) 그라데이션
// - 반짝이는 별 (텍스처 없이 방향 벡터를 해시해서 생성)
// - _Dawn 0 → 1 로 올리면 보라·분홍 새벽 하늘 + 창문 쪽(-X) 해 빛으로 바뀐다. 별은 사라진다.
//   (새벽 전환 연출을 쓸지는 아직 미정. Timeline이나 스크립트에서 _Dawn 값만 바꾸면 된다.)

Shader "Stella/NightSky"
{
    Properties
    {
        [Header(Night)]
        _NightTop        ("Night Top", Color) = (0.010, 0.015, 0.050, 1)
        _NightHorizon    ("Night Horizon", Color) = (0.090, 0.070, 0.200, 1)

        [Header(Dawn)]
        _Dawn            ("Dawn (0 = night, 1 = dawn)", Range(0, 1)) = 0
        _DawnTop         ("Dawn Top", Color) = (0.280, 0.250, 0.550, 1)
        _DawnHorizon     ("Dawn Horizon", Color) = (1.000, 0.620, 0.780, 1)
        [HDR] _SunColor  ("Sun Glow Color", Color) = (1.6, 1.0, 1.2, 1)
        _SunDirection    ("Sun Direction (xyz)", Vector) = (-1, 0.08, 0.35, 0)

        [Header(Shape)]
        _Ground          ("Ground (below horizon)", Color) = (0.015, 0.015, 0.035, 1)
        _HorizonSharpness("Horizon Sharpness", Range(0.5, 8)) = 2.5
        _Exposure        ("Exposure", Range(0, 4)) = 1

        [Header(Stars)]
        _StarDensity     ("Star Density", Range(30, 400)) = 120
        _StarSize        ("Star Size", Range(0.02, 0.4)) = 0.15
        _StarAmount      ("Star Amount", Range(0, 1)) = 0.35
        _StarBrightness  ("Star Brightness", Range(0, 4)) = 0.9
        _TwinkleSpeed    ("Twinkle Speed", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _NightTop;
                half4  _NightHorizon;
                half   _Dawn;
                half4  _DawnTop;
                half4  _DawnHorizon;
                half4  _SunColor;
                float4 _SunDirection;
                half4  _Ground;
                half   _HorizonSharpness;
                half   _Exposure;
                float  _StarDensity;
                float  _StarSize;
                half   _StarAmount;
                half   _StarBrightness;
                half   _TwinkleSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir        : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz; // Skybox 메시의 정점 위치 = 바라보는 방향
                return o;
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yxz + 19.19);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                // 하늘 그라데이션 (지평선 근처가 넓게 밝음)
                float t = 1.0 - pow(1.0 - saturate(h), _HorizonSharpness);
                float3 night = lerp(_NightHorizon.rgb, _NightTop.rgb, t);
                float3 dawn  = lerp(_DawnHorizon.rgb,  _DawnTop.rgb,  t);
                float3 sky   = lerp(night, dawn, _Dawn);

                // 새벽 해 빛 (창문 쪽에서 번지는 빛)
                float s = saturate(dot(d, normalize(_SunDirection.xyz)));
                sky += _SunColor.rgb * (pow(s, 8.0) * 0.5 + pow(s, 64.0)) * _Dawn;

                // 지평선 아래 (땅)
                float3 groundCol = lerp(_Ground.rgb, _DawnHorizon.rgb * 0.25, _Dawn);
                sky = lerp(sky, groundCol, saturate(-h * 8.0));

                // 별: 방향을 3D 격자로 나누고, 칸마다 확률적으로 별 하나
                float3 p    = d * _StarDensity;
                float3 cell = floor(p);
                float3 rnd  = Hash33(cell);
                float3 starPos = 0.2 + rnd * 0.6;
                float  dist = length(frac(p) - starPos);
                float  present = step(1.0 - _StarAmount, frac(rnd.x * 13.7 + rnd.z * 7.3));
                float  twinkle = 0.65 + 0.35 * sin(_Time.y * _TwinkleSpeed * (0.5 + rnd.y) + rnd.z * 6.2831);
                float  star = saturate(1.0 - dist / _StarSize);
                star = star * star * present * twinkle * _StarBrightness;
                star *= saturate(h * 6.0) * (1.0 - _Dawn); // 지평선 근처와 새벽에는 숨김
                float3 starTint = lerp(float3(0.80, 0.86, 1.00), float3(1.00, 0.95, 0.85), rnd.y);
                sky += star * starTint;

                return half4(sky * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
