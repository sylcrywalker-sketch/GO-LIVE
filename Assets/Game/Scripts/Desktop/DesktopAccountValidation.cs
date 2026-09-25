using System;
using System.Collections.Generic;

namespace GoLive.Desktop
{
    // One stateless set of input rules for commands and preflight; no account state lives here.
    internal static class DesktopAccountValidation
    {
        internal static bool Id(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) return false;
            foreach (char c in value)
                if (!AsciiLetterOrDigit(c) && c != '-' && c != '_' && c != '.' && c != ':') return false;
            return true;
        }

        internal static bool Text(string value, int minimum, int maximum, bool multiline = false)
        {
            if (value == null || value.Length < minimum || value.Length > maximum || value != value.Trim()) return false;
            foreach (char c in value)
                if (c == '<' || c == '>' || (char.IsControl(c) && !(multiline && c == '\n'))) return false;
            return true;
        }

        internal static bool Username(string value)
        {
            if (value == null || value.Length < 3 || value.Length > 24 || !AsciiLetterOrDigit(value[0]) || !AsciiLetterOrDigit(value[value.Length - 1])) return false;
            foreach (char c in value)
                if (!AsciiLetterOrDigit(c) && c != '.' && c != '-' && c != '_') return false;
            return true;
        }

        internal static bool Address(string value)
        {
            const string suffix = "@outline.local";
            return value != null && value.EndsWith(suffix, StringComparison.Ordinal) &&
                value == value.ToLowerInvariant() && Username(value.Substring(0, value.Length - suffix.Length));
        }

        internal static bool Code(string value)
        {
            if (value == null || value.Length != 32) return false;
            foreach (char c in value)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }

        internal static bool Ledger(string[] ids, int maximum)
        {
            if (ids == null || ids.Length > maximum) return false;
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
                if (!Id(id) || !distinct.Add(id)) return false;
            return true;
        }

        internal static bool FiniteNonnegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        private static bool AsciiLetterOrDigit(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }
}
