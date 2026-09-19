using System;
using System.Collections.Generic;
using System.IO;
using GoLive.Items;
using UnityEditor;
using UnityEngine;

namespace GoLive.Editor.Items
{
    public static class ItemIconGenerator
    {
        private const string OutputFolder = "Assets/Game/UI/Generated/ItemIcons";
        private const int OutputSize = 512;
        private const float ContentFill = 0.82f;
        private const double GenerationTimeoutSeconds = 10d;
        private const int MinimumBackgroundTolerance = 28;
        private const int MaximumBackgroundTolerance = 80;
        private const int BackgroundTolerancePadding = 12;

        private static ItemDefinition _pendingDefinition;
        private static double _generationStartedAt;

        [MenuItem("GO LIVE/Items/Generate Selected Item Icon")]
        public static void GenerateSelected()
        {
            if (Selection.activeObject is not ItemDefinition definition)
            {
                EditorUtility.DisplayDialog("GO! LIVE Item Icon", "Select an ItemDefinition asset first.", "OK");
                return;
            }

            if (!ItemDefinition.IsValidItemId(definition.ItemId))
            {
                EditorUtility.DisplayDialog("GO! LIVE Item Icon", "The selected ItemDefinition has an invalid Item ID.", "OK");
                return;
            }

            if (definition.WorldPrefab == null)
            {
                EditorUtility.DisplayDialog("GO! LIVE Item Icon", "Assign a World Prefab before generating an icon.", "OK");
                return;
            }

            Stop();

            _pendingDefinition = definition;
            _generationStartedAt = EditorApplication.timeSinceStartup;

            AssetPreview.GetAssetPreview(definition.WorldPrefab);

            EditorApplication.update += Process;
        }

        private static void Process()
        {
            if (_pendingDefinition == null)
            {
                Stop();
                return;
            }

            Texture2D preview = AssetPreview.GetAssetPreview(_pendingDefinition.WorldPrefab);

            if (preview == null)
            {
                if (EditorApplication.timeSinceStartup - _generationStartedAt < GenerationTimeoutSeconds)
                    return;

                Debug.LogError($"Unity could not generate a preview for {_pendingDefinition.name}.", _pendingDefinition);
                Stop();
                return;
            }

            ItemDefinition definition = _pendingDefinition;
            Texture2D readablePreview = null;
            Texture2D transparentPreview = null;
            Texture2D finalTexture = null;

            try
            {
                EnsureOutputFolder();

                readablePreview = MakeReadable(preview);
                transparentPreview = RemoveConnectedBackground(readablePreview);

                if (!TryGetOpaqueBounds(transparentPreview, out RectInt opaqueBounds))
                    throw new InvalidOperationException($"Background removal produced no visible pixels for '{definition.ItemId}'.");

                finalTexture = CreateFittedTexture(transparentPreview, opaqueBounds);

                string iconPath = $"{OutputFolder}/{definition.ItemId}.png";
                File.WriteAllBytes(GetAbsolutePath(iconPath), finalTexture.EncodeToPNG());

                AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);

                TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;

                if (importer == null)
                    throw new IOException($"Unable to configure generated icon: {iconPath}");

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = OutputSize;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

                if (sprite == null)
                    throw new IOException($"Generated icon could not be loaded as a Sprite: {iconPath}");

                Undo.RecordObject(definition, "Generate Item Icon");

                SerializedObject serializedDefinition = new(definition);
                SerializedProperty generatedIconProperty = serializedDefinition.FindProperty("generatedIcon");

                if (generatedIconProperty == null)
                    throw new InvalidOperationException($"Serialized field 'generatedIcon' was not found on {nameof(ItemDefinition)}.");

                generatedIconProperty.objectReferenceValue = sprite;
                serializedDefinition.ApplyModifiedProperties();

                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();

                Debug.Log($"Generated transparent inventory icon for {definition.ItemId}: {iconPath}", definition);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                EditorUtility.DisplayDialog(
                    "GO! LIVE Item Icon",
                    $"Generation failed:\n\n{exception.Message}",
                    "OK");
            }
            finally
            {
                DestroyImmediate(readablePreview);
                DestroyImmediate(transparentPreview);
                DestroyImmediate(finalTexture);
                Stop();
            }
        }

        private static Texture2D RemoveConnectedBackground(Texture2D source)
        {
            int width = source.width;
            int height = source.height;

            Color32[] pixels = source.GetPixels32();
            Color32 background = CalculateCornerBackground(pixels, width, height);
            int tolerance = CalculateBackgroundTolerance(pixels, width, height, background);
            int toleranceSquared = tolerance * tolerance;

            bool[] visited = new bool[pixels.Length];
            Queue<int> queue = new();

            for (int x = 0; x < width; x++)
            {
                TryEnqueueBackground(x, 0, width, pixels, background, toleranceSquared, visited, queue);
                TryEnqueueBackground(x, height - 1, width, pixels, background, toleranceSquared, visited, queue);
            }

            for (int y = 1; y < height - 1; y++)
            {
                TryEnqueueBackground(0, y, width, pixels, background, toleranceSquared, visited, queue);
                TryEnqueueBackground(width - 1, y, width, pixels, background, toleranceSquared, visited, queue);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;

                pixels[index].a = 0;

                TryEnqueueBackground(x - 1, y, width, height, pixels, background, toleranceSquared, visited, queue);
                TryEnqueueBackground(x + 1, y, width, height, pixels, background, toleranceSquared, visited, queue);
                TryEnqueueBackground(x, y - 1, width, height, pixels, background, toleranceSquared, visited, queue);
                TryEnqueueBackground(x, y + 1, width, height, pixels, background, toleranceSquared, visited, queue);
            }

            Texture2D result = new(width, height, TextureFormat.RGBA32, false);
            result.SetPixels32(pixels);
            result.Apply(false, false);

            return result;
        }

        private static void TryEnqueueBackground(
            int x,
            int y,
            int width,
            Color32[] pixels,
            Color32 background,
            int toleranceSquared,
            bool[] visited,
            Queue<int> queue)
        {
            int height = pixels.Length / width;
            TryEnqueueBackground(x, y, width, height, pixels, background, toleranceSquared, visited, queue);
        }

        private static void TryEnqueueBackground(
            int x,
            int y,
            int width,
            int height,
            Color32[] pixels,
            Color32 background,
            int toleranceSquared,
            bool[] visited,
            Queue<int> queue)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                return;

            int index = y * width + x;

            if (visited[index])
                return;

            visited[index] = true;

            Color32 pixel = pixels[index];

            if (pixel.a <= 3 || ColorDistanceSquared(pixel, background) <= toleranceSquared)
                queue.Enqueue(index);
        }

        private static Color32 CalculateCornerBackground(Color32[] pixels, int width, int height)
        {
            Color32 bottomLeft = pixels[0];
            Color32 bottomRight = pixels[width - 1];
            Color32 topLeft = pixels[(height - 1) * width];
            Color32 topRight = pixels[height * width - 1];

            return new Color32(
                (byte)((bottomLeft.r + bottomRight.r + topLeft.r + topRight.r) / 4),
                (byte)((bottomLeft.g + bottomRight.g + topLeft.g + topRight.g) / 4),
                (byte)((bottomLeft.b + bottomRight.b + topLeft.b + topRight.b) / 4),
                255);
        }

        private static int CalculateBackgroundTolerance(Color32[] pixels, int width, int height, Color32 background)
        {
            List<int> distances = new((width + height) * 2);

            for (int x = 0; x < width; x++)
            {
                distances.Add(Mathf.RoundToInt(Mathf.Sqrt(ColorDistanceSquared(pixels[x], background))));
                distances.Add(Mathf.RoundToInt(Mathf.Sqrt(ColorDistanceSquared(pixels[(height - 1) * width + x], background))));
            }

            for (int y = 1; y < height - 1; y++)
            {
                distances.Add(Mathf.RoundToInt(Mathf.Sqrt(ColorDistanceSquared(pixels[y * width], background))));
                distances.Add(Mathf.RoundToInt(Mathf.Sqrt(ColorDistanceSquared(pixels[y * width + width - 1], background))));
            }

            distances.Sort();

            int percentileIndex = Mathf.Clamp(Mathf.RoundToInt((distances.Count - 1) * 0.9f), 0, distances.Count - 1);
            int tolerance = distances[percentileIndex] + BackgroundTolerancePadding;

            return Mathf.Clamp(tolerance, MinimumBackgroundTolerance, MaximumBackgroundTolerance);
        }

        private static int ColorDistanceSquared(Color32 left, Color32 right)
        {
            int red = left.r - right.r;
            int green = left.g - right.g;
            int blue = left.b - right.b;

            return red * red + green * green + blue * blue;
        }

        private static Texture2D CreateFittedTexture(Texture2D source, RectInt opaqueBounds)
        {
            Texture2D result = new(OutputSize, OutputSize, TextureFormat.RGBA32, false);
            result.SetPixels32(new Color32[OutputSize * OutputSize]);

            source.filterMode = FilterMode.Bilinear;
            source.wrapMode = TextureWrapMode.Clamp;

            int longestSide = Mathf.Max(opaqueBounds.width, opaqueBounds.height);
            int targetLongestSide = Mathf.RoundToInt(OutputSize * ContentFill);
            float scale = targetLongestSide / (float)longestSide;

            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(opaqueBounds.width * scale));
            int targetHeight = Mathf.Max(1, Mathf.RoundToInt(opaqueBounds.height * scale));
            int startX = (OutputSize - targetWidth) / 2;
            int startY = (OutputSize - targetHeight) / 2;

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    float sourceX = opaqueBounds.xMin + ((x + 0.5f) / targetWidth) * opaqueBounds.width;
                    float sourceY = opaqueBounds.yMin + ((y + 0.5f) / targetHeight) * opaqueBounds.height;

                    Color color = source.GetPixelBilinear(sourceX / source.width, sourceY / source.height);
                    result.SetPixel(startX + x, startY + y, color);
                }
            }

            result.Apply(false, false);

            return result;
        }

        private static bool TryGetOpaqueBounds(Texture2D texture, out RectInt bounds)
        {
            Color32[] pixels = texture.GetPixels32();

            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    Color32 pixel = pixels[y * texture.width + x];

                    if (pixel.a <= 8)
                        continue;

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                bounds = default;
                return false;
            }

            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        private static Texture2D MakeReadable(Texture source)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);

            RenderTexture previous = RenderTexture.active;

            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                Texture2D result = new(source.width, source.height, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                result.Apply(false, false);

                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static void EnsureOutputFolder()
        {
            string absolutePath = GetAbsolutePath(OutputFolder);

            if (Directory.Exists(absolutePath))
                return;

            Directory.CreateDirectory(absolutePath);
            AssetDatabase.Refresh();
        }

        private static string GetAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new DirectoryNotFoundException("Unity project root could not be resolved.");

            return Path.Combine(projectRoot, assetPath);
        }

        private static void DestroyImmediate(UnityEngine.Object target)
        {
            if (target != null)
                UnityEngine.Object.DestroyImmediate(target);
        }

        private static void Stop()
        {
            EditorApplication.update -= Process;
            _pendingDefinition = null;
            _generationStartedAt = 0d;
        }
    }
}