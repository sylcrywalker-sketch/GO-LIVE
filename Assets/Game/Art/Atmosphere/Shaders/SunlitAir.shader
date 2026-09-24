// Faint light shafts where direct sun crosses the air near the window. Drawn on a box (the unit cube scaled over the
// sunlit part of the room): each pixel marches its view ray through the box, up to the scene depth, and adds the sun
// that the main light shadow map lets through. Additive, no history; the sun's colour and intensity come from the main
// light, so the shafts follow the time of day on their own.
Shader "GO LIVE/Atmosphere/Sunlit Air"
{
    Properties
    {
        _Tint("Scattering tint", Color) = (1, 0.97, 0.92, 1)
        _Density("Density", Range(0, 0.2)) = 0.035
        _Anisotropy("Forward scattering", Range(0, 0.9)) = 0.45
        _EdgeFade("Box edge fade", Range(0.02, 0.5)) = 0.22
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SunlitAir"
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "SunlitMedium.hlsl"

            #define SUNLIT_AIR_STEPS 20

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Density;
                half _Anisotropy;
                half _EdgeFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 origin = GetCameraPositionWS();
                float3 direction = normalize(input.positionWS - origin);

                // Slab test against the unit box. The object-space direction is not normalised, so t stays in metres.
                float3 originOS = TransformWorldToObject(origin);
                float3 directionOS = mul((float3x3)GetWorldToObjectMatrix(), direction);
                directionOS = max(abs(directionOS), 1e-6) * (step(0.0, directionOS) * 2.0 - 1.0);
                float3 t0 = (-0.5 - originOS) / directionOS;
                float3 t1 = (0.5 - originOS) / directionOS;
                float3 nearHit = min(t0, t1);
                float3 farHit = max(t0, t1);
                float enter = max(max(nearHit.x, nearHit.y), max(nearHit.z, 0.0));
                float leave = min(farHit.x, min(farHit.y, farHit.z));

                // Stop at the first opaque surface along the ray.
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;
                float eyeDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
                leave = min(leave, eyeDepth / max(dot(direction, cameraForward), 1e-4));

                float span = leave - enter;
                if (span <= 0.0)
                    return 0;

                float stepLength = span / SUNLIT_AIR_STEPS;
                float jitter = SunlitJitter(input.positionCS.xy);
                float sunlit = 0.0;

                [unroll]
                for (int i = 0; i < SUNLIT_AIR_STEPS; i++)
                {
                    float3 positionWS = origin + direction * (enter + (i + jitter) * stepLength);
                    float3 box = abs(TransformWorldToObject(positionWS)) * 2.0;
                    float edge = 1.0 - smoothstep(1.0 - _EdgeFade, 1.0, max(box.x, max(box.y, box.z)));
                    sunlit += SunlitShadow(positionWS) * edge;
                }

                Light sun = GetMainLight();
                half phase = SunlitPhase(dot(direction, sun.direction), _Anisotropy);
                half3 inscatter = sun.color * _Tint.rgb * (sunlit * stepLength * _Density * phase);
                return half4(inscatter, 0);
            }
            ENDHLSL
        }
    }
}
