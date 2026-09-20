#ifndef GL_WINDOW_MEDIUM_INCLUDED
#define GL_WINDOW_MEDIUM_INCLUDED
float4x4 _WorldToWindow;
float4 _WindowSize; // width, height, inward depth, edge feather
float4 _BeamDrift; // local x/y offset per inward metre
float4 _SourceVisibility0, _SourceVisibility1, _SourceVisibility2;
float SourceVisibility(float2 aperture)
{
    float2 grid = saturate(aperture) * 2;
    float2 cell = min(floor(grid), 1);
    float2 blend = smoothstep(0, 1, grid - cell);
    float3 bottom = lerp(_SourceVisibility0.xyz, _SourceVisibility1.xyz, cell.y);
    float3 top = lerp(_SourceVisibility1.xyz, _SourceVisibility2.xyz, cell.y);
    float2 lowPair = lerp(bottom.xy, bottom.yz, cell.x);
    float2 highPair = lerp(top.xy, top.yz, cell.x);
    return saturate(lerp(lerp(lowPair.x, lowPair.y, blend.x), lerp(highPair.x, highPair.y, blend.x), blend.y));
}
float3 BeamCoordinates(float3 world)
{
    float3 p = mul(_WorldToWindow, float4(world, 1)).xyz;
    float distanceIn = -p.z;
    return float3((p.xy - _BeamDrift.xy * distanceIn) / _WindowSize.xy + 0.5, distanceIn / _WindowSize.z);
}
float MediumMask(float3 q)
{
    float2 edge = min(q.xy, 1-q.xy);
    float border = smoothstep(0, _WindowSize.w, min(edge.x,edge.y));
    float ends = smoothstep(0, 0.04, q.z) * (1-smoothstep(0.65, 1, q.z));
    return border * ends * SourceVisibility(q.xy);
}
#endif
