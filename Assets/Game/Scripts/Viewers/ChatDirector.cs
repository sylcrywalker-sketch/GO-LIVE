using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Desktop;
using GoLive.Voice;

namespace GoLive.Viewers
{
    public enum ChatModelHealth { Disabled, Available, BackingOff }

    // Development measurements of text generation (never saved).
    public sealed class ChatDirectorStats
    {
        private readonly List<double> _latencies = new();

        public int Submitted { get; internal set; }
        public int ShownFromModel { get; internal set; }
        public int ShownFromFallback { get; internal set; }
        public int Rejected { get; internal set; }
        public int TimedOut { get; internal set; }
        public int Unavailable { get; internal set; }
        public int Malformed { get; internal set; }
        public int DroppedStale { get; internal set; }
        public int DroppedQueueFull { get; internal set; }
        public int DroppedViewerLeft { get; internal set; }
        public int Discarded { get; internal set; }
        public int MaximumQueueDepth { get; internal set; }
        public IReadOnlyList<double> Latencies => _latencies;
        public List<string> RejectedSamples { get; } = new();
        // Per broadcast: the warm-up request's latency and the first real generation's (NaN until measured).
        public double WarmupSeconds { get; internal set; } = double.NaN;
        public double FirstSeconds { get; internal set; } = double.NaN;

        internal void AddLatency(double seconds)
        {
            if (_latencies.Count == 2000) _latencies.RemoveAt(0);
            _latencies.Add(seconds);
        }

        internal void AddRejected(string sample)
        {
            if (RejectedSamples.Count == 40) RejectedSamples.RemoveAt(0);
            RejectedSamples.Add(sample);
        }

        public double Percentile(double fraction)
        {
            if (_latencies.Count == 0) return 0;
            var sorted = new List<double>(_latencies);
            sorted.Sort();
            return sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(fraction * (sorted.Count - 1) + .5))];
        }
    }

    // Turns C#-approved reaction intents into chat lines. Generation starts as soon as a reaction is scheduled and
    // the line appears at the reaction's due time. Inference runs off the main thread with bounded concurrency and
    // a bounded queue; the main thread only polls finished tasks. Every model result is validated; failures fall
    // back to compact lines (or silence). Stale reactions are dropped, never shown late. Broadcast end cancels all.
    public sealed class ChatDirector : IDisposable
    {
        private sealed class Job
        {
            public ReactionIntent Intent;
            public Task<LanguageModelResult> Task;
            public CancellationTokenSource Cancellation;
            public bool Resolved;
            public string Text;
            public ReactionSource Source;
            public string Reason;
            public double Latency;
            public int PromptCharacters;
            public ChatSituation Situation;
            public double SubmittedAt;
            public double QueueSeconds;
            public double PublishAfter;
        }

        private readonly IViewerLanguageModel _model;
        private readonly ChatModelSettings _settings;
        private readonly StreamChat _chat;
        private readonly ReactionLog _log;
        private readonly Func<double> _realClock;
        private readonly List<Job> _jobs = new();
        // Removed reactions release their state immediately, but their adapter calls still own workers until done.
        private readonly List<Task<LanguageModelResult>> _retired = new();
        private AudienceRandom _random = new(1);
        private int _failures;
        private double _backoffUntil = double.NegativeInfinity;
        private Task<LanguageModelResult> _warmup;
        private CancellationTokenSource _warmupCancellation;
        private bool _warmupRequested;
        private bool _warmupWanted;

        public ChatDirectorStats Stats { get; } = new();
        // Runtime switch (developer toggle, outage simulation). The authored Enabled flag is the default.
        public bool ModelEnabled { get; set; }
        public int QueueDepth => _jobs.Count;
        public ChatModelHealth Health => !ModelEnabled || _model == null ? ChatModelHealth.Disabled
            : _realClock() < _backoffUntil ? ChatModelHealth.BackingOff : ChatModelHealth.Available;
        public event Action<StreamChatMessage, ReactionIntent> Shown;
        // Owner consumes a callback opportunity only when the chosen memory was actually referenced in a
        // published line. Every drop/failure/cancellation releases the transient reservation.
        public event Action<ReactionIntent, ChatSituation, string> Finished;

        public ChatDirector(IViewerLanguageModel model, ChatModelSettings settings, StreamChat chat, ReactionLog log, Func<double> realClock = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            string error = settings.Validate();
            if (error != null) throw new ArgumentException(error, nameof(settings));
            _model = model;
            _chat = chat ?? throw new ArgumentNullException(nameof(chat));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _realClock = realClock ?? (() => SpeechClock.Now);
            ModelEnabled = settings.Enabled && model != null;
        }

        public void BeginBroadcast(AudienceRandom random)
        {
            CancelAll("broadcast restarted");
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _warmupRequested = false;
            Stats.WarmupSeconds = Stats.FirstSeconds = double.NaN;
        }

        // One tiny request with the real system text: pages the model back into memory and caches the shared prompt
        // prefix before the first viewer needs an answer. Its result is never shown and never counts as a failure.
        public void Warmup()
        {
            if (Health != ChatModelHealth.Available || _warmupRequested) return;
            _warmupWanted = true;
            StartWarmup();
        }

        private void StartWarmup()
        {
            if (!_warmupWanted || _warmup != null || Health != ChatModelHealth.Available || RunningGenerations() >= _settings.MaximumConcurrent) return;
            var request = new ViewerChatRequest(ChatContextBuilder.SystemText, "Warm-up before the stream. Reply {\"text\": \"ok\"}.", 8);
            _warmupCancellation = new CancellationTokenSource();
            _warmupRequested = true;
            _warmupWanted = false;
            _warmup = _model.GenerateAsync(request, _warmupCancellation.Token);
        }

        public void Submit(ReactionIntent intent)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            Stats.Submitted++;
            if (_jobs.Count >= _settings.QueueCapacity && !MakeRoom(intent))
            {
                Stats.DroppedQueueFull++;
                _log.Add(ReactionLog.ForIntent(intent, ReactionOutcome.Dropped, "queue full"));
                Finished?.Invoke(intent, null, null);
                return;
            }
            _jobs.Add(new Job { Intent = intent, SubmittedAt = _realClock() });
            Stats.MaximumQueueDepth = Math.Max(Stats.MaximumQueueDepth, _jobs.Count);
        }

        public void Update(double now, AudienceRoster roster, Func<ChatSituation> situation, string broadcastId)
            => Update(now, roster, _ => situation(), broadcastId);

        public void Update(double now, AudienceRoster roster, Func<ReactionIntent, ChatSituation> situation, string broadcastId)
        {
            DropDeparted(roster);
            Poll();
            StartWarmup();
            StartGenerations(now, situation);
            Post(now, roster, broadcastId);
        }

        public void CancelAll(string reason)
        {
            _warmupWanted = false;
            _warmupCancellation?.Cancel();
            foreach (Job job in _jobs)
            {
                Retire(job);
                _log.Add(ReactionLog.ForIntent(job.Intent, ReactionOutcome.Dropped, reason));
                Finished?.Invoke(job.Intent, job.Situation, null);
            }
            _jobs.Clear();
        }

        public void Dispose()
        {
            CancelAll("disposed");
            _warmupCancellation?.Dispose();
            _warmupCancellation = null;
            _warmup = null;
            _model?.Dispose();
            _retired.Clear();
        }

        private void Poll()
        {
            for (int i = _retired.Count - 1; i >= 0; i--)
                if (_retired[i].IsCompleted)
                {
                    if (_retired[i].IsFaulted) _ = _retired[i].Exception;
                    _retired.RemoveAt(i);
                }
            if (_warmup != null && _warmup.IsCompleted)
            {
                if (!_warmupCancellation.IsCancellationRequested)
                    Stats.WarmupSeconds = _warmup.Status == TaskStatus.RanToCompletion ? _warmup.Result.LatencySeconds : double.NaN;
                _warmup = null;
                _warmupCancellation.Dispose();
                _warmupCancellation = null;
            }
            foreach (Job job in _jobs)
            {
                if (job.Resolved || job.Task == null || !job.Task.IsCompleted) continue;
                LanguageModelResult result = job.Task.Status == TaskStatus.RanToCompletion
                    ? job.Task.Result
                    : new LanguageModelResult(LanguageModelStatus.Unavailable, null, 0, detail: job.Task.Exception?.GetBaseException().Message);
                job.Cancellation?.Dispose();
                job.Cancellation = null;
                job.Latency = result.LatencySeconds;
                Stats.AddLatency(result.LatencySeconds);
                if (double.IsNaN(Stats.FirstSeconds)) Stats.FirstSeconds = result.LatencySeconds;
                switch (result.Status)
                {
                    case LanguageModelStatus.Ok:
                        _failures = 0;
                        ChatValidation validation = ChatOutputValidator.Validate(result.Text, job.Intent, _chat.Messages, job.Situation);
                        if (validation.Accepted) Resolve(job, validation.Text, ReactionSource.LanguageModel, null);
                        else
                        {
                            Stats.Rejected++;
                            Stats.AddRejected(validation.Reason + ": " + result.Text);
                            Fallback(job, "rejected (" + validation.Reason + ")");
                        }
                        break;
                    case LanguageModelStatus.Cancelled:
                        Resolve(job, null, ReactionSource.None, "cancelled");
                        break;
                    default:
                        if (result.Status == LanguageModelStatus.TimedOut) Stats.TimedOut++;
                        else if (result.Status == LanguageModelStatus.Malformed) Stats.Malformed++;
                        else Stats.Unavailable++;
                        if (result.Status != LanguageModelStatus.Malformed && ++_failures >= _settings.FailuresBeforeBackoff)
                            _backoffUntil = _realClock() + _settings.BackoffSeconds;
                        Fallback(job, result.Status.ToString().ToLowerInvariant());
                        break;
                }
            }
        }

        private void StartGenerations(double now, Func<ReactionIntent, ChatSituation> situation)
        {
            int running = RunningGenerations();
            _jobs.Sort((a, b) => a.Intent.DueSeconds.CompareTo(b.Intent.DueSeconds));
            foreach (Job job in _jobs)
            {
                if (job.Resolved || job.Task != null) continue;
                if (now > job.Intent.ExpiresSeconds) continue;
                if (Health != ChatModelHealth.Available)
                {
                    job.Situation = situation(job.Intent);
                    Fallback(job, Health == ChatModelHealth.Disabled ? "model disabled" : "model backing off");
                    continue;
                }
                if (running >= _settings.MaximumConcurrent) continue;
                job.Situation = situation(job.Intent);
                ViewerChatRequest request = ChatContextBuilder.Build(job.Intent, job.Situation, _settings.MaximumTokens);
                job.PromptCharacters = request.Characters;
                job.Cancellation = new CancellationTokenSource();
                job.QueueSeconds = _realClock() - job.SubmittedAt;
                job.Task = _model.GenerateAsync(request, job.Cancellation.Token);
                running++;
            }
        }

        private int RunningGenerations()
        {
            // A cancelled request retains its slot until the adapter actually completes it. This prevents a
            // slow cancellation at a broadcast/load boundary from overlapping a new request on one worker.
            int running = _warmup != null && !_warmup.IsCompleted ? 1 : 0;
            foreach (Task<LanguageModelResult> task in _retired) if (!task.IsCompleted) running++;
            foreach (Job job in _jobs)
                if (!job.Resolved && job.Task != null && !job.Task.IsCompleted) running++;
            return running;
        }

        private void Post(double now, AudienceRoster roster, string broadcastId)
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                Job job = _jobs[i];
                ReactionIntent intent = job.Intent;
                if (now > intent.ExpiresSeconds)
                {
                    job.Cancellation?.Cancel();
                    Stats.DroppedStale++;
                    Remove(i--, job, ReactionOutcome.Dropped, job.Resolved ? "stale" : "stale (still generating)");
                    continue;
                }
                if (!job.Resolved || intent.DueSeconds > now) continue;
                if (intent.ConversationTarget.Kind == ConversationTargetKind.Group)
                {
                    if (now < job.PublishAfter) continue;
                    bool earlierPending = false;
                    foreach (Job sibling in _jobs)
                        if (sibling.Intent.Event.Key == intent.Event.Key && sibling.Intent.Order < intent.Order)
                        { earlierPending = true; break; }
                    if (earlierPending) continue;
                }
                if (job.Text == null)
                {
                    Stats.Discarded++;
                    Remove(i--, job, ReactionOutcome.Discarded, job.Reason);
                    continue;
                }
                if (!SameVisit(roster, intent))
                {
                    Stats.DroppedViewerLeft++;
                    Remove(i--, job, ReactionOutcome.Dropped, "viewer left");
                    continue;
                }
                // Other jobs may have published since generation finished. Recheck at the last possible moment.
                ChatValidation current = ChatOutputValidator.Validate(job.Text, intent, _chat.Messages, job.Situation);
                if (!current.Accepted)
                {
                    if (job.Source == ReactionSource.LanguageModel)
                    {
                        Stats.Rejected++;
                        Stats.AddRejected(current.Reason + ": " + job.Text);
                    }
                    Fallback(job, "publication rejected (" + current.Reason + ")");
                    if (job.Text == null || !ChatOutputValidator.Validate(job.Text, intent, _chat.Messages, job.Situation).Accepted)
                    {
                        Stats.Discarded++;
                        Remove(i--, job, ReactionOutcome.Discarded, job.Reason);
                        continue;
                    }
                }
                long donation = intent.Event.Kind == StreamEventKind.Donation && intent.Direct && intent.Event.SubjectViewerId == intent.Viewer.ViewerId
                    ? intent.Event.AmountCents : 0;
                StreamChatMessage message = _chat.Add(broadcastId, intent.Viewer.ViewerId, intent.Viewer.DisplayName, job.Text, now, intent.Id, job.Source, donation);
                if (job.Source == ReactionSource.LanguageModel) Stats.ShownFromModel++;
                else Stats.ShownFromFallback++;
                if (intent.ConversationTarget.Kind == ConversationTargetKind.Group)
                    foreach (Job sibling in _jobs)
                        if (sibling.Intent.Event.Key == intent.Event.Key && sibling.Intent.Order > intent.Order)
                            sibling.PublishAfter = now + 1.4;
                Remove(i--, job, ReactionOutcome.Shown, job.Reason);
                Shown?.Invoke(message, intent);
            }
        }

        private void Fallback(Job job, string reason)
        {
            string text = FallbackChat.Pick(job.Intent, _random, _chat.Messages);
            Resolve(job, text, text == null ? ReactionSource.None : ReactionSource.Fallback, reason);
        }

        private static bool SameVisit(AudienceRoster roster, ReactionIntent intent) =>
            roster.IsWatching(intent.Viewer.ViewerId) && roster.Epoch(intent.Viewer.ViewerId) == intent.PresenceEpoch &&
            (intent.Event.Kind != StreamEventKind.ViewerReply ||
                roster.IsWatching(intent.Event.SubjectViewerId) && intent.Event.WitnessedBy(intent.Event.SubjectViewerId, roster.Epoch(intent.Event.SubjectViewerId)) &&
                intent.Event.WitnessedBy(intent.Viewer.ViewerId, intent.PresenceEpoch));

        private void DropDeparted(AudienceRoster roster)
        {
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                Job job = _jobs[i];
                if (SameVisit(roster, job.Intent)) continue;
                job.Cancellation?.Cancel();
                Stats.DroppedViewerLeft++;
                Remove(i, job, ReactionOutcome.Dropped, "viewer left or visit changed");
            }
        }

        private static void Resolve(Job job, string text, ReactionSource source, string reason)
        {
            job.Resolved = true;
            job.Text = text;
            job.Source = source;
            job.Reason = reason;
        }

        private ReactionLogEntry Remove(int index, Job job, ReactionOutcome outcome, string reason)
        {
            _jobs.RemoveAt(index);
            Retire(job);
            Finished?.Invoke(job.Intent, job.Situation, outcome == ReactionOutcome.Shown ? job.Text : null);
            ReactionLogEntry entry = ReactionLog.ForIntent(job.Intent, outcome, reason);
            entry.Source = job.Source;
            entry.LatencySeconds = job.Latency;
            entry.QueueSeconds = job.QueueSeconds;
            entry.PromptCharacters = job.PromptCharacters;
            entry.Text = outcome == ReactionOutcome.Shown ? job.Text : null;
            ReactionLog.Context(entry, job.Situation);
            return _log.Add(entry);
        }

        private void Retire(Job job)
        {
            job.Cancellation?.Cancel();
            job.Cancellation?.Dispose();
            job.Cancellation = null;
            if (job.Task != null && !job.Task.IsCompleted) _retired.Add(job.Task);
        }

        // A full queue gives way to a more important reaction: the least important waiting (not generating) one leaves.
        private bool MakeRoom(ReactionIntent incoming)
        {
            int victim = -1;
            for (int i = 0; i < _jobs.Count; i++)
            {
                Job job = _jobs[i];
                if (job.Task != null || job.Resolved) continue;
                if (victim < 0 || Importance(job.Intent) < Importance(_jobs[victim].Intent)) victim = i;
            }
            if (victim < 0 || Importance(_jobs[victim].Intent) >= Importance(incoming)) return false;
            Job dropped = _jobs[victim];
            Stats.DroppedQueueFull++;
            Remove(victim, dropped, ReactionOutcome.Dropped, "queue full");
            return true;
        }

        private static float Importance(ReactionIntent intent) => (intent.Direct ? 2f : 0f) + intent.Event.Significance - intent.Order * .2f;
    }
}
