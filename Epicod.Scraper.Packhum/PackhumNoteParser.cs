using Epicod.Core;
using Fusi.Antiquity.Chronology;
using Fusi.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Epicod.Scraper.Packhum
{
    /// <summary>
    /// A simple parser for a Packhum text note.
    /// </summary>
    public sealed class PackhumNoteParser
    {
        private readonly char[] _seps;
        private readonly Regex _typeRegex;
        private readonly Regex _writingRegex;
        private readonly Regex _cornerDateRegex;
        private readonly Regex _dateRegex;
        private readonly Regex _refAbbrRegex;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackhumNoteParser"/> class.
        /// </summary>
        public PackhumNoteParser()
        {
            _seps = new[] { '\u2014' };

            // type like [pottery]
            _typeRegex = new Regex(@"^\s*\[([^]]+)\]\s*$");

            // type as writing direction/layout
            _writingRegex = new Regex(@"^\s*(?:stoich|non-stoich|boustr|retrogr)\.\s*\d*\s*$");

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
                props.Add(new TextNodeProperty(nodeId, PackhumProps.TYPE,
                    m.Groups[1].Value));
                return true;
            }
            m = _writingRegex.Match(text);
            if (m.Success)
            {
                props.Add(new TextNodeProperty(nodeId, PackhumProps.LAYOUT,
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
        private Tuple<Datation, char, bool> ParseDatation(string text,
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

        private static bool IsCenturyDigit(char c) => c == 'I' || c == 'V' || c == 'X';

        private static string PreprocessDateSlash(string text)
        {
            int i = text.IndexOf('/');
            if (i < 1) return text;

            return (IsCenturyDigit(text[i - 1]) && IsCenturyDigit(text[i + 1])) ||
                (char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
                ? $"{text.Substring(0, i)}-{text.Substring(i + 1)}"
                : text;
        }

        private bool ParseDate(string text, int nodeId,
            List<TextNodeProperty> props)
        {
            // corner cases: non-numeric dates like "early empire"
            if (_cornerDateRegex.IsMatch(text))
            {
                props.Add(new TextNodeProperty(nodeId, PackhumProps.DATE_NAN,
                    text.Trim()));
                return true;
            }

            // first split at / for alternatives (e.g. "fin. s. VI/init. s. V a.")
            // unless / separates Roman or Arabic digits (e.g. "s. VI/V a.");
            // in the latter case, replace / with - thus making it a range.
            bool isDate = false;
            text = PreprocessDateSlash(text);
            bool defaultToBC = false;
            bool firstDt = true;

            // process multiple dates in reverse order, as in this case the era
            // for points is often implicit in the first, like in the above sample
            // for "fin. s. VI". We use defaultToBC for this purpose, but for
            // non-range only dates.
            string[] dates = text.Split('/');
            int dateNr = dates.Length;
            foreach (string dt in dates.Reverse())
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
                    var b = ParseDatation(dt.Substring(i + 1), false);
                    if (b == null) continue;
                    date.SetEndPoint(b.Item1);

                    var a = ParseDatation(dt.Substring(0, i), b.Item1.Value < 0);
                    if (a == null) continue;
                    isDate = true;

                    date.SetStartPoint(a.Item1);
                }
                else
                {
                    var p = ParseDatation(dt, defaultToBC);
                    if (p == null) continue;
                    isDate = true;
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

                // all the alternative dates after the 1st are suffixed
                // (e.g. "date-txt#1", "date-val#1", "date-txt#2"...)
                string suffix = "";
                if (dateNr > 1) suffix = $"#{dateNr}";

                props.Add(new TextNodeProperty(nodeId,
                    PackhumProps.DATE_TXT + suffix,
                    date.ToString()));

                double val = date.GetSortValue();
                props.Add(new TextNodeProperty(nodeId,
                    PackhumProps.DATE_VAL + suffix,
                    val.ToString(CultureInfo.InvariantCulture)));

                if (firstDt)
                {
                    firstDt = false;
                    if (val < 0) defaultToBC = true;
                }

                dateNr--;
            }

            return isDate;
        }

        /// <summary>
        /// Parses the specified note.
        /// </summary>
        /// <param name="note">The note.</param>
        /// <param name="nodeId">The node identifier.</param>
        /// <returns>Properties parsed from note.</returns>
        public IList<TextNodeProperty> Parse(string note, int nodeId)
        {
            if (string.IsNullOrEmpty(note)) return Array.Empty<TextNodeProperty>();

            string[] tokens = note.Split(_seps);

            List<TextNodeProperty> props = new();

            // region
            props.Add(new TextNodeProperty(nodeId, PackhumProps.REGION,
                tokens[0].Trim()));
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
                        PackhumProps.DATE_PHI, tokens[i].Trim()));
                    hasDate = true;
                    // there cannot be a location after a date
                    hasLoc = true;
                    continue;
                }

                if (!hasLoc && !_refAbbrRegex.IsMatch(tokens[i]))
                {
                    props.Add(new TextNodeProperty(
                        nodeId, PackhumProps.LOCATION, tokens[i].Trim()));
                    hasLoc = true;
                }
                else
                {
                    props.Add(new TextNodeProperty(
                        nodeId, PackhumProps.REFERENCE, tokens[i].Trim()));
                }
            }

            return props;
        }
    }
}
