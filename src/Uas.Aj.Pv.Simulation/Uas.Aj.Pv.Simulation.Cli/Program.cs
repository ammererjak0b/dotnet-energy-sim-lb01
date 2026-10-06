using Microsoft.Extensions.DependencyInjection;
using Uas.Aj.Pv.Simulation.Core.Device;
using Uas.Aj.Pv.Simulation.Core.History;
using Uas.Aj.Pv.Simulation.Core.Simulation;
using Uas.Aj.Pv.Simulation.Core.Weather;

namespace Uas.Aj.Pv.Simulation.Cli;

internal class Program
{
    // no args -> manual mode (paused, manual clouds), args -> auto mode (runs at once)
    private static int Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            ArgsParser.PrintHelp();
            return 0;
        }

        bool isCheck = args.Contains("--check");
        var simulationArgs = new List<string>();
        foreach (string arg in args)
        {
            if (arg != "--check")
            {
                simulationArgs.Add(arg);
            }
        }

        SimulationOptions options;
        PvPlantConfig plantConfig;
        try
        {
            (options, plantConfig) = ArgsParser.Parse(simulationArgs.ToArray());
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message); // typo in option, bad number
            return 1; // exit code 1 = error
        }

        using var provider = BuildServiceProvider(options, plantConfig); // dispose at end -> csv file gets closed

        try
        {
            provider.GetRequiredService<SimulationEngine>(); // fail fast: weather file, clock + plant guards
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine(exception.Message); // broken weather file, --days 0, --peak-kw -5
            return 1;
        }

        if (isCheck)
        {
            return provider.GetRequiredService<PvPlantCheck>().Run(); // pv model smoke check, no simulation
        }

        bool isManualStart = simulationArgs.Count == 0;
        return provider.GetRequiredService<ConsoleAppRunner>().Run(isManualStart);
    }

    // register here
    private static ServiceProvider BuildServiceProvider(SimulationOptions options, PvPlantConfig plantConfig)
    {
        var services = new ServiceCollection();

        services.AddSingleton(options);
        services.AddSingleton<SimulationClock>();
        services.AddSingleton<JsonFileWeatherSource>(); // real data
        services.AddSingleton<AdjustableWeatherSource>(); // auto/manual on top, runner switches mode here
        services.AddSingleton<IWeatherSource>(provider => provider.GetRequiredService<AdjustableWeatherSource>()); // engine gets adjusted weather
        services.AddSingleton(plantConfig);
        services.AddSingleton<PvPlant>();
        services.AddSingleton<SimulationEngine>();
        services.AddSingleton<CsvHistoryWriter>();
        services.AddSingleton<IHistoryWriter>(provider => provider.GetRequiredService<CsvHistoryWriter>()); // engine writes csv
        services.AddSingleton<ConsoleAppRunner>();
        services.AddTransient<PvPlantCheck>();

        return services.BuildServiceProvider();
    }
}
