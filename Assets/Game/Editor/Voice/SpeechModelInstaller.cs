using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace GoLive.Editor.Voice
{
    // Explicit local installation of the model selected by the real-corpus comparison. SHA-256 is the
    // official Hugging Face LFS object hash retained in the benchmark download manifest. Weights are gitignored.
    public static class SpeechModelInstaller
    {
        private const string Model = "ggml-large-v3-turbo-q5_0.bin";
        private const string Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/" + Model;
        private const string Sha256 = "394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2";

        [MenuItem("GO! LIVE/Voice/Install selected turbo speech model (548 MiB)")]
        public static void InstallShippingModel()
        {
            string destination = Path.Combine(Application.streamingAssetsPath, "Whisper", Model);
            string temporary = destination + ".download";
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
                using HttpResponseMessage response = client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? -1;
                using (Stream source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                using (FileStream target = File.Create(temporary))
                using (SHA256 hash = SHA256.Create())
                {
                    var buffer = new byte[1 << 20];
                    long received = 0;
                    int read;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        target.Write(buffer, 0, read);
                        hash.TransformBlock(buffer, 0, read, null, 0);
                        received += read;
                        float progress = total > 0 ? received / (float)total : 0;
                        if (EditorUtility.DisplayCancelableProgressBar("GO! LIVE speech model", $"{Model}  {received / 1048576} MiB", progress))
                            throw new OperationCanceledException();
                    }
                    hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    string actual = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
                    if (actual != Sha256) throw new InvalidDataException("Downloaded model checksum " + actual + " does not match " + Sha256 + ".");
                }
                if (File.Exists(destination)) File.Delete(destination);
                File.Move(temporary, destination);
                AssetDatabase.Refresh();
                Debug.Log("Installed " + destination + ". Voice recognition uses it from the next Play Mode session.");
            }
            catch (Exception exception)
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                Debug.LogError("Speech model was not installed: " + exception.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
