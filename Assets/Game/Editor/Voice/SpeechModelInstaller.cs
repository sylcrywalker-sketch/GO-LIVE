using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace GoLive.Editor.Voice
{
    // Optional, explicit download of the larger multilingual whisper.cpp model (better Russian than the bundled
    // tiny model). Verified against the SHA-1 published in whisper.cpp's models/README.md. The file is not
    // versioned (see .gitignore); VoiceInputBehaviour prefers it automatically when present.
    public static class SpeechModelInstaller
    {
        private const string Url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin";
        private const string Sha1 = "465707469ff3a37a2b9b8d8f89f2f99de7299dac";

        [MenuItem("GO! LIVE/Voice/Install base speech model (142 MiB)")]
        public static void InstallBase()
        {
            string destination = Path.Combine(Application.streamingAssetsPath, "Whisper", "ggml-base.bin");
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
                using (SHA1 hash = SHA1.Create())
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
                        if (EditorUtility.DisplayCancelableProgressBar("GO! LIVE speech model", $"ggml-base.bin  {received / 1048576} MiB", progress))
                            throw new OperationCanceledException();
                    }
                    hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    string actual = BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
                    if (actual != Sha1) throw new InvalidDataException("Downloaded model checksum " + actual + " does not match " + Sha1 + ".");
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
