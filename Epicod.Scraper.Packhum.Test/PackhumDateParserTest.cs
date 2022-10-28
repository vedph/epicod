using System.Linq;
using Xunit;

namespace Epicod.Scraper.Packhum.Test
{
    public sealed class PackhumDateParserTest
    {
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
        public void SplitDates(string text, string[] expected)
        {
            PackhumDateParser parser = new();
            text = parser.PreprocessForSplit(text);
            string[] actual = parser.SplitDates(text).ToArray();
            Assert.Equal(expected, actual);
        }
    }
}
