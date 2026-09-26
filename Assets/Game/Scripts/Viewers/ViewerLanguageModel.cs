using System;
using System.Threading;
using System.Threading.Tasks;

namespace GoLive.Viewers
{
    // The only boundary to text generation. An implementation owns the backend call, cancellation, timeout and
    // malformed-response handling; it never owns viewer state, memory, relationships or gameplay outcomes.
    public interface IViewerLanguageModel : IDisposable
    {
        // Never throws for backend problems: failures come back as a result status.
        Task<LanguageModelResult> GenerateAsync(ViewerChatRequest request, CancellationToken cancellation);
    }

    // Instructions and data kept apart: the system text is ours; everything quoted in the user text (speech,
    // chat, names, memories) is content to react to, never instructions.
    public sealed class ViewerChatRequest
    {
        public string System { get; }
        public string User { get; }
        public int MaximumTokens { get; }

        public ViewerChatRequest(string system, string user, int maximumTokens)
        {
            if (string.IsNullOrWhiteSpace(system)) throw new ArgumentException("A request needs instructions.", nameof(system));
            if (string.IsNullOrWhiteSpace(user)) throw new ArgumentException("A request needs context.", nameof(user));
            if (maximumTokens <= 0) throw new ArgumentOutOfRangeException(nameof(maximumTokens));
            System = system;
            User = user;
            MaximumTokens = maximumTokens;
        }

        public int Characters => System.Length + User.Length;
        // Rough size for diagnostics (about four characters per token for mixed RU/EN).
        public int ApproximateTokens => Characters / 4;
    }

    public enum LanguageModelStatus { Ok, Unavailable, TimedOut, Malformed, Cancelled }

    public sealed class LanguageModelResult
    {
        public LanguageModelStatus Status { get; }
        // The viewer message the model produced (its whole output contract); null unless Ok.
        public string Text { get; }
        public double LatencySeconds { get; }
        public int PromptTokens { get; }
        public int CompletionTokens { get; }
        public string Detail { get; }

        public LanguageModelResult(LanguageModelStatus status, string text, double latencySeconds, int promptTokens = 0, int completionTokens = 0,
            string detail = null)
        {
            Status = status;
            Text = status == LanguageModelStatus.Ok ? text : null;
            LatencySeconds = latencySeconds;
            PromptTokens = promptTokens;
            CompletionTokens = completionTokens;
            Detail = detail;
        }
    }

    // Local inference settings, authored in the ViewerCoreConfig asset.
    [Serializable]
    public sealed class ChatModelSettings
    {
        public bool Enabled = true;
        // Any OpenAI-compatible local server; LM Studio's is http://127.0.0.1:1234.
        public string Endpoint = "http://127.0.0.1:1234/v1/chat/completions";
        public string Model = "mistralai/ministral-3-8b-instruct-2512";
        public float TimeoutSeconds = 6f;
        public int MaximumTokens = 64;
        public float Temperature = .85f;
        public float TopP = .95f;
        // Generations in flight at once and reactions waiting for one; beyond that reactions are dropped.
        public int MaximumConcurrent = 1;
        public int QueueCapacity = 6;
        // After consecutive failures the backend is left alone this long (fallback chat meanwhile).
        public int FailuresBeforeBackoff = 2;
        public float BackoffSeconds = 20f;

        public string Validate()
        {
            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return "Chat model endpoint must be an absolute http(s) URL.";
            if (!uri.IsLoopback) return "Chat model endpoint must be local (loopback).";
            if (string.IsNullOrWhiteSpace(Model)) return "Chat model name is missing.";
            if (!(TimeoutSeconds > 0 && TimeoutSeconds <= 60)) return "Chat model timeout must be 0-60 s.";
            if (MaximumTokens < 8 || MaximumTokens > 256) return "Chat model token limit must be 8-256.";
            if (!(Temperature >= 0 && Temperature <= 2) || !(TopP > 0 && TopP <= 1)) return "Chat model sampling values are invalid.";
            if (MaximumConcurrent < 1 || MaximumConcurrent > 4 || QueueCapacity < 1 || QueueCapacity > 32) return "Chat model queue limits are invalid.";
            if (FailuresBeforeBackoff < 1 || !(BackoffSeconds > 0)) return "Chat model backoff values are invalid.";
            return null;
        }
    }
}
