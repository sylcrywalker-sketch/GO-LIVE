using System;
using System.Collections.Generic;
using GoLive.Desktop;
using GoLive.PcBuilding;

namespace GoLive.Viewers
{
    // Unity-side facts the plain domain cannot observe itself, sampled every frame by the runtime bridge.
    public readonly struct StreamerContext
    {
        // The player is seated and focused on the desktop (the broadcast shows live desktop frames).
        public bool AtDesk { get; }
        // The real microphone is recognizing speech; silence can only be measured while it is.
        public bool VoiceListening { get; }
        public double GameMinutes { get; }
        public StreamTopic Content { get; }

        public StreamerContext(bool atDesk, bool voiceListening, double gameMinutes = 0, StreamTopic content = StreamTopic.Community)
        {
            AtDesk = atDesk;
            VoiceListening = voiceListening;
            GameMinutes = gameMinutes;
            Content = content;
        }
    }

    // Normalizes the current broadcast's facts into stream events: live start, recognized speech, accepted
    // donations, the simulation's follows/subscriptions/chat impulses, audience milestones, peripheral changes
    // and silence/away periods. Every event has an idempotency key; a fact seen twice is normalized once.
    public sealed class StreamEventSource : IDisposable
    {
        private const int MaximumPerKindPerTick = 3;
        private const double ThanksWindowSeconds = 60;
        private const double MilestoneQuietSeconds = 45;
        private static readonly int[] Milestones = { 3, 5, 10, 15, 25, 50, 75, 100, 150, 200, 300, 500, 1000, 2000 };

        private readonly StreamSession _stream;
        private readonly StreamSpeechFeed _speech;
        private readonly DonationAccount _donations;
        private readonly PcPeripherals _peripherals;
        private readonly TrichChannel _channel;
        private readonly AudienceRoster _roster;
        private readonly ReactionTuning _tuning;
        private readonly Func<IReadOnlyList<ViewerNameForms>> _knownNames;
        private readonly List<ViewerNameForms> _mentionNames = new();
        private readonly HashSet<string> _mentionIds = new(StringComparer.Ordinal);
        private readonly List<StreamEvent> _pending = new();
        private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
        private bool _live;
        private string _broadcast = "";
        private long _serial;
        private int _follows, _subscriptions;
        private long _chatter;
        private int _milestone;
        private int _channelPeak;
        private bool _hadMicrophone, _hadWebcam;
        private double _quietSince;
        private int _silenceStep;
        private double _awaySince = -1;
        private bool _awayReported;
        private StreamEvent _lastDonation;
        private int _peripheralSerial;

        public StreamerActivity Activity { get; private set; }

        public StreamEventSource(StreamSession stream, StreamSpeechFeed speech, DonationAccount donations, PcPeripherals peripherals,
            TrichChannel channel, AudienceRoster roster, ReactionTuning tuning, Func<IReadOnlyList<ViewerNameForms>> knownNames = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _speech = speech ?? throw new ArgumentNullException(nameof(speech));
            _donations = donations ?? throw new ArgumentNullException(nameof(donations));
            _peripherals = peripherals ?? throw new ArgumentNullException(nameof(peripherals));
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _roster = roster ?? throw new ArgumentNullException(nameof(roster));
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _knownNames = knownNames;
            _stream.Changed += ObserveStream;
            _speech.SpeechAdded += OnSpeech;
            _donations.Received += OnDonation;
            _peripherals.Changed += OnPeripherals;
            ObserveStream();
        }

        public bool IsLive => _live;
        public string BroadcastId => _broadcast;
        public double Now => _stream.DurationSeconds;

        public void NotifyJoined(ChatParticipant participant)
        {
            if (!_live || participant == null || !_roster.IsWatching(participant.ViewerId)) return;
            Add(StreamEvent.ViewerJoined(++_serial, _broadcast + ".visit." + _roster.Epoch(participant.ViewerId),
                _roster.JoinedAt(participant.ViewerId), participant.ViewerId, participant.DisplayName, .3f));
        }

        // Moves the events normalized since the last call into the given list, in order.
        public void Drain(List<StreamEvent> into)
        {
            into.AddRange(_pending);
            _pending.Clear();
        }

        // Called once per frame after the stream advanced.
        public void Tick(StreamerContext context)
        {
            ObserveStream();
            if (!_live) return;
            double now = Now;
            AudienceSimulation audience = _stream.Audience;
            _roster.SetAudienceSize(audience.CurrentViewers);

            for (int emitted = 0; _follows < audience.Follows; _follows++)
                if (emitted++ < MaximumPerKindPerTick)
                    Add(StreamEvent.Follow(++_serial, _broadcast + ".follow." + (_follows + 1), now, null, null));
            for (int emitted = 0; _subscriptions < audience.Subscriptions; _subscriptions++)
                if (emitted++ < MaximumPerKindPerTick)
                    Add(StreamEvent.Subscription(++_serial, _broadcast + ".subscription." + (_subscriptions + 1), now, null, null));
            // Chat impulses beyond a couple per frame would all fall to the budget anyway.
            long impulses = audience.ChatMessages - _chatter;
            _chatter = audience.ChatMessages;
            for (long i = Math.Max(0, impulses - 2); i < impulses; i++)
                Add(StreamEvent.AudienceChatter(++_serial, _broadcast + ".chatter." + (_chatter - impulses + i + 1), now, audience.CurrentViewers));

            // Only the highest size reached is news, and the audience the broadcast starts with is not.
            int reached = 0;
            while (_milestone < Milestones.Length && audience.CurrentViewers >= Milestones[_milestone]) reached = Milestones[_milestone++];
            if (reached > 0 && now >= MilestoneQuietSeconds)
                Add(StreamEvent.AudienceMilestone(++_serial, _broadcast, now, reached, reached > _channelPeak));

            TrackSilence(context, now);
            TrackAway(context, now);
            Activity = !context.AtDesk && _awaySince >= 0 && now - _awaySince >= _tuning.AwaySeconds ? StreamerActivity.Away
                : _silenceStep >= 2 ? StreamerActivity.VeryQuiet
                : _silenceStep == 1 ? StreamerActivity.Quiet
                : StreamerActivity.Active;
        }

        public void Dispose()
        {
            _stream.Changed -= ObserveStream;
            _speech.SpeechAdded -= OnSpeech;
            _donations.Received -= OnDonation;
            _peripherals.Changed -= OnPeripherals;
        }

        private void ObserveStream()
        {
            bool live = _stream.State == StreamState.Live;
            if (live == _live) return;
            _live = live;
            _pending.Clear();
            _keys.Clear();
            _lastDonation = null;
            Activity = StreamerActivity.Active;
            if (!live) return;
            _broadcast = _stream.BroadcastId;
            _serial = 0;
            _peripheralSerial = 0;
            AudienceSimulation audience = _stream.Audience;
            _follows = audience.Follows;
            _subscriptions = audience.Subscriptions;
            _chatter = audience.ChatMessages;
            _milestone = 0;
            _channelPeak = _channel.PeakViewers;
            _hadMicrophone = _peripherals.HasMicrophone;
            _hadWebcam = _peripherals.HasWebcam;
            _quietSince = Now;
            _silenceStep = 0;
            _awaySince = -1;
            _awayReported = false;
            _roster.SetAudienceSize(audience.CurrentViewers);
            Add(StreamEvent.Started(++_serial, _broadcast, Now));
        }

        private void OnSpeech(StreamSpeechEvent entry)
        {
            if (!_live) return;
            SpeechAnalysis analysis = SpeechRelevance.Analyze(entry.Speech, NamesForSpeech());
            _quietSince = entry.StreamSeconds;
            _silenceStep = 0;
            StreamEvent speech = StreamEvent.StreamerSpeech(++_serial, _broadcast, entry.StreamSeconds, analysis);
            // "Thanks" right after a donation is addressed to that donor.
            if (analysis.Has(SpeechCue.Thanks) && analysis.MentionedViewerIds.Count == 0 && _lastDonation != null && entry.StreamSeconds - _lastDonation.StreamSeconds <= ThanksWindowSeconds
                && _lastDonation.SubjectViewerId != null)
                speech = speech.WithSubject(_lastDonation.SubjectViewerId, _lastDonation.SubjectName);
            Add(speech);
        }

        private IReadOnlyList<ViewerNameForms> NamesForSpeech()
        {
            _mentionNames.Clear();
            _mentionIds.Clear();
            IReadOnlyList<ViewerNameForms> known = _knownNames?.Invoke();
            if (known != null)
                foreach (ViewerNameForms name in known)
                    if (_mentionIds.Add(name.ViewerId)) _mentionNames.Add(name);
            foreach (ChatParticipant viewer in _roster.Named)
                if (_mentionIds.Add(viewer.ViewerId)) _mentionNames.Add(new ViewerNameForms(viewer.ViewerId, viewer.NameForms));
            foreach (ChatParticipant viewer in _roster.Ephemeral)
                if (_mentionIds.Add(viewer.ViewerId)) _mentionNames.Add(new ViewerNameForms(viewer.ViewerId, viewer.NameForms));
            return _mentionNames;
        }

        private void OnDonation(DonationReceipt receipt)
        {
            if (!_live || !receipt.Id.StartsWith(_broadcast + ".", StringComparison.Ordinal)) return;
            ChatParticipant donor = _roster.FindByName(receipt.SenderName);
            StreamEvent donation = StreamEvent.Donation(++_serial, receipt.Id, Now, donor?.ViewerId, receipt.SenderName, receipt.AmountCents);
            if (Add(donation)) _lastDonation = donation;
        }

        private void OnPeripherals()
        {
            if (!_live) return;
            if (_peripherals.HasMicrophone != _hadMicrophone)
            {
                _hadMicrophone = _peripherals.HasMicrophone;
                Add(StreamEvent.PeripheralChanged(++_serial, _broadcast + ".peripheral." + ++_peripheralSerial, Now,
                    PcPeripheralKind.Microphone, _hadMicrophone));
            }
            if (_peripherals.HasWebcam != _hadWebcam)
            {
                _hadWebcam = _peripherals.HasWebcam;
                Add(StreamEvent.PeripheralChanged(++_serial, _broadcast + ".peripheral." + ++_peripheralSerial, Now,
                    PcPeripheralKind.Webcam, _hadWebcam));
            }
        }

        // Silence is only a fact while the real microphone listens; otherwise nothing can tell it from speech.
        private void TrackSilence(StreamerContext context, double now)
        {
            if (!context.VoiceListening || !context.AtDesk)
            {
                _quietSince = now;
                _silenceStep = 0;
                return;
            }
            double quiet = now - _quietSince;
            if (_silenceStep == 0 && quiet >= _tuning.LongSilenceSeconds)
            {
                _silenceStep = 1;
                Add(StreamEvent.StreamerSilence(++_serial, $"{_broadcast}.silence.{_quietSince:0}.1", now, quiet, SilenceLevel.Long));
            }
            double veryLongAt = _tuning.VeryLongSilenceSeconds + (_silenceStep - 1) * (double)_tuning.SilenceRepeatSeconds;
            if (_silenceStep >= 1 && quiet >= veryLongAt)
            {
                _silenceStep++;
                Add(StreamEvent.StreamerSilence(++_serial, $"{_broadcast}.silence.{_quietSince:0}.{_silenceStep}", now, quiet, SilenceLevel.VeryLong));
            }
        }

        private void TrackAway(StreamerContext context, double now)
        {
            if (context.AtDesk)
            {
                _awaySince = -1;
                _awayReported = false;
                return;
            }
            if (_awaySince < 0) _awaySince = now;
            if (!_awayReported && now - _awaySince >= _tuning.AwaySeconds)
            {
                _awayReported = true;
                Add(StreamEvent.StreamerAway(++_serial, $"{_broadcast}.away.{_awaySince:0}", now, now - _awaySince));
            }
        }

        private bool Add(StreamEvent streamEvent)
        {
            if (!_keys.Add(streamEvent.Key)) return false;
            _pending.Add(streamEvent);
            return true;
        }
    }
}
