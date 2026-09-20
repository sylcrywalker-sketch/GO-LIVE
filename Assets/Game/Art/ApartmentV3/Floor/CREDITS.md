# Worn pine floor

Source: Wood Floor Worn, Poly Haven (Dimitrios Savva).
https://polyhaven.com/a/wood_floor_worn
License: CC0 https://polyhaven.com/license

Original diffuse 4096, OpenGL normal 2048 and AO 2048 are retained without image editing. The Unity metallic/smoothness map packs metallic=0 and alpha=1-original roughness. Import normal as Normal Map; data maps are linear. No displacement/tessellation or duplicate detail normal.

GL uses a saved mesh with per-board UV selection and longitudinal offsets, on the exact original L-shaped floor footprint. The original MeshCollider remains unchanged. Four shared URP/Lit finish variants use the same maps; no runtime materials, generation, or extra MonoBehaviour.
