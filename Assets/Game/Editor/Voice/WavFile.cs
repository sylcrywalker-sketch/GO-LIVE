using System;
using System.IO;
using System.Text;

namespace GoLive.Editor.Voice
{
    // Development-only 16-bit PCM WAV I/O for local speech evidence (corpus recordings, session audio). Mono.
    public static class WavFile
    {
        public static void Write(string path, float[] samples, int count, int sampleRate)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (count < 0 || count > samples.Length || count > (int.MaxValue - 36) / 2)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (sampleRate <= 0 || sampleRate > int.MaxValue / 2)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            int bytes = count * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + bytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(bytes);
            for (int i = 0; i < count; i++)
                writer.Write((short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * 32767f));
        }

        // Reads 16-bit PCM (mono, or the first channel of a multi-channel file).
        public static float[] Read(string path, out int sampleRate)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 12 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
                throw new InvalidDataException(path + " is not a WAV file");
            uint riffSize = BitConverter.ToUInt32(bytes, 4);
            if (riffSize < 4 || riffSize > bytes.Length - 8)
                throw new InvalidDataException(path + " has a truncated RIFF container");
            int limit = (int)riffSize + 8;
            int channels = 0, blockAlign = 0;
            sampleRate = 0;
            int position = 12;
            while (position < limit)
            {
                if (limit - position < 8) throw new InvalidDataException(path + " has a truncated chunk header");
                string id = Encoding.ASCII.GetString(bytes, position, 4);
                uint chunkSize = BitConverter.ToUInt32(bytes, position + 4);
                int payload = position + 8;
                if (chunkSize > limit - payload) throw new InvalidDataException(path + " has a truncated chunk");
                int size = (int)chunkSize;
                if (id == "fmt ")
                {
                    if (size < 16) throw new InvalidDataException(path + " has an incomplete PCM format");
                    int format = BitConverter.ToUInt16(bytes, payload);
                    channels = BitConverter.ToUInt16(bytes, payload + 2);
                    sampleRate = BitConverter.ToInt32(bytes, payload + 4);
                    blockAlign = BitConverter.ToUInt16(bytes, payload + 12);
                    int bits = BitConverter.ToUInt16(bytes, payload + 14);
                    if (format != 1 || channels == 0 || sampleRate <= 0 || bits != 16 || blockAlign != channels * 2)
                        throw new InvalidDataException(path + " must be 16-bit PCM with a valid channel layout");
                }
                else if (id == "data")
                {
                    if (sampleRate <= 0 || blockAlign <= 0 || size % blockAlign != 0)
                        throw new InvalidDataException(path + " must contain complete 16-bit PCM frames after its format");
                    int frames = size / blockAlign;
                    var samples = new float[frames];
                    for (int i = 0; i < frames; i++) samples[i] = BitConverter.ToInt16(bytes, payload + i * blockAlign) / 32768f;
                    return samples;
                }
                long next = (long)payload + size + (size & 1);
                if (next > limit) throw new InvalidDataException(path + " has a missing chunk pad byte");
                position = (int)next;
            }
            throw new InvalidDataException(path + " has no PCM data");
        }
    }
}
