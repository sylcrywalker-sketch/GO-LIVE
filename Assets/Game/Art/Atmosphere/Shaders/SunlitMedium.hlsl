#ifndef GL_SUNLIT_MEDIUM_INCLUDED
#define GL_SUNLIT_MEDIUM_INCLUDED

// Shared by the window air and dust: how much direct sun reaches a point in the room's air, and how strongly the air
// scatters it toward the camera. Needs Lighting.hlsl and the _MAIN_LIGHT_SHADOWS(_CASCADE) keywords; the project does
// not use screen-space main light shadows.

// Direct sun at a world position: one hardware-filtered tap of the main light shadow map (1 = lit).
half SunlitShadow(float3 positionWS)
{
    return MainLightRealtimeShadow(TransformWorldToShadowCoord(positionWS));
}

// Henyey-Greenstein phase, scaled so isotropic scattering is 1. cosTheta = dot(camera-to-point, direction to the sun).
half SunlitPhase(half cosTheta, half anisotropy)
{
    half g2 = anisotropy * anisotropy;
    return (1.0h - g2) / pow(max(1.0h + g2 - 2.0h * anisotropy * cosTheta, 0.001h), 1.5h);
}

// Stable per-pixel offset for ray-march samples (interleaved gradient noise).
float SunlitJitter(float2 pixel)
{
    return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
}

#endif
