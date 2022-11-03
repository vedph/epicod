using Epicod.Core;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Epicod.Scraper.Packhum.Test
{
    public sealed class PackhumParserTest
    {
        [Fact]
        public void Parse_RegionSite_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — Lamptrai: Thiti", 1);

            Assert.Equal(2, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.SITE && p.Value == "Lamptrai: Thiti"));
        }

        [Fact]
        public void Parse_RegionSiteType_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props =
                parser.ParseNote("Att. — Athens: Akropolis — stoich. 28", 1);

            Assert.Equal(5, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.SITE && p.Value == "Athens: Akropolis"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.LAYOUT && p.Value == "stoich. 28"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.STOICH_MIN && p.Value == "28"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.STOICH_MAX && p.Value == "28"));
        }

        [Theory]
        [InlineData("Att. — Athens — stoich.", true, 0, 0)]
        [InlineData("Att. — Athens — non-stoich.", true, -1, -1)]
        [InlineData("Att. — Athens — stoich. 28", true, 28, 28)]
        [InlineData("Att. — Athens — stoich. 28?", true, 28, 28)]
        [InlineData("Att. — Athens — stoich. 28(?)", true, 28, 28)]
        [InlineData("Att. — Athens — stoich. c.28", true, 28, 28)]
        [InlineData("Att. — Athens — stoich. c. 28", true, 28, 28)]
        [InlineData("Att. — Athens — stoich. c.28?", true, 28, 28)]
        [InlineData("Att. — Athens — stoich. c.28(?)", true, 28, 28)]
        [InlineData("Att. — Athens — stoich. 28-30", true, 28, 30)]
        [InlineData("Att. — Athens — stoich. 28-30?", true, 28, 30)]
        [InlineData("Att. — Athens — stoich. 30/28", true, 28, 30)]
        [InlineData("Att. — Athens — stoich. 30/28?", true, 28, 30)]
        [InlineData("Att. — Athens — stoich. c.28/30", true, 28, 30)]
        [InlineData("Att. — Athens — stoich. c.28/30?", true, 28, 30)]
        [InlineData("Att. — Athens — stoich. c.30/28", true, 28, 30)]
        [InlineData("Att. — Athens — stoich. c.30/28?", true, 28, 30)]
        [InlineData("Att. — Athens — stoich. 24 (25)", true, 24, 24)]
        [InlineData("Att. — Athens — stoich. (v.1-4)", true, 0, 0)]
        public void Parse_Stoich_Ok(string text, bool stoich, int min, int max)
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote(text, 1);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.SITE && p.Value == "Athens"));

            Assert.NotNull(props.FirstOrDefault(p => p.Name == TextNodeProps.LAYOUT));

            if (min != -1 && max != -1)
            {
                Assert.NotNull(props.FirstOrDefault(
                    p => p.Name == (stoich
                        ? TextNodeProps.STOICH_MIN
                        : TextNodeProps.NON_STOICH_MIN) && p.Value == $"{min}"));

                Assert.NotNull(props.FirstOrDefault(
                    p => p.Name == (stoich
                        ? TextNodeProps.STOICH_MAX
                        : TextNodeProps.NON_STOICH_MAX) && p.Value == $"{max}"));
            }
        }

        [Fact]
        public void Parse_RegionSiteTypeDate_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props =
                parser.ParseNote("Att. — Athens: Akropolis — stoich. 28 — 440-410 a.", 1);

            Assert.Equal(8, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.SITE && p.Value == "Athens: Akropolis"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.LAYOUT && p.Value == "stoich. 28"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "440-410 a."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "440 -- 410 BC"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-425"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.STOICH_MIN && p.Value == "28"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.STOICH_MAX && p.Value == "28"));
        }

        [Fact]
        public void Parse_RegionSiteTypeDateWithForgery_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props =
                parser.ParseNote(
                    "Att. — Athens: Akropolis — stoich. 28 — 440-410 a. (forgery?)", 1);

            Assert.Equal(9, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.SITE && p.Value == "Athens: Akropolis"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.LAYOUT && p.Value == "stoich. 28"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI &&
                     p.Value == "440-410 a. (forgery?)"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT &&
                     p.Value == "440 -- 410 BC {forgery?}"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-425"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.FORGERY && p.Value == "3"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.STOICH_MIN && p.Value == "28"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.STOICH_MAX && p.Value == "28"));
        }

        [Fact]
        public void Parse_RegionSiteTypeRefs_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props =
                parser.ParseNote("Att. — prov.? — forgery? — IG I³, p.972", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.SITE && p.Value == "prov.?"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.FORGERY && p.Value == "3"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REFERENCE && p.Value == "IG I³, p.972"));
        }

        [Fact]
        public void Parse_RegionSiteTypeDateRefs_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props =
                parser.ParseNote("Att. — Athens: Akropolis — stoich. 28 " +
                "— 440-410 a. — IG I² 87,f + 141,a, + 174 — IG I³, Add.p.950", 1);

            Assert.Equal(10, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.SITE && p.Value == "Athens: Akropolis"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.LAYOUT && p.Value == "stoich. 28"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "440-410 a."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "440 -- 410 BC"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-425"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REFERENCE && p.Value == "IG I² 87,f + 141,a, + 174"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REFERENCE && p.Value == "IG I³, Add.p.950"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.STOICH_MIN && p.Value == "28"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.STOICH_MAX && p.Value == "28"));
        }

        [Fact]
        public void Parse_RegionType_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — [pottery]", 1);

            Assert.Equal(2, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.TYPE && p.Value == "pottery"));
        }

        [Fact]
        public void Parse_RegionDate_CenturyWithOrd_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — 2nd a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "2nd a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "II BC"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-150"));
        }

        [Fact]
        public void Parse_RegionDate_AboutCenturyWithOrd_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — c. 2nd a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "c. 2nd a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "c. II BC"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-150"));
        }

        [Fact]
        public void Parse_RegionDate_Century_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — s. VI a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "s. VI a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "VI BC"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-550"));
        }

        [Fact]
        public void Parse_RegionDate_InitCentury_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — init. s. VI a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "init. s. VI a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "c. 590 BC"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-590"));
        }

        [Fact]
        public void Parse_RegionDate_MedCentury_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — med. s. VI a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "med. s. VI a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "c. 550 BC"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-550"));
        }

        [Fact]
        public void Parse_RegionDate_FinCentury_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — fin. s. VI a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "fin. s. VI a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "c. 510 BC"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-510"));
        }

        [Fact]
        public void Parse_RegionDate_Year_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — 427 a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "427 a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "427 BC"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-427"));
        }

        [Fact]
        public void Parse_RegionDate_AnteYear_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — ante 450 a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "ante 450 a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "-- 450 BC"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-460"));
        }

        [Fact]
        public void Parse_RegionDate_PostYear_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — post 450 a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "post 450 a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "450 BC --"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-440"));
        }

        [Fact]
        public void Parse_RegionDate_YearRangeBC_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — 440-430 a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "440-430 a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "440 -- 430 BC"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-435"));
        }

        [Fact]
        public void Parse_RegionDate_YearRangeAD_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — 430-440 p.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "430-440 p."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "430 -- 440 AD"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "435"));
        }

        [Fact]
        public void Parse_RegionRef_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — IG I³, Add.p.950", 1);

            Assert.Equal(2, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REFERENCE && p.Value == "IG I³, Add.p.950"));
        }
    }
}
