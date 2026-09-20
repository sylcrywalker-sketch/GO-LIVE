Shader "GO LIVE/Atmosphere/Lit Dust"
{
    Properties { [HDR] _Tint("Dust tint",Color)=(0.62,0.69,0.78,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "WindowMedium.hlsl"
            float4 _Tint;
            float _LightAmount;
            struct Attributes {float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;float eye:TEXCOORD2;};
            Varyings vert(Attributes v) {Varyings o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=v.uv;o.color=v.color;o.eye=-TransformWorldToView(o.world).z;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;
                float radius=dot(p,p);
                float soft=saturate((LinearEyeDepth(SampleSceneDepth(i.positionCS.xy/_ScaledScreenParams.xy),_ZBufferParams)-i.eye)*6);
                float cameraFade=smoothstep(0.45,1.35,distance(i.world,GetCameraPositionWS()));
                float mask=MediumMask(BeamCoordinates(i.world));
                float alpha=pow(saturate(1-radius),2.5)*i.color.a*soft*cameraFade*mask*_LightAmount;
                return half4(_Tint.rgb*i.color.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
