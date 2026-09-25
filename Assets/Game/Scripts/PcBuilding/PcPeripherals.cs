using System;
using System.Collections.Generic;

namespace GoLive.PcBuilding
{
    public enum PcPeripheralKind { None, Microphone, Webcam }

    [Serializable]
    public sealed class PcPeripheralsSnapshot
    {
        public string MicrophoneId = "";
        public string WebcamId = "";
    }

    // Owns explicit connections only. World transfers and saved item identity are verified by the bridge.
    // These are fictional character-owned items, independent of OS microphones and future voice/STT input.
    public sealed class PcPeripherals
    {
        private string _microphoneId = "";
        private string _webcamId = "";
        public bool HasMicrophone => _microphoneId.Length > 0;
        public bool HasWebcam => _webcamId.Length > 0;
        public event Action Changed;

        public string GetConnectedId(PcPeripheralKind kind) => kind switch
        {
            PcPeripheralKind.Microphone => _microphoneId,
            PcPeripheralKind.Webcam => _webcamId,
            _ => ""
        };

        public bool CanConnect(PcPeripheralKind kind, string instanceId) =>
            (kind == PcPeripheralKind.Microphone || kind == PcPeripheralKind.Webcam) &&
            !string.IsNullOrWhiteSpace(instanceId) && GetConnectedId(kind).Length == 0 &&
            instanceId != _microphoneId && instanceId != _webcamId;

        public bool TryConnect(PcPeripheralKind kind, string instanceId)
        {
            if (!CanConnect(kind, instanceId)) return false;
            if (kind == PcPeripheralKind.Microphone) _microphoneId = instanceId;
            else _webcamId = instanceId;
            Changed?.Invoke();
            return true;
        }

        public bool TryDisconnect(PcPeripheralKind kind)
        {
            if (GetConnectedId(kind).Length == 0) return false;
            if (kind == PcPeripheralKind.Microphone) _microphoneId = "";
            else _webcamId = "";
            Changed?.Invoke();
            return true;
        }

        public PcPeripheralsSnapshot Capture() => new() { MicrophoneId = _microphoneId, WebcamId = _webcamId };

        public string Validate(PcPeripheralsSnapshot snapshot, IReadOnlyDictionary<string, PcPeripheralKind> installed)
        {
            if (snapshot == null || snapshot.MicrophoneId == null || snapshot.WebcamId == null || installed == null)
                return "Peripheral connections are missing.";
            int count = 0;
            if (!ValidConnection(snapshot.MicrophoneId, PcPeripheralKind.Microphone, installed, ref count) ||
                !ValidConnection(snapshot.WebcamId, PcPeripheralKind.Webcam, installed, ref count))
                return "Peripheral connection does not match its physical item.";
            if (snapshot.MicrophoneId.Length > 0 && snapshot.MicrophoneId == snapshot.WebcamId)
                return "A peripheral cannot occupy two connections.";
            return installed.Count == count ? null : "An installed peripheral has no connection.";
        }

        public void Restore(PcPeripheralsSnapshot snapshot, IReadOnlyDictionary<string, PcPeripheralKind> installed)
        {
            string error = Validate(snapshot, installed);
            if (error != null) throw new ArgumentException(error, nameof(snapshot));
            bool changed = _microphoneId != snapshot.MicrophoneId || _webcamId != snapshot.WebcamId;
            _microphoneId = snapshot.MicrophoneId;
            _webcamId = snapshot.WebcamId;
            if (changed) Changed?.Invoke();
        }

        private static bool ValidConnection(string id, PcPeripheralKind kind,
            IReadOnlyDictionary<string, PcPeripheralKind> installed, ref int count)
        {
            if (id.Length == 0) return true;
            if (string.IsNullOrWhiteSpace(id) || !installed.TryGetValue(id, out PcPeripheralKind actual) || actual != kind) return false;
            count++;
            return true;
        }
    }
}
