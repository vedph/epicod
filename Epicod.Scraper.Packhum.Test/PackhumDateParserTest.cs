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
        public void PreprocessForSplit_Ok(string text, string expected)
        {
            PackhumDateParser parser = new();
            string actual = parser.PreprocessForSplit(text);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("", new string[0])]
        public void SplitDates(string text, string[] expected)
        {
            PackhumDateParser parser = new();
            string[] actual = parser.SplitDates(text).ToArray();
            Assert.Equal(expected, actual);
        }
    }
}
