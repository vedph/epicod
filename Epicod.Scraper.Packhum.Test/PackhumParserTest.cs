using Epicod.Core;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Epicod.Scraper.Packhum.Test
{
    public sealed class PackhumParserTest
    {
        [Fact]
        public void Parse_RegionLocation_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — Lamptrai: Thiti", 1);

            Assert.Equal(2, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.LOCATION && p.Value == "Lamptrai: Thiti"));
        }

        [Fact]
        public void Parse_RegionLocationType_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props =
                parser.ParseNote("Att. — Athens: Akropolis — stoich. 28", 1);

            Assert.Equal(3, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.LOCATION && p.Value == "Athens: Akropolis"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.LAYOUT && p.Value == "stoich. 28"));
        }

        [Fact]
        public void Parse_RegionLocationTypeDate_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props =
                parser.ParseNote("Att. — Athens: Akropolis — stoich. 28 — 440-410 a.", 1);

            Assert.Equal(6, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.LOCATION && p.Value == "Athens: Akropolis"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.LAYOUT && p.Value == "stoich. 28"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "440-410 a."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "440 -- 410 BC"));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-425"));
        }

        [Fact]
        public void Parse_RegionLocationTypeDateRefs_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props =
                parser.ParseNote("Att. — Athens: Akropolis — stoich. 28 " +
                "— 440-410 a. — IG I² 87,f + 141,a, + 174 — IG I³, Add.p.950", 1);

            Assert.Equal(8, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.LOCATION && p.Value == "Athens: Akropolis"));

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
        public void Parse_RegionDate_SlashAsRange_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props = parser.ParseNote("Att. — 430/410 a.", 1);

            Assert.Equal(4, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI && p.Value == "430/410 a."));

            // date-txt
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "430 -- 410 BC"));

            // date-val
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-420"));
        }

        [Fact]
        public void Parse_RegionDate_SlashAsTwoDates_Ok()
        {
            PackhumParser parser = new();
            IList<TextNodeProperty> props =
                parser.ParseNote("Att. — fin. s. VI/init. s. V a.", 1);

            Assert.Equal(6, props.Count);

            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.REGION && p.Value == "Att."));

            // date-phi
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_PHI &&
                p.Value == "fin. s. VI/init. s. V a."));

            // date-txt (fin. s. VI)
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT && p.Value == "c. 510 BC"));

            // date-val (fin. s. VI)
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL && p.Value == "-510"));

            // date-txt#2 (init. s. V a.)
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_TXT + "#2" && p.Value == "c. 490 BC"));

            // date-val#2 (init. s. V a.)
            Assert.NotNull(props.FirstOrDefault(
                p => p.Name == TextNodeProps.DATE_VAL + "#2" && p.Value == "-490"));
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
