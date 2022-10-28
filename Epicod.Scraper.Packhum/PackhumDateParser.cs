using Fusi.Antiquity.Chronology;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Epicod.Scraper.Packhum
{
    /// <summary>
    /// Packard Humanities date parser.
    /// </summary>
    public sealed class PackhumDateParser
    {
        private readonly Regex _wsRegex;
        private readonly Regex _orModifierRegex;
        private readonly Regex _earlyModifierRegex;
        private readonly Regex _dateSuffixRegex;
        private readonly Regex _wRegex;
        private readonly string[] _dateSeps;

        private readonly Regex _apSuffixRegex;
        private readonly Regex _caRegex;
        private readonly Regex _splitQmkRegex;
        private readonly Regex _splitSlashRegex;
        private readonly Regex _splitPtRegex;

        // state
        private bool _globalCa;
        private bool _globalDub;
        private readonly List<string> _hints;

        public PackhumDateParser()
        {
            // preprocessing for split
            _wsRegex = new Regex(@"\s+", RegexOptions.Compiled);

            _orModifierRegex = new Regex(
                @"(?:or |od\.|oder )" +
                @"(?<l>shortly |slightly |sh\.)? ?" +
                @"(?<m>later|lat\.|after|aft.|earlier|früher|später)\??",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            _earlyModifierRegex = new Regex(", early$", RegexOptions.Compiled);

            _dateSuffixRegex = new Regex(@",\s*(?<d>[0-3]?[0-9])?\s*" +
                @"(?<m>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[^\s]*",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            _wRegex = new Regex(@"\bw//?", RegexOptions.Compiled);

            _dateSeps = new[] { " and ", " or ", " od.", " oder ", " & ", "," };

            // date preprocessing
            _apSuffixRegex = new Regex(@"([0-9])(a\.|p\.)", RegexOptions.Compiled);
            _caRegex = new Regex(@"^ca?\.", RegexOptions.Compiled);
            _splitQmkRegex = new Regex(@"([0-9]\?)\s+([0-9])", RegexOptions.Compiled);
            _splitSlashRegex = new Regex("([^0-9IVX])/([^0-9IVX])",
                RegexOptions.Compiled);

            // datation
            _splitPtRegex = new Regex("([0-9IVXtdh?])-([0-9IVX])",
                RegexOptions.Compiled);

            _hints = new List<string>();
        }

        private static int GetMonthNumber(string month)
        {
            return month[..3].ToLowerInvariant() switch
            {
                "jan" => 1,
                "feb" => 2,
                "mar" => 3,
                "apr" => 4,
                "may" => 5,
                "jun" => 6,
                "jul" => 7,
                "aug" => 8,
                "sep" => 9,
                "oct" => 10,
                "nov" => 11,
                "dec" => 12,
                _ => 0,
            };
        }

        private string NormalizeWS(string s) => _wsRegex.Replace(s, " ").Trim();

        private string ExtractHints(string text)
        {
            char[] clChars = new[] { ']', ')' };

            // find 1st ] or )
            int i = text.LastIndexOfAny(clChars);
            if (i == -1) return text;

            StringBuilder sb = new(text);
            while (i > -1)
            {
                // find next [ or (
                int end = i;
                char c = text[i] == ']' ? '[' : '(';
                i = text.LastIndexOf(c, i - 1);
                if (i == -1) i = 0;

                // extract and remove hint
                _hints.Add(text[i..(end + 1)]);
                sb.Remove(i, end + 1 - i);

                // find next ] or )
                i = text.LastIndexOfAny(clChars, i - 1);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Preprocesses date text for split (see A1 in docs).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <exception cref="ArgumentNullException">text</exception>
        public string PreprocessForSplit(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            // normalize WS
            string s = NormalizeWS(text);

            // extract [...] and (...)
            s = ExtractHints(s);

            // corner cases
            s = _wRegex.Replace(s, "w");
            s = s.Replace("July/August", "July");

            // or...earlier/later: wrap in () normalizing expression
            s = _orModifierRegex.Replace(s, (Match m) =>
            {
                return "(or " +
                       (m.Groups["l"].Length > 0 ? "shortly " : "") +
                       (m.Groups["m"].Value[0] == 'f' ||
                        m.Groups["m"].Value[0] == 'e' ? "earlier" : "later")
                        + ")";
            });

            // at the earliest => wrap in ()
            s = s.Replace("at the earliest", "(at the earliest)");

            // , early... : wrap in ()
            s = _earlyModifierRegex.Replace(s, " (early)");

            // date suffix: wrap in {d=N,m=N} ({} are never used in PHI dates)
            s = _dateSuffixRegex.Replace(s, (Match m) =>
            {
                int day = m.Groups["d"].Length > 0
                    ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
                    : 0;
                int month = GetMonthNumber(m.Groups[2].Value);
                return "{" + (day > 0 ? $"d={day},m={month}" : $"m={month}") + "}";
            });

            // re-extract hints eventually injected by preprocessing,
            // and re-normalize whitespace
            return NormalizeWS(ExtractHints(s));
        }

        private static IList<string> SplitAtRegexWithSep(string text, Regex r)
        {
            if (!r.IsMatch(text)) return Array.Empty<string>();

            string lines = r.Replace(text, (Match m) =>
                $"{m.Groups[1].Value}\n{m.Groups[2].Value}");
            return lines.Split('\n');
        }

        /// <summary>
        /// Splits the dates in the specified date(s) text (see A2 in docs).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>Date(s) text(s).</returns>
        /// <exception cref="ArgumentNullException">text</exception>
        public IList<string> SplitDates(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            // corner case: split at question mark
            if (_splitQmkRegex.IsMatch(text))
                return SplitAtRegexWithSep(text, _splitQmkRegex);

            // corner case: split at slash
            if (_splitSlashRegex.IsMatch(text))
                return SplitAtRegexWithSep(text, _splitSlashRegex);

            // normal split at conjunctions or comma
            return (from split in
                text.Split(_dateSeps, StringSplitOptions.RemoveEmptyEntries)
                select NormalizeWS(split))
                .ToList();
        }

        /// <summary>
        /// Splits the datations in the specified single date text (see B in docs).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>Datation(s) text(s).</returns>
        /// <exception cref="ArgumentNullException">text</exception>
        public IList<string> SplitDatations(string text)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));

            return SplitAtRegexWithSep(text, _splitPtRegex);
        }

        private string PreprocessDatation(string text)
        {
            // detach a./p. when attached to N
            string s = _apSuffixRegex.Replace(text, "$1 $2");

            // remove initial ca. (this must be applied to any datations)
            Match m = _caRegex.Match(s);
            if (m.Success)
            {
                _globalCa = true;
                s = s.Remove(0, m.Length);
            }

            // remove final ?
            if (s.Length > 0 && s[^1] == '?')
            {
                _globalDub = true;
                s = s[..^1];
            }

            return s;
        }

        private void Reset()
        {
            _globalCa = false;
            _globalDub = false;
            _hints.Clear();
        }

        public IList<HistoricalDate> Parse(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<HistoricalDate>();

            Reset();
            string s = PreprocessForSplit(text);
            foreach (string singleDate in SplitDates(s))
            {
                // TODO
            }
            throw new NotImplementedException();
        }
    }
}
