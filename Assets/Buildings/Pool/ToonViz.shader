// Rajzfilmes (toon) víz URP-hez (Unity 6) - telefonbarát, depth textúra nélkül.
// - két tónusú, lassan mozgó világos/sötét foltok
// - vékony fehér "fényháló" vonalak (mint a medencék alján)
// - fehér hab a víz szélén (a lap UV-ja alapján, ezért a víz lap UV-ja maradjon 0-1)
// - lágy hullámzás (a lapnak legyenek belső pontjai: Subdivide 20)
Shader "ClayGun/Toon Viz"
{
    Properties
    {
        _ShallowColor ("Világos víz", Color) = (0.38, 0.86, 0.95, 1)
        _DeepColor    ("Sötét víz", Color)   = (0.10, 0.50, 0.80, 1)
        _Alpha        ("Átlátszóság (1 = tömör)", Range(0, 1)) = 0.85

        [Header(Fenyhalo)]
        _CausticColor ("Vonal színe (A = erősség)", Color) = (1, 1, 1, 0.8)
        _CausticScale ("Minta mérete", Float) = 1.5
        _CausticSpeed ("Minta sebessége", Float) = 0.6
        _LineWidth    ("Vonal vastagsága", Range(0.01, 0.4)) = 0.12

        [Header(Hab a szelen)]
        _FoamColor    ("Hab színe", Color) = (1, 1, 1, 1)
        _FoamWidth    ("Hab szélessége", Range(0, 0.2)) = 0.04

        [Header(Hullamok)]
        _WaveHeight   ("Hullám magasság", Float) = 0.03
        _WaveScale    ("Hullám sűrűség", Float) = 1.5
        _WaveSpeed    ("Hullám sebesség", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "ToonWater"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half  _Alpha;
                half4 _CausticColor;
                float _CausticScale;
                float _CausticSpeed;
                half  _LineWidth;
                half4 _FoamColor;
                half  _FoamWidth;
                float _WaveHeight;
                float _WaveScale;
                float _WaveSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
            };

            // 0 a lap szélén, 1 beljebb (a szél ne mozogjon, ne lógjon ki a medencéből)
            float EdgeMask(float2 uv)
            {
                float2 e = min(uv, 1.0 - uv);
                return saturate(min(e.x, e.y) * 10.0);
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);

                float t = _Time.y * _WaveSpeed;
                float w = sin(ws.x * _WaveScale + t) * 0.6
                        + sin(ws.z * _WaveScale * 1.3 - t * 0.8) * 0.4;
                ws.y += w * _WaveHeight * EdgeMask(i.uv);

                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = i.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // olcsó, hullámzó minta: kb. -1..1
            float WaterNet(float2 p, float t)
            {
                float a = sin(p.x + sin(p.y * 1.3 + t)) * sin(p.y + sin(p.x * 0.9 - t));
                float b = sin(p.x * 1.7 - sin(p.y * 0.7 - t * 1.2)) * sin(p.y * 1.5 + sin(p.x * 1.1 + t * 0.7));
                return (a + b) * 0.5;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _CausticSpeed;
                float2 p = i.positionWS.xz * _CausticScale;

                // világos / sötét foltok (két tónus, lágy átmenettel)
                half spots = WaterNet(p * 0.35 + 3.1, t * 0.5) * 0.5h + 0.5h;
                half3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, smoothstep(0.4h, 0.6h, spots));

                // vékony fehér fényháló vonalak
                half n = abs(WaterNet(p, t));
                half lines = 1.0h - smoothstep(_LineWidth - 0.03h, _LineWidth, n);
                col = lerp(col, _CausticColor.rgb, lines * _CausticColor.a);

                // hab a szélén, enyhén hullámos határral
                float2 e = min(i.uv, 1.0 - i.uv);
                half d = min(e.x, e.y);
                half wob = sin((i.uv.x + i.uv.y) * 40.0 + _Time.y * 2.0) * 0.008h;
                half foam = 1.0h - step(_FoamWidth + wob, d);
                col = lerp(col, _FoamColor.rgb, foam);

                half alpha = saturate(_Alpha + foam + lines * 0.3h);
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
