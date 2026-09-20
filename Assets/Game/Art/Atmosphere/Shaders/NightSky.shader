Shader "GO LIVE/Atmosphere/Night Sky"
{
    Properties
    {
        _Exposure("Day/night exposure (WorldLighting)",Range(0,2))=0.22
        _SunDirection("Sun direction",Vector)=(0.5,0.13,-0.86,0)
        [HDR] _SunColor("Sun color",Color)=(1,0.89,0.73,1)
        _SunVisibility("Sun above horizon",Range(0,1))=1
        _SunRadius("Sun angular radius, radians",Range(0.003,0.012))=0.00465
        _MoonDirection("Moon direction",Vector)=(0.804,0.129,-0.579,0)
        _MoonVisibility("Moon above horizon",Range(0,1))=1
        _MoonRadius("Moon angular radius, radians",Range(0.003,0.025))=0.0105
        _StarIntensity("Star intensity",Range(0,2))=0.65
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Exposure,_MoonRadius,_StarIntensity,_MoonVisibility;
            float4 _MoonDirection, _SunDirection, _SunColor;
            float _SunVisibility, _SunRadius;
            struct Attributes {float4 positionOS:POSITION;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 dir:TEXCOORD0;};
            Varyings vert(Attributes v) {Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.dir=v.positionOS.xyz;return o;}
            float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p) {float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
            half4 frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.dir);
                float night=1-smoothstep(0.26,0.60,_Exposure);
                float altitude=saturate(d.y);
                float3 nightSky=lerp(float3(0.017,0.023,0.032),float3(0.0025,0.005,0.011),pow(altitude,0.4));
                float3 daySky=lerp(float3(0.46,0.54,0.61),float3(0.14,0.29,0.48),pow(altitude,0.45))*_Exposure;
                float3 col=lerp(daySky,nightSky,night);
                col=lerp(col,lerp(float3(0.12,0.135,0.15)*_Exposure,float3(0.01,0.012,0.017),night),smoothstep(0,-0.12,d.y));
                float2 skyUV=float2(atan2(d.z,d.x)/6.2831853+0.5,asin(clamp(d.y,-1,1))/3.14159265+0.5);
                float2 cells=skyUV*float2(760,380);
                float2 cell=floor(cells);
                float random=hash(cell);
                float2 jitter=float2(hash(cell+19.3),hash(cell+57.9))*0.6+0.2;
                float r=length(frac(cells)-jitter);
                float aa=max(fwidth(r),0.025);
                float footprint=0.065+aa*0.48;
                float stars=exp(-r*r/(footprint*footprint))*step(0.996,random)*min(1,0.20/max(aa,0.01));
                col+=stars*lerp(0.16,0.8,hash(cell+3.7))*_StarIntensity*night*smoothstep(0.015,0.12,d.y);
                float3 moon=normalize(_MoonDirection.xyz);
                float3 referenceUp=abs(moon.y)>0.98?float3(0,0,1):float3(0,1,0);
                float3 right=normalize(cross(referenceUp,moon));
                float3 up=cross(moon,right);
                float2 p=float2(dot(d,right),dot(d,up))/_MoonRadius;
                float diskR=length(p);
                float edge=max(fwidth(diskR),0.008);
                float disk=(1-smoothstep(1-edge,1+edge,diskR))*step(0,dot(d,moon));
                float maria=noise(p*3.1+8)*0.52+noise(p*7.2+3)*0.30+noise(p*19.5)*0.18;
                float3 n=float3(p,sqrt(saturate(1-dot(p,p))));
                float phase=saturate(dot(n,normalize(float3(-0.4,0.13,0.9))));
                float shade=lerp(0.52,0.96,maria)*(0.16+0.84*sqrt(phase));
                col=lerp(col,float3(1.22,1.24,1.20)*shade,disk*night*_MoonVisibility);
                float angle=acos(clamp(dot(d,moon),-1,1));
                col+=float3(0.45,0.53,0.65)*exp(-angle*angle/0.00085)*0.014*night*_MoonVisibility*(1-disk);
                float sunAngle=acos(clamp(dot(d,normalize(_SunDirection.xyz)),-1,1));
                float sunEdge=max(fwidth(sunAngle),0.00015);
                float sunDisk=1-smoothstep(_SunRadius-sunEdge,_SunRadius+sunEdge,sunAngle);
                float sunHalo=exp(-sunAngle*sunAngle/0.0005)*0.12+exp(-sunAngle*sunAngle/0.012)*0.018;
                col+=_SunColor.rgb*(sunDisk*5.0+sunHalo)*_SunVisibility;
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
