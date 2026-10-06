using System.Globalization;
using Spectre.Console;
using Uas.Aj.Pv.Simulation.Core.Device;
using Uas.Aj.Pv.Simulation.Core.Simulation;

namespace Uas.Aj.Pv.Simulation.Cli;

// reads "--key value" pairs into start settings, missing key -> default
// unknown key or bad value -> ArgumentException with clear message
internal static class ArgsParser
{
    // defaults, used by Parse and shown by --help
    private const string DefaultStart = "2026-09-23";
    private const int DefaultDays = 7;
    private const double DefaultStepMinutes = 15;
    private const double DefaultSpeed = 600;
    private const string DefaultWeatherPath = "data/salzburg-2026-09-23_2026-09-29.json";
    private const string DefaultHistoryFolder = "data/runs";
    private const string DefaultDeviceId = "pv-01";
    private const double DefaultPeakKw = 5;
    private const double DefaultPerformanceRatio = 0.8;

    public static (SimulationOptions Options, PvPlantConfig PlantConfig) Parse(string[] args)
    {
        var values = ReadPairs(args);

        var options = new SimulationOptions(
            ReadDate(values, "--start", DefaultStart),
            ReadInt(values, "--days", DefaultDays),
            TimeSpan.FromMinutes(ReadDouble(values, "--step", DefaultStepMinutes)), // minutes per tick
            ReadDouble(values, "--speed", DefaultSpeed),
            ReadText(values, "--weather", DefaultWeatherPath),
            ReadText(values, "--history-folder", DefaultHistoryFolder));

        var plantConfig = new PvPlantConfig(
            ReadText(values, "--device-id", DefaultDeviceId),
            ReadDouble(values, "--peak-kw", DefaultPeakKw),
            ReadDouble(values, "--performance-ratio", DefaultPerformanceRatio));

        if (values.Count > 0)
        {
            throw new ArgumentException($"unknown option {string.Join(", ", values.Keys)}"); // every known key was removed while reading
        }

        return (options, plantConfig);
    }

    private static Dictionary<string, string> ReadPairs(string[] args)
    {
        var values = new Dictionary<string, string>();
        for (int index = 0; index < args.Length; index += 2)
        {
            string key = args[index];
            if (!key.StartsWith("--"))
            {
                throw new ArgumentException($"expected an option like --peak-kw, got '{key}'");
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"option {key} has no value");
            }

            values[key] = args[index + 1];
        }

        return values;
    }

    // read + remove, leftovers are unknown keys
    private static string ReadText(Dictionary<string, string> values, string key, string defaultValue)
    {
        if (!values.Remove(key, out var value))
        {
            return defaultValue;
        }

        return value;
    }

    private static double ReadDouble(Dictionary<string, string> values, string key, double defaultValue)
    {
        string text = ReadText(values, key, defaultValue.ToString(CultureInfo.InvariantCulture));
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new ArgumentException($"option {key}: '{text}' is not a number (use . as decimal point)");
        }

        return value;
    }

    private static int ReadInt(Dictionary<string, string> values, string key, int defaultValue)
    {
        string text = ReadText(values, key, defaultValue.ToString(CultureInfo.InvariantCulture));
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new ArgumentException($"option {key}: '{text}' is not a whole number");
        }

        return value;
    }

    private static DateTime ReadDate(Dictionary<string, string> values, string key, string defaultText)
    {
        string text = ReadText(values, key, defaultText);
        if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            throw new ArgumentException($"option {key}: '{text}' is not a date like 2026-09-23");
        }

        return value;
    }

    // --help: modes, options with defaults, keys
    public static void PrintHelp()
    {
        AnsiConsole.MarkupLine("[bold]PV plant simulator[/]");
        AnsiConsole.MarkupLine("Usage: [bold]Uas.Aj.Pv.Simulation.Cli [[--option value]]...[/]   (run from repo root)");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Modes[/]");
        AnsiConsole.MarkupLine("  no options    manual: starts paused, clouds set by hand, step with n or run with space");
        AnsiConsole.MarkupLine("  any option    auto: runs at once, clouds drift randomly around real weather");
        AnsiConsole.MarkupLine("  --check       runs the model checks, no simulation");
        AnsiConsole.MarkupLine("  --help, -h    this overview");
        AnsiConsole.WriteLine();

        var options = new Table().Title("[bold]Options[/]").Border(TableBorder.Simple);
        options.AddColumn("Option");
        options.AddColumn("Default");
        options.AddColumn("Meaning");
        options.AddRow("--device-id", DefaultDeviceId, "name of this plant, used in the csv file name");
        options.AddRow("--peak-kw", Number(DefaultPeakKw), "nominal power in kWp (at 1000 W/m², 25 °C)");
        options.AddRow("--performance-ratio", Number(DefaultPerformanceRatio), "losses as one factor, 0-1");
        options.AddRow("--start", DefaultStart, "first simulated day (yyyy-MM-dd)");
        options.AddRow("--days", DefaultDays.ToString(), "simulated days");
        options.AddRow("--step", Number(DefaultStepMinutes), "simulated minutes per tick");
        options.AddRow("--speed", Number(DefaultSpeed), "times faster than real time");
        options.AddRow("--weather", DefaultWeatherPath, "weather json (open-meteo)");
        options.AddRow("--history-folder", DefaultHistoryFolder, "folder for the csv history");
        AnsiConsole.Write(options);

        var keys = new Table().Title("[bold]Keys while running[/]").Border(TableBorder.Simple);
        keys.AddColumn("Key");
        keys.AddColumn("Action");
        foreach (var keyHelp in ConsoleAppRunner.KeyHelp)
        {
            keys.AddRow(Markup.Escape(keyHelp.Key), keyHelp.Action);
        }

        AnsiConsole.Write(keys);
        AnsiConsole.MarkupLine("Example: [bold]--peak-kw 10 --speed 2500 --days 1[/]");
    }

    private static string Number(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture); // same format as typed on the command line
    }
}
