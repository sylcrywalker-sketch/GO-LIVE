using System;
using System.Text.RegularExpressions;

namespace GoLive.Viewers
{
    // Deliberately small, anchored RU/EN grammar. False negatives are preferable to inventing commitments.
    // No substring fallback: quoted, speculative, negated and reported clauses never reach the ledger.
    public static class ViewerPromiseVocabulary
    {
        private const string SubjectPattern = "(?<subject>видеокарту|видюху|микрофон|вебкамеру|веб камеру|вебку|gpu|graphics card|microphone|webcam)";
        private static readonly Regex Russian = new("^(?:я )?(?:(?<time>завтра|к завтрашнему дню) )?(?<action>куплю|установлю) (?:новую |новый )?" + SubjectPattern + "(?: (?<time>завтра|к завтрашнему дню))?$", RegexOptions.CultureInvariant);
        private static readonly Regex English = new("^i will (?<action>buy|install) (?:a |the |a new |new )?" + SubjectPattern + " (?<time>tomorrow|by tomorrow)$", RegexOptions.CultureInvariant);

        public static PromiseProposal Parse(string text, double now, double broadcastStarted)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 180 || text.IndexOfAny(new[] { '"', '\'', '«', '»', '“', '”', '‘', '’', '\n', '\r', ';', '?' }) >= 0) return null;
            string normalized = SpeechRelevance.Normalize(text);
            if (normalized == "я начну следующий стрим раньше" || normalized == "следующий стрим начну раньше" ||
                normalized == "i will start the next stream earlier")
                return new PromiseProposal(PromiseAction.StartBroadcast, "stream-start", now, now + .000001, now + 7 * 1440,
                    PromiseCondition.ValueBelow, broadcastStarted % 1440, false);
            Match match = Russian.Match(normalized);
            if (!match.Success) match = English.Match(normalized);
            if (!match.Success || match.Groups["time"].Captures.Count != 1) return null;
            string subject = Subject(match.Groups["subject"].Value);
            if (subject == null) return null;
            string time = match.Groups["time"].Value;
            double tomorrow = (Math.Floor(now / 1440) + 1) * 1440;
            string action = match.Groups["action"].Value;
            return new PromiseProposal(action == "куплю" || action == "buy" ? PromiseAction.Purchase : PromiseAction.Install,
                subject, now, time == "by tomorrow" || time == "к завтрашнему дню" ? now : tomorrow, tomorrow + 1440);
        }

        public static string Subject(StreamEvent fact)
        {
            if (fact == null) return null;
            if (fact.Kind == StreamEventKind.StreamStarted) return "stream-start";
            if (fact.Kind == StreamEventKind.PeripheralChanged)
                return fact.Peripheral == GoLive.PcBuilding.PcPeripheralKind.Microphone ? "microphone" : "webcam";
            return Subject(fact.Speech?.Text);
        }

        public static string Subject(string text)
        {
            string value = SpeechRelevance.Normalize(text ?? "");
            if (Regex.IsMatch(value, @"\b(видеокарт\w*|видюх\w*|gpu|graphics card)\b")) return "gpu";
            if (Regex.IsMatch(value, @"\b(микрофон\w*|microphone)\b")) return "microphone";
            if (Regex.IsMatch(value, @"\b(вебк\w*|веб камер\w*|webcam)\b")) return "webcam";
            if (Regex.IsMatch(value, @"\b(раньше|earlier|следующий стрим|next stream)\b")) return "stream-start";
            return null;
        }

        public static bool MentionsPromise(string text) => Regex.IsMatch(SpeechRelevance.Normalize(text ?? ""),
            @"\b(обещ\w*|promise\w*|сдержал|fulfilled|kept your word|broke your word)\b");

        public static bool References(string text, ViewerPromiseContext context)
        {
            if (context == null || Subject(text) != context.Subject || !(MentionsPromise(text) || ViewerMemoryBank.Historical(text))) return false;
            string value = SpeechRelevance.Normalize(text);
            if (context.Action != PromiseAction.Install && Regex.IsMatch(value, @"\b(установ\w*|install\w*)\b") ||
                context.Action != PromiseAction.Purchase && Regex.IsMatch(value, @"\b(куп\w*|buy|bought)\b")) return false;
            bool failure = Regex.IsMatch(value, @"\b(не купил|не установил|не выполнил|не сдержал|нарушил|broken|broke|did not|didnt|failed)\b");
            bool success = !failure && Regex.IsMatch(value, @"\b(купил|установил|выполнил|сдержал|bought|installed|fulfilled|kept|started earlier|начал раньше)\b");
            if (failure && context.Status != PromiseStatus.Broken || success && context.Status != PromiseStatus.Fulfilled) return false;
            return true;
        }
    }
}
