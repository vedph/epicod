using Epicod.Core;
using Fusi.Antiquity.Chronology;
using Fusi.Tools;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Epicod.Scraper.Packhum
{
    /// <summary>
    /// A simple parser for a Packhum text note.
    /// </summary>
    public sealed class PackhumParser
    {
        private readonly char[] _seps;
        private readonly Regex _typeRegex;
        private readonly Regex _writingRegex;
        private readonly Regex _cornerDateRegex;
        private readonly Regex _dateRegex;
        private readonly Regex _refAbbrRegex;
        private Dictionary<string, HistoricalDate>? _nanDates;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackhumParser"/> class.
        /// </summary>
        public PackhumParser()
        {
            _seps = new[] { '\u2014' };

            // type like [pottery]
            _typeRegex = new Regex(@"^\s*\[([^]]+)\]\s*$");

            // type as writing direction/layout
            _writingRegex = new Regex(
                @"^\s*(?:stoich|non-stoich|boustr|retrogr)\.\s*\d*\s*$");

            _cornerDateRegex = new Regex(@"^\s*(early|late|aet\.)");

            // datation:
            // m: c., ante, post
            // cm: init., med., fin. (century modifier)
            // century: s.
            // nv: numeric value
            // ord: ordinal, suffixed to nv for centuries
            // cv: century value (Roman)
            // dubious: ?
            // era: a., p., ac, pc, bc, ad
            _dateRegex = new Regex(
                @"^\s*(?<m>c\.|ante|post)?\s*" +
                @"(?<cm>init\.|med\.|fin\.)?\s*" +
                @"(?:(?:(?<nv>\d+)(?<ord>st|nd|rd|th)?)|(?:s\.\s*(?<cv>[IVX]+)))\s*" +
                @"(?<dubious>\?)?\s*" +
                @"(?<era>a\.|p\.|ac|pc|bc|ad)?", RegexOptions.IgnoreCase);

            // 2 initial capitals are usually a hint for SEG, IG, etc.
            _refAbbrRegex = new Regex(@"^\s*[A-Z]{2,}");
        }

        private bool ParseType(string text, int nodeId,
            List<TextNodeProperty> props)
        {
            Match m = _typeRegex.Match(text);
            if (m.Success)
            {
                props.Add(new TextNodeProperty(nodeId, TextNodeProps.TYPE,
                    m.Groups[1].Value));
                return true;
            }
            m = _writingRegex.Match(text);
            if (m.Success)
            {
                props.Add(new TextNodeProperty(nodeId, TextNodeProps.LAYOUT,
                    text.Trim()));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Parses the datation.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="defaultToBc">if set to <c>true</c>, default era to BC
        /// when no explicit era is found.</param>
        /// <returns>
        /// Tuple where 1=datation, 2=a/p when the datation represents
        /// a terminus ante/post, 3=true when the datation has an explicit era
        /// set.
        /// </returns>
        private Tuple<Datation, char, bool>? ParseDatation(string text,
            bool defaultToBc = false)
        {
            Match m = _dateRegex.Match(text);
            if (!m.Success) return null;

            Datation d = new();
            char type = '.';
            switch (m.Groups["m"].Value.ToLowerInvariant())
            {
                case "c.":
                    d.IsApproximate = true;
                    break;
                case "ante":
                    type = 'a';
                    break;
                case "post":
                    type = 'p';
                    break;
            }

            d.IsCentury = m.Groups["century"].Length > 0;
            d.IsDubious = m.Groups["dubious"].Length > 0;

            if (m.Groups["nv"].Length > 0)
            {
                d.Value = int.Parse(m.Groups["nv"].Value,
                    CultureInfo.InvariantCulture);
                if (m.Groups["ord"].Length > 0) d.IsCentury = true;
            }
            else if (m.Groups["cv"].Length > 0)
            {
                d.IsCentury = true;
                d.Value = RomanNumber.FromRoman(
                    m.Groups["cv"].Value.ToUpperInvariant());
            }

            switch (m.Groups["era"].Value.ToLowerInvariant())
            {
                case "a.":
                case "ac":
                case "bc":
                    d.Value = -d.Value;
                    break;
                default:
                    if (m.Groups["era"].Length == 0 && defaultToBc)
                        d.Value = -d.Value;
                    break;
            }

            // cm: init. med. fin. convert into approx number
            if (d.IsCentury)
            {
                bool ac = d.Value < 0;

                switch (m.Groups["cm"].Value.ToLowerInvariant())
                {
                    case "init.":
                        // init. 5 a. = c. -490
                        // init. 5 p. = c. +410
                        d.IsCentury = false;
                        d.IsApproximate = true;
                        d.Value = (((Math.Abs(d.Value) - 1) * 100) +
                            (ac? 90 : 10)) * (ac ? -1 : 1);
                        break;
                    case "med.":
                        // med. 5 a. = c. -450
                        // med. 5 p. = c. +450
                        d.IsCentury = false;
                        d.IsApproximate = true;
                        d.Value = (((Math.Abs(d.Value) - 1) * 100) +
                            50) * (ac ? -1 : 1);
                        break;
                    case "fin.":
                        // fin. 5 a. = c. -410
                        // fin. 5 p. = c. +490
                        d.IsCentury = false;
                        d.IsApproximate = true;
                        d.Value = (((Math.Abs(d.Value) - 1) * 100) +
                            (ac? 10 : 90)) * (ac ? -1 : 1);
                        break;
                }
            }

            return Tuple.Create(d, type, m.Groups["era"].Length > 0);
        }

        private static bool IsCenturyDigit(char c)
            => c == 'I' || c == 'V' || c == 'X';

        private static string PreprocessDateSlash(string text)
        {
            int i = text.IndexOf('/');
            if (i < 1) return text;

            return (IsCenturyDigit(text[i - 1]) && IsCenturyDigit(text[i + 1])) ||
                (char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
                ? $"{text[..i]}-{text[(i + 1)..]}"
                : text;
        }

        private void EnsureNanLoaded()
        {
            if (_nanDates != null) return;

            _nanDates = new();
            using StreamReader reader = new(Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(
                    "Epicod.Scraper.Packhum.Assets.NanDates.csv")!,
                Encoding.UTF8);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] cols = line.Split(',');
                _nanDates[cols[0].ToLowerInvariant()] =
                    HistoricalDate.Parse(cols[1])!;
            }
        }

        public IList<HistoricalDate> ParseDates(string text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<HistoricalDate>();

            // corner cases: non-numeric dates like "early empire"
            // https://raw.githubusercontent.com/sommerschield/iphi/main/train/data/iphi_dates.py
            if (_cornerDateRegex.IsMatch(text))
            {
                string nan = text.Trim();
                EnsureNanLoaded();
                nan = nan.ToLowerInvariant();
                if (_nanDates!.ContainsKey(nan)) return new[] { _nanDates[nan] };
                else return Array.Empty<HistoricalDate>();
            }

            // first split at / for alternatives (e.g. "fin. s. VI/init. s. V a.")
            // unless / separates Roman or Arabic digits (e.g. "s. VI/V a.").
            // In the latter case, replace / with - thus making it a range.
            text = PreprocessDateSlash(text);
            bool defaultToBC = false;
            bool firstDt = true;

            // process multiple dates in reverse order, as in this case the era
            // for points is often implicit in the first, like in the above sample
            // for "fin. s. VI". We use defaultToBC for this purpose, but for
            // non-range only dates.
            string[] textDates = text.Split('/');
            int dateNr = textDates.Length;
            List<HistoricalDate> dates = new();

            foreach (string dt in textDates.Reverse())
            {
                HistoricalDate date = new();

                // then split at - for ranges
                int i;
                if ((i = dt.IndexOf('-')) > -1)
                {
                    // with an explicit range we assume that no ante/post is
                    // possible, so we just set start and end points.
                    // Also, we first parse the end point, as usually in a range
                    // the era is defined only for the 2nd point, and implicitly
                    // applies to the 1st (e.g. 440-430 a.).
                    var b = ParseDatation(dt[(i + 1)..], false);
                    if (b == null) continue;
                    date.SetEndPoint(b.Item1);

                    var a = ParseDatation(dt[..i], b.Item1.Value < 0);
                    if (a == null) continue;

                    date.SetStartPoint(a.Item1);
                }
                else
                {
                    var p = ParseDatation(dt, defaultToBC);
                    if (p == null) continue;
                    switch (p.Item2)
                    {
                        case 'a':
                            date.SetEndPoint(p.Item1);
                            break;
                        case 'p':
                            date.SetStartPoint(p.Item1);
                            break;
                        default:
                            date.SetSinglePoint(p.Item1);
                            break;
                    }
                }

                dates.Add(date);
                if (firstDt)
                {
                    firstDt = false;
                    if (date.GetSortValue() < 0) defaultToBC = true;
                }

                dateNr--;
            }

            // restore original order
            dates.Reverse();
            return dates;
        }

        private bool ParseDate(string text, int nodeId,
            List<TextNodeProperty> props)
        {
            IList<HistoricalDate> dates = ParseDates(text);
            int n = 0;

            foreach (HistoricalDate date in dates)
            {
                // all the alternative dates after the 1st are suffixed
                // (e.g. "date-txt#1", "date-val#1", "date-txt#2"...)
                string suffix = ++n > 1? $"#{n}" : "";

                props.Add(new TextNodeProperty(nodeId,
                    TextNodeProps.DATE_TXT + suffix,
                    date.ToString()));

                props.Add(new TextNodeProperty(nodeId,
                    TextNodeProps.DATE_VAL + suffix,
                    date.GetSortValue().ToString(CultureInfo.InvariantCulture),
                    TextNodeProps.TYPE_INT));
            }

            return dates.Count > 0;
        }

        /// <summary>
        /// Parses the specified note.
        /// </summary>
        /// <param name="note">The note.</param>
        /// <param name="nodeId">The node identifier.</param>
        /// <returns>Properties parsed from note.</returns>
        public IList<TextNodeProperty> ParseNote(string note, int nodeId)
        {
            if (string.IsNullOrEmpty(note))
                return Array.Empty<TextNodeProperty>();

            string[] tokens = note.Split(_seps);

            List<TextNodeProperty> props = new()
            {
                // region
                new TextNodeProperty(nodeId, TextNodeProps.REGION,
                    tokens[0].Trim())
            };
            bool hasLoc = false, hasType = false, hasDate = false;

            for (int i = 1; i < tokens.Length; i++)
            {
                // type
                if (!hasType && ParseType(tokens[i], nodeId, props))
                {
                    // there cannot be a location after a type
                    hasLoc = true;
                    hasType = true;
                    continue;
                }

                // date
                if (!hasDate && ParseDate(tokens[i], nodeId, props))
                {
                    props.Add(new TextNodeProperty(nodeId,
                        TextNodeProps.DATE_PHI, tokens[i].Trim()));
                    hasDate = true;
                    // there cannot be a location after a date
                    hasLoc = true;
                    continue;
                }

                if (!hasLoc && !_refAbbrRegex.IsMatch(tokens[i]))
                {
                    props.Add(new TextNodeProperty(
                        nodeId, TextNodeProps.LOCATION, tokens[i].Trim()));
                    hasLoc = true;
                }
                else
                {
                    props.Add(new TextNodeProperty(
                        nodeId, TextNodeProps.REFERENCE, tokens[i].Trim()));
                }
            }

            return props;
        }
    }
}
