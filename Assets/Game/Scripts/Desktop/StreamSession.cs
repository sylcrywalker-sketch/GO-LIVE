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
        public double AverageViewers { get; }
        public int Followers { get; }
        public int Subscriptions { get; }
        public long DonationCents { get; }
        public bool Aborted { get; }
        public StreamSummary(string id, long sequence, double durationSeconds, int peakViewers, double averageViewers,
            int followers, int subscriptions, long donationCents, bool aborted)
        {
            Id = id; Sequence = sequence; DurationSeconds = durationSeconds; PeakViewers = peakViewers; AverageViewers = averageViewers;
            Followers = followers; Subscriptions = subscriptions; DonationCents = donationCents; Aborted = aborted;
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
        // The current (or last finished) broadcast's audience: the only source of viewer counts.
        public AudienceSimulation Audience { get; private set; }
        // Support actually accepted by the donation account during this broadcast.
        public long DonationCents { get; private set; }
        public IReadOnlyList<StreamChatMessage> Chat { get; }
        public event Action Changed;
        public event Action<StreamChatMessage> ChatAdded;
        public event Action<StreamSummary> Completed;
        private readonly TrichChannel _channel;
        private readonly DonationAccount _donation;
        private readonly PcPeripherals _peripherals;
        private readonly AudienceTuning _tuning;
        private readonly AudienceRandom _seeds;
        private readonly List<StreamChatMessage> _chat = new();
        private readonly List<long> _newDonations = new();
        private string _connectedCode = "";
        private string _streamId = "";
        private long _sequence;
        private long _chatSerial;
        private int _receiptSerial;
        private double _transitionRemaining;
        private bool _hasBeenLive;
        private bool _dedicatedGraphics;
        private float _uploadMbps;

        // All commands and synchronous observers run on the owning game thread. Only this instance owns
        // the current broadcast. The root commits Completed to Trich and Outline; it must do so before Start.
        // Seeds come from one explicit random stream so tests can reproduce an audience exactly.
        public StreamSession(TrichChannel channel, DonationAccount donation, PcPeripherals peripherals = null,
            AudienceTuning audienceTuning = null, AudienceRandom audienceSeeds = null)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _donation = donation ?? throw new ArgumentNullException(nameof(donation));
            _peripherals = peripherals ?? new PcPeripherals();
            _tuning = audienceTuning ?? new AudienceTuning();
            string tuningError = _tuning.Validate();
            if (tuningError != null) throw new ArgumentException(tuningError, nameof(audienceTuning));
            _seeds = audienceSeeds ?? AudienceRandom.FromEntropy();
            Audience = IdleAudience();
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

        private float MinimumUpload => MinimumUploadMbps(Quality);

        public static float MinimumUploadMbps(StreamQuality quality) => quality == StreamQuality.Low ? 1 : quality == StreamQuality.Medium ? 3 : 6;

        public string Start(PcCapabilities capabilities, bool powered, float uploadMbps)
        {
            string error = CheckStart(capabilities, powered, uploadMbps);
            if (error != null) return error;
            _streamId = Guid.NewGuid().ToString("N");
            _sequence = _channel.CompletedStreams + 1;
            _chatSerial = 0;
            _receiptSerial = 0;
            _hasBeenLive = false;
            _dedicatedGraphics = capabilities.GamingGraphicsAvailable;
            _uploadMbps = uploadMbps;
            // Onboarding discovery applies only while the channel has never completed a broadcast.
            Audience = new AudienceSimulation(_tuning, _seeds.NextUInt64(), _channel.TotalFollowers, _channel.CompletedStreams == 0);
            DurationSeconds = 0;
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
            _transitionRemaining = 0;
            _hasBeenLive = false;
            DurationSeconds = 0;
            Audience = IdleAudience();
            DonationCents = 0;
            _chat.Clear();
            Changed?.Invoke();
        }

        // Pure value checks: safe to call whenever power, assembly, or upload configuration changes.
        public void RefreshEnvironment(PcCapabilities capabilities, bool powered, float uploadMbps)
        {
            _dedicatedGraphics = capabilities.GamingGraphicsAvailable;
            _uploadMbps = uploadMbps;
            if (State != StreamState.Offline && (!IsConnected || EnvironmentError(capabilities, powered, uploadMbps) != null)) Abort();
            else Changed?.Invoke();
        }

        // minuteOfDay is the game clock's current minute (0-1439): the audience depends on the time of day.
        public void Tick(float deltaSeconds, int minuteOfDay)
        {
            if (!DesktopAccountValidation.FiniteNonnegative(deltaSeconds)) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (minuteOfDay < 0 || minuteOfDay >= 1440) throw new ArgumentOutOfRangeException(nameof(minuteOfDay));
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
                Audience.GoLive();
                Changed?.Invoke();
                if (State != StreamState.Live || streamId != _streamId) return;
            }
            if (elapsed == 0) return;
            double previousWholeSecond = Math.Floor(DurationSeconds);
            DurationSeconds = DurationSeconds > double.MaxValue - elapsed ? double.MaxValue : DurationSeconds + elapsed;

            // The audience simulation decides who watches and what they do; this session only applies the
            // outcomes it owns: support receipts (idempotent ids) and the bounded visible chat.
            _newDonations.Clear();
            AudienceAdvance advance = Audience.Advance(elapsed, new AudienceConditions(minuteOfDay, Quality, _uploadMbps,
                _dedicatedGraphics, _peripherals.HasMicrophone, _peripherals.HasWebcam), _newDonations);
            for (int i = 0; i < _newDonations.Count && _donation.RemainingReceiptCapacity > 0; i++)
            {
                string id = _streamId + ".donation." + ++_receiptSerial;
                long amount = _newDonations[i];
                if (DonationCents > long.MaxValue - amount) break;
                // Counted before observers run, so a completion triggered by a receipt observer includes it.
                DonationCents += amount;
                if (_donation.Receive(id, SupporterName(_receiptSerial), amount) != null)
                {
                    DonationCents -= amount;
                    break;
                }
                if (State != StreamState.Live || streamId != _streamId) return;
            }

            // A huge delta materializes only the last visible entries; serials still count every message.
            long chatCount = advance.ChatMessages;
            long visible = Math.Min(MaximumChatMessages, chatCount);
            _chatSerial = _chatSerial > long.MaxValue - (chatCount - visible) ? long.MaxValue : _chatSerial + chatCount - visible;
            for (long i = 0; i < visible; i++)
            {
                if (_chatSerial == long.MaxValue) break;
                ulong variation = AudienceRandom.Hash(Audience.Seed, (ulong)++_chatSerial);
                var message = new StreamChatMessage(_streamId + ".chat." + _chatSerial, Sender(variation), ChatKey(variation >> 8));
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
            if (Quality == StreamQuality.High && !capabilities.GamingGraphicsAvailable) return "desktop.stream.gpu_required";
            return uploadMbps < MinimumUpload ? "desktop.stream.upload_low" : null;
        }

        private void Finish(bool aborted)
        {
            Audience.Finish();
            StreamSummary summary = _hasBeenLive
                ? new StreamSummary(_streamId, _sequence, DurationSeconds, Audience.PeakViewers, Audience.AverageViewers,
                    Audience.Follows, Audience.Subscriptions, DonationCents, aborted)
                : null;
            // Clear completion eligibility before calling any observer, so reentrant Abort cannot
            // reissue the summary and the root can safely begin another broadcast from Completed.
            _hasBeenLive = false;
            _transitionRemaining = 0;
            State = StreamState.Offline;
            if (summary != null) Completed?.Invoke(summary);
            Changed?.Invoke();
        }

        private AudienceSimulation IdleAudience()
        {
            var audience = new AudienceSimulation(_tuning, 0, 0, false);
            audience.Finish();
            return audience;
        }

        private string SupporterName(int receiptSerial) => Sender(AudienceRandom.Hash(Audience.Seed, ~(ulong)receiptSerial));

        private static string Sender(ulong variation) => (variation % 4) switch
        {
            0 => "PixelFox", 1 => "NightOwl", 2 => "ByteCat", _ => "ArcadeKid"
        };

        private static string ChatKey(ulong variation) => (variation % 4) switch
        {
            0 => "desktop.stream.chat.hello", 1 => "desktop.stream.chat.looks_good",
            2 => "desktop.stream.chat.nice_play", _ => "desktop.stream.chat.keep_going"
        };
    }
}
