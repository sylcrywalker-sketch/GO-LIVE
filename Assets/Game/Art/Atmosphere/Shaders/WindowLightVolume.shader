Shader "GO LIVE/Atmosphere/Window Light Volume"
{
    Properties { [HDR] _Tint("Scattering tint", Color) = (0.55,0.65,0.8,1) _Density("Density", Range(0,0.05)) = 0.009 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Name "Window scattering"
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "WindowMedium.hlsl"
            float4 _Tint;
            float _Density, _LightAmount;
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float t=0.5-v.positionOS.z;
                float3 local=float3(v.positionOS.xy*_WindowSize.xy+_BeamDrift.xy*_WindowSize.z*t,-_WindowSize.z*t);
                o.positionWS=TransformObjectToWorld(local);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 origin = GetCameraPositionWS();
                float3 ray = normalize(i.positionWS-origin);
                float3 a = BeamCoordinates(origin);
                float3 d = BeamCoordinates(origin+ray)-a;
                float3 invD = rcp(d + 0.000001);
                float3 t0 = -a*invD, t1=(1-a)*invD;
                float3 enter=min(t0,t1), leave=max(t0,t1);
                float start=max(0, max(enter.x,max(enter.y,enter.z)));
                float end=min(leave.x,min(leave.y,leave.z));
                float2 uv=i.positionCS.xy/_ScaledScreenParams.xy;
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 surface=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                end=min(end,distance(origin,surface));
                float span=max(0,end-start);
                clip(span-0.0001);
                float sum=0;
                // Fixed, bounded work; no full-screen renderer feature or temporal history.
                [unroll] for(int s=0;s<12;s++)
                {
                    float t=start+span*(s+0.5)/12;
                    float3 q=a+d*t;
                    float ribbons=0.48+0.52*pow(0.5+0.5*sin(q.x*15+q.y*2),2);
                    sum+=MediumMask(q)*ribbons*saturate((end-t)*7);
                }
                float3 lightRay=normalize(mul((float3x3)transpose(_WorldToWindow),float3(_BeamDrift.xy,-1)));
                float forward=pow(saturate(dot(ray,-lightRay)),4);
                float amount=sum*span/12*_Density*_LightAmount*(0.7+0.3*forward);
                return half4(_Tint.rgb*min(amount,0.075),0);
            }
            ENDHLSL
        }
    }
}
