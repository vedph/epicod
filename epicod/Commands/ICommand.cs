using System.Threading.Tasks;

namespace Epicod.Cli.Commands
{
    public interface ICommand
    {
        Task Run();
    }
}
