using Microsoft.Extensions.DependencyInjection;
using Uas.Aj.Pv.Simulation.Core.Device;
using Uas.Aj.Pv.Simulation.Core.Simulation;
using Uas.Aj.Pv.Simulation.Core.Weather;

namespace Uas.Aj.Pv.Simulation.Cli;

internal class Program
{
    private static int Main(string[] args)
    {
        var provider = BuildServiceProvider();

        try
        {
            provider.GetRequiredService<IWeatherSource>(); // fail fast, loads the weather file now
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1; // exit code 1 = error
        }

        if (args.Contains("--check"))
        {
            return provider.GetRequiredService<PvPlantCheck>().Run(); // pv model smoke check, no simulation
        }

        return 0;
    }

    // register here
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // debug defaults until args parsing exists, path relative to working dir (run from repo root)
        var options = new SimulationOptions(
            new DateTime(2026, 9, 23),
            7,
            TimeSpan.FromMinutes(15),
            600,
            "data/salzburg-2026-09-23_2026-09-29.json");

        services.AddSingleton(options);
        services.AddSingleton<SimulationClock>();
        services.AddSingleton<IWeatherSource, JsonFileWeatherSource>();
        services.AddSingleton(new PvPlantConfig("pv-01", 5)); // debug default until args parsing exists
        services.AddSingleton<PvPlant>();
        services.AddTransient<PvPlantCheck>();
        // csv writer etc. get registered here once they exist

        return services.BuildServiceProvider();
    }
}
