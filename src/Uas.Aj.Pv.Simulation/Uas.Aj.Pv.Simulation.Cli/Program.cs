using Microsoft.Extensions.DependencyInjection;

namespace Uas.Aj.Pv.Simulation.Cli;

class Program
{
    static void Main(string[] args)
    {
        var services = new ServiceCollection();
        // device model, simulation clock, csv writer etc. get registered here once they exist

        var provider = services.BuildServiceProvider();
    }
}
