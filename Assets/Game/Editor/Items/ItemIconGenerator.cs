using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GoLive.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GoLive.Editor.Items
{
    // Inventory icons are rendered from each ItemDefinition's real World Prefab in an isolated preview scene: one
    // fixed studio light rig, a 3/4 camera framed on the model, and a transparent background recovered from a
    // render over black and one over white. iconOverride always wins; this tool only owns generatedIcon and writes
    // one file per Item ID, so regenerating replaces the icon instead of adding another.
    public static class ItemIconGenerator
    {
        public const string OutputFolder = "Assets/Game/UI/Generated/ItemIcons";
        public const string ContentFolder = "Assets/Game";
        public const int OutputSize = 512;

        private const string DialogTitle = "GO! LIVE Item Icons";
        private const int Supersampling = 4;
        private const float ContentFill = 0.88f;
        private const float CameraPitch = 26f;
        private const float FlatModelCameraPitch = 42f;
        private const float CameraYaw = -38f;
        private const float BaseFieldOfView = 22f;
        private const float FramingMargin = 1.04f;
        private const float OpaqueAlpha = 0.02f;

        // Very dark models are lifted towards this mean linear luminance (never darkened, never more than the cap)
        // so black plastics stay readable on the dark Inventory panel.
        private const float MinimumMeanLuminance = 0.075f;
        private const float MaximumExposureLift = 1.5f;

        private static readonly Color AmbientColor = new(0.36f, 0.37f, 0.4f);

        private static readonly LightRig[] StudioLights =
        {
            // Key from the upper left, soft fill from the right, and a rim from behind that separates dark
            // plastics from the dark Inventory background.
            new(new Vector3(42f, 38f, 0f), new Color(1f, 0.97f, 0.93f), 1.25f),
            new(new Vector3(8f, -58f, 0f), new Color(0.88f, 0.93f, 1f), 0.55f),
            new(new Vector3(28f, 160f, 0f), Color.white, 1.1f)
        };

        [MenuItem("GO LIVE/Items/Generate Missing Item Icons")]
        public static void GenerateMissingFromMenu()
        {
            ShowReport(GenerateIcons(FindItemDefinitions(), regenerateExisting: false));
        }

        [MenuItem("GO LIVE/Items/Regenerate All Item Icons")]
        public static void RegenerateAllFromMenu()
        {
            ShowReport(GenerateIcons(FindItemDefinitions(), regenerateExisting: true));
        }

        [MenuItem("GO LIVE/Items/Generate Selected Item Icon")]
        public static void GenerateSelectedFromMenu()
        {
            ItemDefinition[] selected = Selection.GetFiltered<ItemDefinition>(SelectionMode.Assets);

            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle, "Select one or more ItemDefinition assets first.", "OK");
                return;
            }

            ShowReport(GenerateIcons(selected, regenerateExisting: true, includeOverridden: true));
        }

        // Batch-mode entry point: -executeMethod GoLive.Editor.Items.ItemIconGenerator.GenerateMissingInBatch
        public static void GenerateMissingInBatch()
        {
            ItemIconReport report = GenerateIcons(FindItemDefinitions(), regenerateExisting: false);
            Debug.Log(report.Describe());

            if (Application.isBatchMode)
                EditorApplication.Exit(report.Failures.Count == 0 ? 0 : 1);
        }

        // Production item content: every ItemDefinition asset describes a physical item the player can pick up.
        public static List<ItemDefinition> FindItemDefinitions()
        {
            List<ItemDefinition> definitions = new();

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(ItemDefinition)}", new[] { ContentFolder }))
            {
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));

                if (definition != null)
                    definitions.Add(definition);
            }

            definitions.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return definitions;
        }

        public static ItemIconReport GenerateIcons(
            IReadOnlyList<ItemDefinition> definitions,
            bool regenerateExisting,
            bool includeOverridden = false,
            string outputFolder = OutputFolder)
        {
            ItemIconReport report = new();

            try
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    ItemDefinition definition = definitions[i];
                    report.Checked++;

                    if (!Application.isBatchMode)
                        EditorUtility.DisplayProgressBar(DialogTitle, definition.name, i / (float)definitions.Count);

                    if (HasOverride(definition))
                    {
                        report.Overridden.Add(definition.name);

                        if (!includeOverridden)
                            continue;
                    }

                    if (!regenerateExisting && GetGeneratedIcon(definition) != null)
                    {
                        report.AlreadyGenerated.Add(definition.name);
                        continue;
                    }

                    if (TryGenerate(definition, outputFolder, out string error))
                        report.Generated.Add(definition.name);
                    else
                        report.Failures.Add($"{definition.name}: {error}");
                }
            }
            finally
            {
                if (!Application.isBatchMode)
                    EditorUtility.ClearProgressBar();

                AssetDatabase.SaveAssets();
            }

            return report;
        }

        public static bool TryGenerate(ItemDefinition definition, string outputFolder, out string error)
        {
            error = null;

            if (definition == null)
            {
                error = "no ItemDefinition";
                return false;
            }

            if (!ItemDefinition.IsValidItemId(definition.ItemId))
            {
                error = $"invalid Item ID '{definition.ItemId}'";
                return false;
            }

            if (definition.WorldPrefab == null)
            {
                error = "no World Prefab to render";
                return false;
            }

            Texture2D icon = null;

            try
            {
                icon = RenderIcon(definition.WorldPrefab);

                string iconPath = $"{outputFolder}/{definition.ItemId}.png";
                EnsureFolder(outputFolder);
                File.WriteAllBytes(GetAbsolutePath(iconPath), icon.EncodeToPNG());

                Sprite sprite = ImportAsSprite(iconPath);

                SerializedObject serialized = new(definition);
                serialized.FindProperty("generatedIcon").objectReferenceValue = sprite;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                if (icon != null)
                    Object.DestroyImmediate(icon);
            }
        }

        // Renders the model into an OutputSize square: transparent background, model centred and scaled so its
        // longer side fills ContentFill of the frame. The caller owns the returned texture.
        public static Texture2D RenderIcon(GameObject prefab)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            int renderSize = OutputSize * Supersampling;
            Scene scene = EditorSceneManager.NewPreviewScene();

            try
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                if (!TryGetVisibleBounds(model, out Bounds bounds))
                    throw new InvalidOperationException($"'{prefab.name}' has no visible renderers.");

                Camera camera = CreateCamera(scene, bounds);
                CreateLights(scene, camera.transform.rotation);

                Color[] overBlack = Render(camera, scene, Color.black, renderSize);
                Color[] overWhite = Render(camera, scene, Color.white, renderSize);
                float[] alpha = ExtractCutout(overBlack, overWhite);

                if (!TryGetOpaqueBounds(alpha, renderSize, out RectInt opaque))
                    throw new InvalidOperationException($"'{prefab.name}' rendered no visible pixels.");

                if (opaque.xMin == 0 || opaque.yMin == 0 || opaque.xMax == renderSize || opaque.yMax == renderSize)
                    throw new InvalidOperationException($"'{prefab.name}' touches the edge of the render and would be clipped.");

                return Compose(overBlack, alpha, renderSize, opaque);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Camera CreateCamera(Scene scene, Bounds bounds)
        {
            GameObject cameraObject = new("Item Icon Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cameraType = CameraType.Preview;
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowMSAA = false;
            camera.fieldOfView = BaseFieldOfView;

            // Flat models (boards, keyboards) are seen more from above so their top face reads, not their edge.
            float footprint = Mathf.Min(bounds.size.x, bounds.size.z);
            float flatness = footprint > 0f ? Mathf.InverseLerp(0.5f, 0.15f, bounds.size.y / footprint) : 0f;
            Quaternion rotation = Quaternion.Euler(Mathf.Lerp(CameraPitch, FlatModelCameraPitch, flatness), CameraYaw, 0f);
            float radius = Mathf.Max(bounds.extents.magnitude, 0.001f);
            float distance = radius / Mathf.Sin(BaseFieldOfView * 0.5f * Mathf.Deg2Rad);

            camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * distance, rotation);
            camera.nearClipPlane = Mathf.Max(0.001f, distance - radius * 1.5f);
            camera.farClipPlane = distance + radius * 1.5f;

            // Tighten the field of view around the projected bounds so the model uses the whole render.
            float extent = 0f;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 view = camera.transform.InverseTransformPoint(corner);
                extent = Mathf.Max(extent, Mathf.Abs(view.x) / view.z, Mathf.Abs(view.y) / view.z);
            }

            camera.fieldOfView = 2f * Mathf.Atan(extent * FramingMargin) * Mathf.Rad2Deg;
            return camera;
        }

        private static void CreateLights(Scene scene, Quaternion cameraRotation)
        {
            for (int i = 0; i < StudioLights.Length; i++)
            {
                GameObject lightObject = new($"Item Icon Light {i}");
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                lightObject.transform.rotation = cameraRotation * Quaternion.Euler(StudioLights[i].Angles);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = StudioLights[i].Color;
                light.intensity = StudioLights[i].Intensity;
                light.shadows = LightShadows.None;
            }
        }

        private static Color[] Render(Camera camera, Scene scene, Color background, int size)
        {
            RenderTexture target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D readback = null;

            try
            {
                camera.targetTexture = target;
                camera.backgroundColor = background;

                Unsupported.SetOverrideLightingSettings(scene);

                try
                {
                    SphericalHarmonicsL2 ambient = new();
                    ambient.AddAmbientLight(AmbientColor);

                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = AmbientColor;
                    RenderSettings.ambientProbe = ambient;
                    RenderSettings.fog = false;

                    camera.Render();
                }
                finally
                {
                    Unsupported.RestoreOverrideLightingSettings();
                }

                RenderTexture.active = target;
                readback = new Texture2D(size, size, TextureFormat.RGBAHalf, false, true);
                readback.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                readback.Apply(false);

                return readback.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);

                if (readback != null)
                    Object.DestroyImmediate(readback);
            }
        }

        // Over black a pixel is alpha * colour; over white it gains (1 - alpha). Opaque surfaces render the same
        // on both backgrounds, so the difference is exactly the background that shows through.
        private static float[] ExtractCutout(Color[] overBlack, Color[] overWhite)
        {
            float[] alpha = new float[overBlack.Length];

            for (int i = 0; i < overBlack.Length; i++)
            {
                Color black = overBlack[i];
                Color white = overWhite[i];
                float background = Mathf.Max(white.r - black.r, Mathf.Max(white.g - black.g, white.b - black.b));

                alpha[i] = Mathf.Clamp01(1f - background);
            }

            return alpha;
        }

        // Crops to the model, scales it so its longer side fills ContentFill of the icon, and area-averages the
        // supersampled render (premultiplied, in linear space) into the final sRGB texture.
        private static Texture2D Compose(Color[] premultiplied, float[] alpha, int sourceSize, RectInt opaque)
        {
            float scale = OutputSize * ContentFill / Mathf.Max(opaque.width, opaque.height);
            float offsetX = (OutputSize - opaque.width * scale) * 0.5f;
            float offsetY = (OutputSize - opaque.height * scale) * 0.5f;
            float footprint = 1f / scale;

            Color[] linear = new Color[OutputSize * OutputSize];
            float luminanceSum = 0f;
            float coverageSum = 0f;

            for (int y = 0; y < OutputSize; y++)
            {
                float sourceY0 = opaque.yMin + (y - offsetY) * footprint;
                float sourceY1 = sourceY0 + footprint;

                for (int x = 0; x < OutputSize; x++)
                {
                    float sourceX0 = opaque.xMin + (x - offsetX) * footprint;
                    float sourceX1 = sourceX0 + footprint;

                    float weightSum = 0f;
                    float alphaSum = 0f;
                    float red = 0f;
                    float green = 0f;
                    float blue = 0f;

                    int minY = Mathf.Max(0, Mathf.FloorToInt(sourceY0));
                    int maxY = Mathf.Min(sourceSize - 1, Mathf.CeilToInt(sourceY1) - 1);
                    int minX = Mathf.Max(0, Mathf.FloorToInt(sourceX0));
                    int maxX = Mathf.Min(sourceSize - 1, Mathf.CeilToInt(sourceX1) - 1);

                    for (int sy = minY; sy <= maxY; sy++)
                    {
                        float weightY = Mathf.Min(sy + 1, sourceY1) - Mathf.Max(sy, sourceY0);

                        if (weightY <= 0f)
                            continue;

                        for (int sx = minX; sx <= maxX; sx++)
                        {
                            float weight = weightY * (Mathf.Min(sx + 1, sourceX1) - Mathf.Max(sx, sourceX0));

                            if (weight <= 0f)
                                continue;

                            int index = sy * sourceSize + sx;
                            Color colour = premultiplied[index];

                            weightSum += weight;
                            alphaSum += alpha[index] * weight;
                            red += Mathf.Max(0f, colour.r) * weight;
                            green += Mathf.Max(0f, colour.g) * weight;
                            blue += Mathf.Max(0f, colour.b) * weight;
                        }
                    }

                    if (weightSum <= 0f || alphaSum <= 0f)
                        continue;

                    Color straight = new(red / alphaSum, green / alphaSum, blue / alphaSum, Mathf.Clamp01(alphaSum / weightSum));
                    linear[y * OutputSize + x] = straight;
                    luminanceSum += (0.2126f * straight.r + 0.7152f * straight.g + 0.0722f * straight.b) * straight.a;
                    coverageSum += straight.a;
                }
            }

            float meanLuminance = coverageSum > 0f ? luminanceSum / coverageSum : 1f;
            float lift = Mathf.Clamp(MinimumMeanLuminance / Mathf.Max(meanLuminance, 0.0001f), 1f, MaximumExposureLift);
            Color32[] output = new Color32[linear.Length];

            for (int i = 0; i < linear.Length; i++)
            {
                Color pixel = linear[i];

                if (pixel.a <= 0f)
                    continue;

                output[i] = new Color32(
                    ToSrgbByte(pixel.r * lift),
                    ToSrgbByte(pixel.g * lift),
                    ToSrgbByte(pixel.b * lift),
                    (byte)Mathf.RoundToInt(pixel.a * 255f));
            }

            Texture2D texture = new(OutputSize, OutputSize, TextureFormat.RGBA32, false, false);
            texture.SetPixels32(output);
            texture.Apply(false, false);

            return texture;
        }

        private static byte ToSrgbByte(float linear)
        {
            return (byte)Mathf.RoundToInt(Mathf.LinearToGammaSpace(Mathf.Clamp01(linear)) * 255f);
        }

        private static bool TryGetVisibleBounds(GameObject model, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is not (MeshRenderer or SkinnedMeshRenderer))
                    continue;

                if (found)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    found = true;
                }
            }

            return found;
        }

        private static bool TryGetOpaqueBounds(float[] alpha, int size, out RectInt bounds)
        {
            int minX = size;
            int minY = size;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (alpha[y * size + x] <= OpaqueAlpha)
                        continue;

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            bounds = maxX < minX ? default : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return maxX >= minX;
        }

        private static Sprite ImportAsSprite(string iconPath)
        {
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;

            if (importer == null)
                throw new IOException($"Unable to configure generated icon: {iconPath}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = OutputSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            return sprite != null ? sprite : throw new IOException($"Generated icon could not be loaded as a Sprite: {iconPath}");
        }

        private static bool HasOverride(ItemDefinition definition)
        {
            return new SerializedObject(definition).FindProperty("iconOverride").objectReferenceValue != null;
        }

        private static Object GetGeneratedIcon(ItemDefinition definition)
        {
            return new SerializedObject(definition).FindProperty("generatedIcon").objectReferenceValue;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;

            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');

            if (string.IsNullOrEmpty(parent))
                throw new DirectoryNotFoundException($"Invalid icon folder: {assetFolder}");

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        private static string GetAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new DirectoryNotFoundException("Unity project root could not be resolved.");

            return Path.Combine(projectRoot, assetPath);
        }

        private static void ShowReport(ItemIconReport report)
        {
            string summary = report.Describe();

            if (report.Failures.Count > 0)
                Debug.LogError(summary);
            else
                Debug.Log(summary);

            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog(DialogTitle, summary, "OK");
        }

        private readonly struct LightRig
        {
            public Vector3 Angles { get; }
            public Color Color { get; }
            public float Intensity { get; }

            public LightRig(Vector3 angles, Color color, float intensity)
            {
                Angles = angles;
                Color = color;
                Intensity = intensity;
            }
        }
    }

    public sealed class ItemIconReport
    {
        public int Checked { get; set; }
        public List<string> Overridden { get; } = new();
        public List<string> AlreadyGenerated { get; } = new();
        public List<string> Generated { get; } = new();
        public List<string> Failures { get; } = new();

        public string Describe()
        {
            StringBuilder builder = new();
            builder.AppendLine($"Item icons: {Checked} definitions checked, {Generated.Count} generated, {AlreadyGenerated.Count} already had a generated icon, {Overridden.Count} use an authored override, {Failures.Count} failed.");

            foreach (string failure in Failures)
                builder.AppendLine($"  FAILED {failure}");

            return builder.ToString();
        }
    }
}
