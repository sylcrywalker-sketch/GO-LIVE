// Dust motes for a particle system: each mote only shows while direct sun reaches it (main light shadow map) and is
// brightest against the light (forward scattering), so the dust drifting near the window reads only inside the sun
// shafts. Additive; motes fade out close to the camera.
Shader "GO LIVE/Atmosphere/Sunlit Dust"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 0.96, 0.9, 1)
        _Brightness("Brightness", Range(0, 4)) = 1
        _Anisotropy("Forward scattering", Range(0, 0.9)) = 0.55
        _NearFade("Near camera fade (m)", Range(0.05, 2)) = 0.6
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "SunlitDust"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SunlitMedium.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Brightness;
                half _Anisotropy;
                half _NearFade;
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
                half nearFade = saturate((moteDistance - 0.2) / _NearFade);

                Light sun = GetMainLight();
                half phase = SunlitPhase(dot(toMote / max(moteDistance, 1e-4), sun.direction), _Anisotropy);
                half amount = SunlitShadow(input.positionWS) * phase * mote * nearFade * input.color.a * _Brightness;
                return half4(sun.color * _Tint.rgb * input.color.rgb * amount, 0);
            }
            ENDHLSL
        }
    }
}
