using System;
using System.Collections.Generic;
using GoLive.PcBuilding;

namespace GoLive.Desktop
{
    public enum StreamState { Offline, Starting, Live, Stopping }
    public enum StreamQuality { Low, Medium, High }

    public sealed class StreamSummary
    {
        public string Id { get; }
        public long Sequence { get; }
        public double DurationSeconds { get; }
        public int PeakViewers { get; }
        public int Followers { get; }
        public long DonationCents { get; }
        public bool Aborted { get; }
        public StreamSummary(string id, long sequence, double durationSeconds, int peakViewers, int followers, long donationCents, bool aborted)
        {
            Id = id; Sequence = sequence; DurationSeconds = durationSeconds; PeakViewers = peakViewers;
            Followers = followers; DonationCents = donationCents; Aborted = aborted;
        }
    }

    public sealed class StreamChatMessage
    {
        public string Id { get; }
        public string SenderName { get; }
        public string BodyKey { get; }
        internal StreamChatMessage(string id, string senderName, string bodyKey) { Id = id; SenderName = senderName; BodyKey = bodyKey; }
    }

    public sealed class StreamSession
    {
        public const int MaximumChatMessages = 60;
        public StreamState State { get; private set; }
        public StreamQuality Quality { get; private set; }
        public bool IsConnected => _channel.IsRegistered && _connectedCode.Length > 0 && string.Equals(_connectedCode, _channel.ChannelCode, StringComparison.Ordinal);
        public double DurationSeconds { get; private set; }
        public int Viewers { get; private set; }
        public int Followers { get; private set; }
        public long DonationCents { get; private set; }
        public IReadOnlyList<StreamChatMessage> Chat { get; }
        public event Action Changed;
        public event Action<StreamChatMessage> ChatAdded;
        public event Action<StreamSummary> Completed;
        private readonly TrichChannel _channel;
        private readonly DonationAccount _donation;
        private readonly PcPeripherals _peripherals;
        private readonly List<StreamChatMessage> _chat = new();
        private string _connectedCode = "";
        private string _streamId = "";
        private long _sequence;
        private long _chatSerial;
        private int _receiptSerial;
        private int _peakViewers;
        private double _transitionRemaining;
        private double _chatIntervals;
        private double _donationIntervals;
        private bool _hasBeenLive;

        // All commands and synchronous observers run on the owning game thread. Only this instance owns
        // the current broadcast. The root commits Completed to Trich and Outline; it must do so before Start.
        public StreamSession(TrichChannel channel, DonationAccount donation, PcPeripherals peripherals = null)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _donation = donation ?? throw new ArgumentNullException(nameof(donation));
            _peripherals = peripherals ?? new PcPeripherals();
            Chat = _chat.AsReadOnly();
        }

        public string Connect(string code)
        {
            if (State != StreamState.Offline) return "desktop.stream.busy";
            if (!_channel.IsRegistered) return "desktop.stream.channel_required";
            string normalized = code?.Trim().ToLowerInvariant();
            if (!string.Equals(normalized, _channel.ChannelCode, StringComparison.Ordinal)) return "desktop.stream.invalid_code";
            if (_connectedCode == normalized) return null;
            _connectedCode = normalized;
            Changed?.Invoke();
            return null;
        }

        public string Disconnect()
        {
            if (State != StreamState.Offline) return "desktop.stream.busy";
            if (_connectedCode.Length == 0) return null;
            _connectedCode = "";
            Changed?.Invoke();
            return null;
        }

        public string SetQuality(StreamQuality quality)
        {
            if (State != StreamState.Offline) return "desktop.stream.busy";
            if (quality < StreamQuality.Low || quality > StreamQuality.High) return "desktop.stream.invalid_quality";
            if (Quality == quality) return null;
            Quality = quality;
            Changed?.Invoke();
            return null;
        }

        public string CheckStart(PcCapabilities capabilities, bool powered, float uploadMbps)
            => EvaluateReadiness(capabilities, powered, uploadMbps).ErrorKey;

        public StreamReadiness EvaluateReadiness(PcCapabilities capabilities, bool powered, float uploadMbps)
        {
            string error = State != StreamState.Offline ? "desktop.stream.busy" :
                !IsConnected ? "desktop.stream.not_connected" :
                _channel.CompletedStreams == long.MaxValue ? "desktop.stream.total_limit" :
                EnvironmentError(capabilities, powered, uploadMbps);
            return new StreamReadiness(IsConnected, DesktopAccountValidation.FiniteNonnegative(uploadMbps) && uploadMbps >= MinimumUpload,
                _peripherals.HasMicrophone, _peripherals.HasWebcam,
                capabilities.CanUseDesktop && (Quality != StreamQuality.High || capabilities.GamingGraphicsAvailable), error);
        }

        private float MinimumUpload => Quality == StreamQuality.Low ? 1 : Quality == StreamQuality.Medium ? 3 : 6;

        public string Start(PcCapabilities capabilities, bool powered, float uploadMbps)
        {
            string error = CheckStart(capabilities, powered, uploadMbps);
            if (error != null) return error;
            _streamId = Guid.NewGuid().ToString("N");
            _sequence = _channel.CompletedStreams + 1;
            _chatSerial = 0;
            _receiptSerial = 0;
            _peakViewers = 0;
            _chatIntervals = 0;
            _donationIntervals = 0;
            _hasBeenLive = false;
            DurationSeconds = 0;
            Viewers = 0;
            Followers = 0;
            DonationCents = 0;
            _chat.Clear();
            State = StreamState.Starting;
            _transitionRemaining = 0.75;
            Changed?.Invoke();
            return null;
        }

        public string Stop()
        {
            if (State != StreamState.Live) return "desktop.stream.not_live";
            State = StreamState.Stopping;
            _transitionRemaining = 0.25;
            Changed?.Invoke();
            return null;
        }

        public void Abort()
        {
            if (State != StreamState.Offline) Finish(true);
        }

        // Load-time discard, unlike Abort: no session summary is committed from the world being replaced.
        public void Reset()
        {
            State = StreamState.Offline;
            Quality = StreamQuality.Low;
            _connectedCode = "";
            _streamId = "";
            _sequence = 0;
            _chatSerial = 0;
            _receiptSerial = 0;
            _peakViewers = 0;
            _transitionRemaining = 0;
            _chatIntervals = 0;
            _donationIntervals = 0;
            _hasBeenLive = false;
            DurationSeconds = 0;
            Viewers = 0;
            Followers = 0;
            DonationCents = 0;
            _chat.Clear();
            Changed?.Invoke();
        }

        // Pure value checks: safe to call whenever power, assembly, or upload configuration changes.
        public void RefreshEnvironment(PcCapabilities capabilities, bool powered, float uploadMbps)
        {
            if (State != StreamState.Offline && (!IsConnected || EnvironmentError(capabilities, powered, uploadMbps) != null)) Abort();
            else Changed?.Invoke();
        }

        public void Tick(float deltaSeconds)
        {
            if (!DesktopAccountValidation.FiniteNonnegative(deltaSeconds)) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (deltaSeconds == 0 || State == StreamState.Offline) return;
            string streamId = _streamId;
            double elapsed = deltaSeconds;
            if (State == StreamState.Stopping)
            {
                _transitionRemaining -= elapsed;
                if (_transitionRemaining <= 0) Finish(false);
                return;
            }
            if (State == StreamState.Starting)
            {
                if (elapsed < _transitionRemaining)
                {
                    _transitionRemaining -= elapsed;
                    return;
                }
                elapsed -= _transitionRemaining;
                _transitionRemaining = 0;
                State = StreamState.Live;
                _hasBeenLive = true;
                Changed?.Invoke();
                if (State != StreamState.Live || streamId != _streamId) return;
            }
            if (elapsed == 0) return;
            double previousWholeSecond = Math.Floor(DurationSeconds);
            DurationSeconds = DurationSeconds > double.MaxValue - elapsed ? double.MaxValue : DurationSeconds + elapsed;
            double chatIntervals = Math.Floor(DurationSeconds / 5);
            Viewers = chatIntervals == 0 ? 0 : (int)Math.Min(250, chatIntervals + 2);
            _peakViewers = Math.Max(_peakViewers, Viewers);
            Followers = (int)Math.Min(int.MaxValue, Math.Floor(DurationSeconds / 15));

            // Arithmetic catch-up keeps huge valid deltas bounded. Donation's finite receipt capacity
            // caps work; chat materializes only the last visible entries. Ordinary frame sizes produce
            // the same totals and authored chat sequence, with no random gameplay outcomes.
            double donationIntervals = Math.Floor(DurationSeconds / 30);
            int receiptCount = (int)Math.Min(_donation.RemainingReceiptCapacity, Math.Max(0, donationIntervals - _donationIntervals));
            _donationIntervals = donationIntervals;
            for (int i = 0; i < receiptCount; i++)
            {
                string id = _streamId + ".donation." + ++_receiptSerial;
                DonationCents += 100;
                string error = _donation.Receive(id, "PixelFox", 100);
                if (error != null)
                {
                    DonationCents -= 100;
                    break;
                }
                if (State != StreamState.Live || streamId != _streamId) return;
            }

            int chatCount = (int)Math.Min(MaximumChatMessages, Math.Max(0, chatIntervals - _chatIntervals));
            _chatIntervals = chatIntervals;
            int finalVariation = (int)(chatIntervals % 4);
            for (int i = 0; i < chatCount; i++)
            {
                if (_chatSerial == long.MaxValue) break;
                int variation = ((finalVariation - chatCount + 1 + i) % 4 + 4) % 4;
                var message = new StreamChatMessage(_streamId + ".chat." + ++_chatSerial, Sender(variation), ChatKey(variation));
                if (_chat.Count == MaximumChatMessages) _chat.RemoveAt(0);
                _chat.Add(message);
                ChatAdded?.Invoke(message);
                if (State != StreamState.Live || streamId != _streamId) return;
            }
            if (previousWholeSecond != Math.Floor(DurationSeconds)) Changed?.Invoke();
        }

        private string EnvironmentError(PcCapabilities capabilities, bool powered, float uploadMbps)
        {
            if (!powered) return "desktop.stream.pc_off";
            if (!capabilities.CanUseDesktop) return "desktop.stream.desktop_required";
            if (!DesktopAccountValidation.FiniteNonnegative(uploadMbps) || uploadMbps == 0) return "desktop.stream.internet_missing";
            if (!_peripherals.HasMicrophone) return "desktop.stream.microphone_missing";
            if (Quality == StreamQuality.High && !capabilities.GamingGraphicsAvailable) return "desktop.stream.gpu_required";
            return uploadMbps < MinimumUpload ? "desktop.stream.upload_low" : null;
        }

        private void Finish(bool aborted)
        {
            StreamSummary summary = _hasBeenLive
                ? new StreamSummary(_streamId, _sequence, DurationSeconds, _peakViewers, Followers, DonationCents, aborted)
                : null;
            // Clear completion eligibility before calling any observer, so reentrant Abort cannot
            // reissue the summary and the root can safely begin another broadcast from Completed.
            _hasBeenLive = false;
            _transitionRemaining = 0;
            State = StreamState.Offline;
            if (summary != null) Completed?.Invoke(summary);
            Changed?.Invoke();
        }

        private static string Sender(int variation) => variation switch
        {
            0 => "PixelFox", 1 => "NightOwl", 2 => "ByteCat", _ => "ArcadeKid"
        };

        private static string ChatKey(int variation) => variation switch
        {
            0 => "desktop.stream.chat.hello", 1 => "desktop.stream.chat.looks_good",
            2 => "desktop.stream.chat.nice_play", _ => "desktop.stream.chat.keep_going"
        };
    }
}
