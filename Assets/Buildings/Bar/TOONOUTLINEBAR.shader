// "Festett" toon shader + vékony körvonal (outline), URP / Unity 6, telefonbarát.
// Ugyanaz, mint a ToonFestett, plusz egy második pass, ami a modell köré
// egy vékony, sötét kontúrt rajzol (inverted hull módszer).
Shader "ClayGun/Toon Festett Outline"
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
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS   = TransformObjectToWorldNormal(i.normalOS);
                o.uv         = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fogFactor  = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = SampleAlbedo(i.uv);

                float3 N = normalize(i.normalWS);
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
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; };

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
                return o;
            }

            half4 fragOutline(V i) : SV_Target
            {
                half3 albedo = SampleAlbedo(i.uv);
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
