using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GoLive.Viewers
{
    // Local OpenAI-compatible chat completion server (LM Studio, llama.cpp server). Offline, no key. The model must
    // answer {"text": "..."}: the schema has no field for money, viewers, relationships, memory or commands.
    public sealed class OpenAiCompatibleChatModel : IViewerLanguageModel
    {
        private const string Schema =
            "{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"viewer_message\",\"strict\":true,\"schema\":{\"type\":\"object\"," +
            "\"properties\":{\"text\":{\"type\":\"string\",\"maxLength\":220}},\"required\":[\"text\"],\"additionalProperties\":false}}}";
        private readonly ChatModelSettings _settings;
        private readonly HttpClient _http;

        public OpenAiCompatibleChatModel(ChatModelSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            string error = settings.Validate();
            if (error != null) throw new ArgumentException(error, nameof(settings));
            _http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };
        }

        public async Task<LanguageModelResult> GenerateAsync(ViewerChatRequest request, CancellationToken cancellation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var stopwatch = Stopwatch.StartNew();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
            string body = Body(request);
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await _http.PostAsync(_settings.Endpoint, content, timeout.Token).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                double latency = stopwatch.Elapsed.TotalSeconds;
                if (!response.IsSuccessStatusCode)
                    return new LanguageModelResult(LanguageModelStatus.Unavailable, null, latency, detail: "HTTP " + (int)response.StatusCode);
                return Parse(json, latency);
            }
            catch (OperationCanceledException)
            {
                return new LanguageModelResult(cancellation.IsCancellationRequested ? LanguageModelStatus.Cancelled : LanguageModelStatus.TimedOut,
                    null, stopwatch.Elapsed.TotalSeconds);
            }
            catch (HttpRequestException exception)
            {
                return new LanguageModelResult(LanguageModelStatus.Unavailable, null, stopwatch.Elapsed.TotalSeconds, detail: exception.Message);
            }
            catch (Exception exception) when (exception is ObjectDisposedException || exception is InvalidOperationException)
            {
                return new LanguageModelResult(LanguageModelStatus.Unavailable, null, stopwatch.Elapsed.TotalSeconds, detail: exception.Message);
            }
        }

        public void Dispose() => _http.Dispose();

        private string Body(ViewerChatRequest request)
        {
            var builder = new StringBuilder(request.Characters + 512);
            builder.Append("{\"model\":").Append(Quote(_settings.Model));
            builder.Append(",\"messages\":[{\"role\":\"system\",\"content\":").Append(Quote(request.System));
            builder.Append("},{\"role\":\"user\",\"content\":").Append(Quote(request.User)).Append("}]");
            builder.Append(",\"temperature\":").Append(_settings.Temperature.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"top_p\":").Append(_settings.TopP.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"max_tokens\":").Append(Math.Min(request.MaximumTokens, _settings.MaximumTokens).ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"stream\":false,\"response_format\":").Append(Schema).Append('}');
            return builder.ToString();
        }

        // Malformed server or model output is a result, never an exception.
        public static LanguageModelResult Parse(string json, double latency)
        {
            try
            {
                Completion completion = JsonUtility.FromJson<Completion>(json);
                if (completion?.choices == null || completion.choices.Length == 0 || completion.choices[0].message == null)
                    return new LanguageModelResult(LanguageModelStatus.Malformed, null, latency, detail: "no choices");
                string content = completion.choices[0].message.content?.Trim();
                if (string.IsNullOrEmpty(content) || content[0] != '{')
                    return new LanguageModelResult(LanguageModelStatus.Malformed, null, latency, detail: "content is not JSON");
                Payload payload = JsonUtility.FromJson<Payload>(content);
                if (payload?.text == null) return new LanguageModelResult(LanguageModelStatus.Malformed, null, latency, detail: "no text");
                return new LanguageModelResult(LanguageModelStatus.Ok, payload.text, latency, completion.usage?.prompt_tokens ?? 0,
                    completion.usage?.completion_tokens ?? 0);
            }
            catch (ArgumentException exception)
            {
                return new LanguageModelResult(LanguageModelStatus.Malformed, null, latency, detail: exception.Message);
            }
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder(value.Length + 16).Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < 0x20) builder.Append("\\u").Append(((int)c).ToString("x4"));
                        else builder.Append(c);
                        break;
                }
            }
            return builder.Append('"').ToString();
        }

#pragma warning disable 0649 // Filled by JsonUtility.
        [Serializable] private sealed class Completion { public Choice[] choices; public Usage usage; }
        [Serializable] private sealed class Choice { public Message message; }
        [Serializable] private sealed class Message { public string content; }
        [Serializable] private sealed class Usage { public int prompt_tokens; public int completion_tokens; }
        [Serializable] private sealed class Payload { public string text; }
#pragma warning restore 0649
    }
}
