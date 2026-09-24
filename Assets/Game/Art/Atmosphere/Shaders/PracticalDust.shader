// Dust motes for a particle system, lit by the room's own lamps: a mote only shows where a local light (the desk lamp,
// the monitor) reaches it, with that light's falloff and shadows, and is brightest against the light (forward
// scattering). Additive; motes fade out close to the camera. The sun-lit counterpart by the window is Sunlit Dust.
Shader "GO LIVE/Atmosphere/Practical Dust"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 0.97, 0.92, 1)
        _Brightness("Brightness", Range(0, 4)) = 1
        _Anisotropy("Forward scattering", Range(0, 0.9)) = 0.5
        _NearFade("Near camera fade (m)", Range(0.05, 2)) = 0.5
        _FarFade("Far fade start (m)", Range(0.5, 10)) = 2.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "PracticalDust"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SunlitMedium.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Brightness;
                half _Anisotropy;
                half _NearFade;
                half _FarFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centred = input.uv * 2.0 - 1.0;
                half mote = saturate(1.0 - dot(centred, centred));
                mote *= mote;

                float3 toMote = input.positionWS - GetCameraPositionWS();
                float moteDistance = length(toMote);
                float3 view = toMote / max(moteDistance, 1e-4);
                half nearFade = saturate((moteDistance - 0.15) / _NearFade);

                // Motes are far below a pixel from across the room: they fade out instead of turning into sparkles.
                half farFade = saturate(1.0 - (moteDistance - _FarFade) / 1.5);

                // The light loop macros read inputData.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half3 lit = 0;
            #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light local = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    half phase = SunlitPhase(dot(view, local.direction), _Anisotropy);
                    lit += local.color * (local.distanceAttenuation * local.shadowAttenuation * phase);
                LIGHT_LOOP_END
            #endif

                // Right next to a bulb the inverse square explodes; a mote never outshines a lit wall by much.
                lit = min(lit, 3.0h);

                half amount = mote * nearFade * farFade * input.color.a * _Brightness;
                return half4(lit * _Tint.rgb * input.color.rgb * amount, 0);
            }
            ENDHLSL
        }
    }
}
