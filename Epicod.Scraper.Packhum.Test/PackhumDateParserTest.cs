using Fusi.Antiquity.Chronology;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static System.Net.Mime.MediaTypeNames;

namespace Epicod.Scraper.Packhum.Test
{
    public sealed class PackhumDateParserTest
    {
        #region PreprocessForSplit
        [Theory]
        [InlineData("", "")]
        [InlineData("21 AD", "21 AD")]
        [InlineData(" \t21  AD\r\n \t ", "21 AD")]
        [InlineData("21 AD (hello)", "21 AD")]
        [InlineData("21 AD [hello]", "21 AD")]
        [InlineData("21 AD [hello (world)]", "21 AD")]
        [InlineData("21 AD [hello] (world)", "21 AD")]
        [InlineData("21 AD or lat.", "21 AD")]
        [InlineData("21 AD or sh. lat.", "21 AD")]
        [InlineData("21 AD or shortly lat.", "21 AD")]
        [InlineData("21 AD or slightly lat.", "21 AD")]
        [InlineData("21 AD or sh. later", "21 AD")]
        [InlineData("21 AD or sh. aft.", "21 AD")]
        [InlineData("21 AD or sh. after", "21 AD")]
        [InlineData("21 AD or sh. earlier", "21 AD")]
        [InlineData("21 AD or sh. früher", "21 AD")]
        [InlineData("21 AD or sh. später", "21 AD")]
        [InlineData("s. II AD, early", "s. II AD")]
        [InlineData("21 AD, 3 Jan.", "21 AD{d=3,m=1}")]
        [InlineData("21 AD, 3 January", "21 AD{d=3,m=1}")]
        [InlineData("21 AD, Aug.", "21 AD{m=8}")]
        [InlineData("21 AD w/ 214", "21 AD w 214")]
        [InlineData("21 AD w// 214", "21 AD w 214")]
        [InlineData("21 AD, July/August", "21 AD{m=7}")]
        public void PreprocessForSplit_Ok(string text, string expected)
        {
            PackhumDateParser parser = new();
            string actual = parser.PreprocessForSplit(text);
            Assert.Equal(expected, actual);
        }
        #endregion

        #region SplitDates
        [Theory]
        [InlineData("", new string[0])]
        [InlineData("21 AD", new[] { "21 AD" })]
        [InlineData("159-156? 157-156? BC (re-inscr. 1st c. AD)",
            new[]
            {
                "159-156?",
                "157-156? BC" }
            )]
        [InlineData("11th/beg. 12th c. AD",
            new[]
            {
                "11th",
                "beg. 12th c. AD" }
            )]
        [InlineData("111 and 117 AD", new[]
        {
            "111",
            "117 AD"
        })]
        [InlineData("10/11 AD and 65/66 or 119/120 AD",
            new[]
            {
                "10/11 AD",
                "65/66",
                "119/120 AD"
            })]
        [InlineData("196 AD [set up betw. 205 and 211?]", new[] { "196 AD" })]
        [InlineData("123 od. 131/2 n.Chr.", new[]
        {
            "123",
            "131/2 n.Chr."
        })]
        [InlineData("179 oder 175 n.Chr.", new[]
        {
            "179",
            "175 n.Chr."
        })]
        [InlineData("118/9 & 129/30 AD", new[]
        {
            "118/9",
            "129/30 AD"
        })]
        [InlineData("114, 116, & 156 AD", new[]
        {
            "114",
            "116",
            "156 AD"
        })]
        public void SplitDates_Ok(string text, string[] expected)
        {
            PackhumDateParser parser = new();
            text = parser.PreprocessForSplit(text);
            string[] actual = PackhumDateParser.SplitDates(text).ToArray();
            Assert.Equal(expected, actual);
        }
        #endregion

        #region SplitDatations
        [Theory]
        [InlineData("", new string[0])]
        [InlineData("100-200 AD", new [] { "100", "200 AD"})]
        [InlineData("123/124-222/223 AD", new[]
        {
            "123/124",
            "222/223 AD"
        })]
        [InlineData("10th-11th c. AD", new[]
        {
            "10th",
            "11th c. AD"
        })]
        public void SplitDatations_Ok(string text, string[] expected)
        {
            PackhumDateParser parser = new();
            text = parser.PreprocessForSplit(text);
            string[] actual = PackhumDateParser.SplitDatations(text).ToArray();
            Assert.Equal(expected, actual);
        }
        #endregion

        #region PreprocessDatations
        [Theory]
        [InlineData("", "")]
        [InlineData("139p.", "139 p.")]
        [InlineData("222-235p.", "222-235 p.")]
        public void PreprocessDatations_DetachAP_Ok(string text, string expected)
        {
            PackhumDateParser parser = new();
            text = parser.PreprocessForSplit(text);
            string actual =
                PackhumDateParser.PreprocessDatations(new[] { text })[0].Item1;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void PreprocessDatations_CaInitial_Ok()
        {
            var tads = PackhumDateParser.PreprocessDatations(new[]
            {
                "c. 123 AD", "456 AD"
            });

            Assert.Equal(2, tads.Count);
            Assert.Equal("123 AD", tads[0].Item1);
            Assert.True(tads[0].Item2);
            Assert.False(tads[0].Item3);

            Assert.Equal("456 AD", tads[1].Item1);
            Assert.True(tads[1].Item2);
            Assert.False(tads[1].Item3);
        }

        [Fact]
        public void PreprocessDatations_CaNonInitial_Ok()
        {
            var tads = PackhumDateParser.PreprocessDatations(new[]
            {
                "123 AD", "c. 456 AD"
            });

            Assert.Equal(2, tads.Count);
            Assert.Equal("123 AD", tads[0].Item1);
            Assert.False(tads[0].Item2);
            Assert.False(tads[0].Item3);

            Assert.Equal("456 AD", tads[1].Item1);
            Assert.True(tads[1].Item2);
            Assert.False(tads[1].Item3);
        }

        [Fact]
        public void PreprocessDatations_QmkFinal_Ok()
        {
            var tads = PackhumDateParser.PreprocessDatations(new[]
            {
                "100", "125 AD?"
            });

            Assert.Equal(2, tads.Count);
            Assert.Equal("100", tads[0].Item1);
            Assert.False(tads[0].Item2);
            Assert.True(tads[0].Item3);

            Assert.Equal("125 AD", tads[1].Item1);
            Assert.False(tads[1].Item2);
            Assert.True(tads[1].Item3);
        }

        [Fact]
        public void PreprocessDatations_QmkNonFinal_Ok()
        {
            var tads = PackhumDateParser.PreprocessDatations(new[]
            {
                "100?", "125 AD"
            });

            Assert.Equal(2, tads.Count);
            Assert.Equal("100", tads[0].Item1);
            Assert.False(tads[0].Item2);
            Assert.True(tads[0].Item3);

            Assert.Equal("125 AD", tads[1].Item1);
            Assert.False(tads[1].Item2);
            Assert.False(tads[1].Item3);
        }

        [Fact]
        public void PreprocessDatations_Mid_Ok()
        {
            var tads = PackhumDateParser.PreprocessDatations(new[]
            {
                "mid-1st c. BC"
            });

            Assert.Equal(1, tads.Count);
            Assert.Equal("med. 1st c. BC", tads[0].Item1);
            Assert.False(tads[0].Item2);
            Assert.False(tads[0].Item3);
        }

        [Fact]
        public void PreprocessDatations_LaterThanTheEarly_Ok()
        {
            var tads = PackhumDateParser.PreprocessDatations(new[]
            {
                "later than the early 5th c. BC?"
            });

            Assert.Equal(1, tads.Count);
            Assert.Equal("5th c. BC", tads[0].Item1);
            Assert.False(tads[0].Item2);
            Assert.True(tads[0].Item3);
        }
        #endregion

        #region Parse
        [Fact]
        public void Parse_Empty_Empty()
        {
            PackhumDateParser parser = new();

            IList<HistoricalDate> dates = parser.Parse("");

            Assert.Empty(dates);
        }

        [Theory]
        [InlineData("early Roman period")]
        [InlineData("EARLY roman period")]
        public void Parse_PeriodNoQmk_Ok(string text)
        {
            PackhumDateParser parser = new();

            IList<HistoricalDate> dates = parser.Parse(text);

            Assert.Single(dates);
            Assert.Equal("200 -- 1 BC", dates[0].ToString());
        }

        [Theory]
        [InlineData("early Roman period?")]
        [InlineData("early? Roman period")]
        [InlineData("EARLY roman period?")]
        public void Parse_PeriodWithQmk_Ok(string text)
        {
            PackhumDateParser parser = new();

            IList<HistoricalDate> dates = parser.Parse(text);

            Assert.Single(dates);
            Assert.Equal("200 ? -- 1 BC ?", dates[0].ToString());
        }

        [Theory]
        // year
        [InlineData("21", "21 AD")]
        [InlineData("c.21", "c. 21 AD")]
        [InlineData("ca. 21", "c. 21 AD")]
        [InlineData("21?", "21 AD ?")]
        [InlineData("21 ?", "21 AD ?")]
        [InlineData("c. 21?", "c. 21 AD ?")]
        [InlineData("21 (hint)", "21 AD {hint}")]
        [InlineData("c.21 (hint)", "c. 21 AD {hint}")]
        [InlineData("21? (hint)", "21 AD ? {hint}")]
        [InlineData("c. 21? (hint)", "c. 21 AD ? {hint}")]
        // century
        [InlineData("III", "III AD")]
        [InlineData("s. III", "III AD")]
        [InlineData("3rd c.", "III AD")]
        [InlineData("3rd", "III AD")]
        [InlineData("s. III BC", "III BC")]
        [InlineData("c. s. III BC", "c. III BC")]
        [InlineData("s. III? BC", "III BC ?")]
        [InlineData("s. III BC?", "III BC ?")]
        // TODO
        public void Parse_N_Ok(string text, string expected)
        {
            PackhumDateParser parser = new();

            IList<HistoricalDate> dates = parser.Parse(text);

            Assert.Single(dates);
            Assert.Equal(expected, dates[0].ToString());
        }

        [Theory]
        [InlineData("21 BC", "21 BC")]
        [InlineData("21 bc", "21 BC")]
        [InlineData("21 a.", "21 BC")]
        [InlineData("21 v.Chr.", "21 BC")]
        [InlineData("21 v. Chr.", "21 BC")]
        public void Parse_Eras_Ok(string text, string expected)
        {
            PackhumDateParser parser = new();

            IList<HistoricalDate> dates = parser.Parse(text);

            Assert.Single(dates);
            Assert.Equal(expected, dates[0].ToString());
        }
        #endregion
    }
}
