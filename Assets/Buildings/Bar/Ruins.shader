// "Festett" toon shader + körvonal + RAJZFILMES MOHA (URP / Unity 6, telefonbarát).
// A ToonOutline mohás változata: a felfelé néző lapokra (fal teteje, oszlop teteje)
// ÉLES szélű, kéttónusú mohát tesz, mint egy kézzel festett rajzon.
// Opcionálisan a fal tövébe is (a jelenet talajszintjétől mérve).
Shader "ClayGun/Toon Festett Outline Moha"
{
    Properties
    {
        _BaseMap        ("Paletta (Base Map)", 2D) = "white" {}
        _BaseColor      ("Szín szorzó", Color) = (1,1,1,1)

        [Header(Arnyek)]
        _ShadowColor    ("Árnyék színe", Color) = (0.62, 0.60, 0.85, 1)
        _ShadeStep      ("Árnyék határa", Range(-1, 1)) = 0.0
        _ShadeSoft      ("Árnyék lágysága", Range(0.001, 1)) = 0.15
        _AmbientStrength("Környezeti fény", Range(0, 1)) = 0.35

        [Header(Festett hatas)]
        _Saturation     ("Színtelítés", Range(0, 2)) = 1.2
        _RimColor       ("Peremfény színe (A = erősség)", Color) = (1, 0.95, 0.85, 0.25)
        _RimPower       ("Peremfény szélessége", Range(1, 10)) = 4

        [Header(Moha)]
        _MossColor      ("Moha színe", Color) = (0.36, 0.58, 0.22, 1)
        _MossColor2     ("Moha világos foltok", Color) = (0.55, 0.76, 0.32, 1)
        _MossAmount     ("Moha mennyisége (tetején)", Range(0, 1)) = 0.45
        _MossWobble     ("Moha széle hullámos", Range(0, 1)) = 0.35
        _MossScale      ("Foltok mérete (kisebb = nagyobb folt)", Float) = 1.5
        _MossLight      ("Világos foltok aránya", Range(0, 1)) = 0.35

        [Header(Moha a toveben)]
        _GroundMoss     ("Moha a tövében (0 = ki, 1 = be)", Range(0, 1)) = 0
        _GroundY        ("Talaj magassága a jelenetben (Y)", Float) = 0
        _GroundHeight   ("Tövében: milyen magasra (m)", Float) = 0.3

        [Header(Korvonal)]
        _OutlineWidth   ("Körvonal vastagság (pixel)", Range(0, 8)) = 2
        _OutlineColor   ("Körvonal színe", Color) = (0.22, 0.15, 0.12, 1)
        _OutlineUseAlbedo("Körvonal a tárgy színéből (0-1)", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _ShadowColor;
            half   _ShadeStep;
            half   _ShadeSoft;
            half   _AmbientStrength;
            half   _Saturation;
            half4  _RimColor;
            half   _RimPower;
            half4  _MossColor;
            half4  _MossColor2;
            half   _MossAmount;
            half   _MossWobble;
            float  _MossScale;
            half   _MossLight;
            half   _GroundMoss;
            float  _GroundY;
            float  _GroundHeight;
            float  _OutlineWidth;
            half4  _OutlineColor;
            half   _OutlineUseAlbedo;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        half3 SampleAlbedo(float2 uv)
        {
            half3 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
            half lum = dot(a, half3(0.299, 0.587, 0.114));
            return saturate(lerp(lum.xxx, a, _Saturation));
        }

        // --- olcsó, sima 3D zaj a moha szélének hullámosításához ---
        float Hash3(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }
        float Noise3(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            float a = lerp(lerp(Hash3(i),                   Hash3(i + float3(1,0,0)), f.x),
                           lerp(Hash3(i + float3(0,1,0)),   Hash3(i + float3(1,1,0)), f.x), f.y);
            float b = lerp(lerp(Hash3(i + float3(0,0,1)),   Hash3(i + float3(1,0,1)), f.x),
                           lerp(Hash3(i + float3(0,1,1)),   Hash3(i + float3(1,1,1)), f.x), f.y);
            return lerp(a, b, f.z);
        }

        // Moha: ÉLES (toon) szél, két tónus.
        // posOS = a tárgy saját koordinátája (a moha együtt mozog a tárggyal),
        // posWS = világ pozíció (a talajszinthez), N = világ normál.
        half3 ApplyMoss(half3 albedo, float3 posOS, float3 posWS, float3 N)
        {
            float3 q = posOS * _MossScale;
            float n  = Noise3(q) * 0.7 + Noise3(q * 2.6 + 7.3) * 0.3;
            float n2 = Noise3(q * 1.9 + 13.7);

            // tetején: felfelé néző lapok (minél nagyobb a Moha mennyisége, annál meredekebb lapra is jut)
            float thr = 1.0 - _MossAmount;
            half top = step(thr, N.y + (n - 0.5) * _MossWobble);

            // tövében: a talajszinttől számított sáv, hullámos felső széllel (alapból kikapcsolva)
            float h = posWS.y - _GroundY;
            half ground = step(h + (n - 0.5) * 0.25, _GroundHeight) * step(N.y, 0.7) * step(-0.2, h);
            ground *= step(0.5, _GroundMoss);

            half m = max(top, ground);
            half3 moss = (n2 > 1.0 - _MossLight) ? _MossColor2.rgb : _MossColor.rgb;
            return lerp(albedo, moss, m);
        }
        ENDHLSL

        // ---------- Fő pass ----------
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS   = TransformObjectToWorldNormal(i.normalOS);
                o.uv         = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fogFactor  = ComputeFogFactor(o.positionCS.z);
                o.positionOS = i.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                half3 albedo = ApplyMoss(SampleAlbedo(i.uv), i.positionOS, i.positionWS, N);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));

                half ndl = dot(N, sun.direction);
                half lit = smoothstep(_ShadeStep - _ShadeSoft, _ShadeStep + _ShadeSoft, ndl);
                lit *= sun.shadowAttenuation;

                half3 col = lerp(albedo * _ShadowColor.rgb, albedo * sun.color, lit);
                col += albedo * SampleSH(N) * _AmbientStrength;

                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(1.0h - saturate(dot(N, V)), _RimPower) * lit;
                col += _RimColor.rgb * rim * _RimColor.a;

                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ---------- Körvonal (outline) ----------
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_fog

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   fogFactor  : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float3 normalWS   : TEXCOORD4;
            };

            V vertOutline(A i)
            {
                V o;
                float4 posCS = TransformObjectToHClip(i.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(i.normalOS);
                float2 nCS   = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float  len   = max(length(nCS), 1e-5);
                // képernyőn állandó, pixelben mért vastagság
                float2 offset = (nCS / len) * _OutlineWidth * 2.0 / _ScreenParams.xy;
                posCS.xy += offset * posCS.w;
                o.positionCS = posCS;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(posCS.z);
                o.positionOS = i.positionOS.xyz;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = nWS;
                return o;
            }

            half4 fragOutline(V i) : SV_Target
            {
                half3 albedo = ApplyMoss(SampleAlbedo(i.uv), i.positionOS, i.positionWS, normalize(i.normalWS));
                half3 col = lerp(_OutlineColor.rgb, albedo * 0.35h, _OutlineUseAlbedo);
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ---------- Árnyékvetés ----------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };

            V vertShadow(A i)
            {
                V o;
                float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 pos = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
                #if UNITY_REVERSED_Z
                    pos.z = min(pos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    pos.z = max(pos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = pos;
                return o;
            }

            half4 fragShadow(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        // ---------- Mélység (depth) ----------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };

            V vertDepth(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); return o; }
            half4 fragDepth(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
}
