using System.Threading.Tasks;
using Xunit;

namespace Epicod.Scraper.Clauss.Test
{
    public class UnitTest1
    {
        [Fact]
        public async Task TestClient()
        {
            await Experiment.GoHttpClient();
        }

        [Fact]
        public void Test1()
        {
            Experiment.Go();
        }
    }
}