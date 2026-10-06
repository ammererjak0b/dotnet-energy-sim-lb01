using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Rendering;
using Uas.Aj.Pv.Simulation.Core.Device;
using Uas.Aj.Pv.Simulation.Core.Simulation;
using Uas.Aj.Pv.Simulation.Core.Weather;

namespace Uas.Aj.Pv.Simulation.Cli;

// console loop: keys -> tick when due -> draw, real time waiting lives here, not in core
internal class ConsoleAppRunner
{
    private static readonly double[] SpeedPresets = { 1, 10, 60, 600, 1000, 2500 };

    // shown by --help, keep in sync with HandleKey
    public static readonly (string Key, string Action)[] KeyHelp =
    {
        ("space", "run / pause"),
        ("n", "one step (while paused)"),
        ("+ / -", "speed up / down (1, 10, 60, 600, 1000, 2500x)"),
        ("o", "plant on / off"),
        ("f", "trigger / reset inverter fault"),
        ("up / down", "power limit +-10 %"),
        ("m", "weather auto / manual"),
        ("left / right", "manual clouds -+10 %"),
        ("q", "quit, csv stays complete"),
    };
    private const int LoopDelayMilliseconds = 50; // keys + screen ~20x per second
    private const int MaxTicksPerLoop = 100; // catch up limit, no endless loop if far behind
    private const double PercentStep = 10; // power limit + clouds per key press
    private const int PowerBarWidth = 20; // chars

    private readonly SimulationEngine _engine;
    private readonly AdjustableWeatherSource _weatherSource;
    private readonly CsvHistoryWriter _csvWriter;
    private readonly SortedDictionary<DateTime, double> _dailyEnergyKwh = new(); // day -> energy, for bar chart
    private readonly Stopwatch _realTime = new();
    private TimeSpan _nextTickAt; // real time when next tick is due
    private bool _isQuitRequested;

    public ConsoleAppRunner(SimulationEngine engine, AdjustableWeatherSource weatherSource, CsvHistoryWriter csvWriter)
    {
        _engine = engine;
        _weatherSource = weatherSource;
        _csvWriter = csvWriter;
    }

    // manual start: paused + manual clouds, auto start: running + auto weather; exit code 0 = ok
    public int Run(bool isManualStart)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("needs a real terminal for keys (Rider: enable 'Use external console' in the run config)");
            return 1;
        }

        if (isManualStart)
        {
            _engine.Clock.Pause(); // user steps with n or starts with space
            _weatherSource.SetManual(0);
        }

        try
        {
            AnsiConsole.Live(BuildView()).Start(RunLoop);
        }
        catch (IOException exception)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(exception.Message)}[/]"); // csv write failed: no access, disk full
            return 1;
        }

        PrintSummary();
        return 0;
    }

    private void RunLoop(LiveDisplayContext context)
    {
        _realTime.Start();
        while (!_isQuitRequested && !_engine.Clock.IsFinished)
        {
            HandleKeys();
            TickWhenDue();
            context.UpdateTarget(BuildView());
            Thread.Sleep(LoopDelayMilliseconds);
        }

        context.UpdateTarget(BuildView()); // final state stays on screen
    }

    // ticks owed by real time, several per loop at high speed
    private void TickWhenDue()
    {
        if (_engine.Clock.IsPaused)
        {
            _nextTickAt = _realTime.Elapsed; // no burst of catch up ticks after resume
            return;
        }

        int ticksThisLoop = 0;
        while (_realTime.Elapsed >= _nextTickAt && !_engine.Clock.IsFinished && ticksThisLoop < MaxTicksPerLoop)
        {
            _engine.Tick();
            RecordDailyEnergy();
            _nextTickAt += _engine.Clock.RealTimePerTick;
            ticksThisLoop++;
        }
    }

    private void RecordDailyEnergy()
    {
        if (_engine.LastWeather != null)
        {
            _dailyEnergyKwh[_engine.LastWeather.Time.Date] = _engine.Plant.EnergyTodayKwh; // last value of a day = its energy
        }
    }

    private void HandleKeys()
    {
        while (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true); // no echo into the display
            HandleKey(key);
        }
    }

    private void HandleKey(ConsoleKeyInfo key)
    {
        if (key.KeyChar == '+')
        {
            ChangeSpeed(faster: true);
            return;
        }

        if (key.KeyChar == '-')
        {
            ChangeSpeed(faster: false);
            return;
        }

        var plant = _engine.Plant;
        switch (key.Key)
        {
            case ConsoleKey.Spacebar:
                TogglePause();
                break;
            case ConsoleKey.N:
                StepOnceWhilePaused();
                break;
            case ConsoleKey.O:
                ToggleOnOff(plant);
                break;
            case ConsoleKey.F:
                ToggleFault(plant);
                break;
            case ConsoleKey.UpArrow:
                plant.SetPowerLimit(Math.Min(plant.PowerLimitPercent + PercentStep, 100));
                break;
            case ConsoleKey.DownArrow:
                plant.SetPowerLimit(Math.Max(plant.PowerLimitPercent - PercentStep, 0));
                break;
            case ConsoleKey.M:
                ToggleWeatherMode();
                break;
            case ConsoleKey.RightArrow:
                ChangeManualClouds(PercentStep);
                break;
            case ConsoleKey.LeftArrow:
                ChangeManualClouds(-PercentStep);
                break;
            case ConsoleKey.Q:
                _isQuitRequested = true;
                break;
        }
    }

    private void TogglePause()
    {
        if (_engine.Clock.IsPaused)
        {
            _engine.Clock.Resume();
            return;
        }

        _engine.Clock.Pause();
    }

    private void StepOnceWhilePaused()
    {
        if (!_engine.Clock.IsPaused)
        {
            return; // running anyway, n would only skip ahead
        }

        _engine.StepOnce();
        RecordDailyEnergy();
    }

    private static void ToggleOnOff(PvPlant plant)
    {
        if (plant.IsOn)
        {
            plant.TurnOff();
            return;
        }

        plant.TurnOn();
    }

    private static void ToggleFault(PvPlant plant)
    {
        if (plant.HasFault)
        {
            plant.ResetFault();
            return;
        }

        plant.TriggerFault();
    }

    // next preset above / below current speed, works with any --speed value
    private void ChangeSpeed(bool faster)
    {
        double currentSpeed = _engine.Clock.Speed;
        double newSpeed = currentSpeed;
        foreach (double preset in SpeedPresets)
        {
            if (faster && preset > currentSpeed)
            {
                newSpeed = preset;
                break;
            }

            if (!faster && preset < currentSpeed)
            {
                newSpeed = preset; // keeps the last one below = closest
            }
        }

        _engine.Clock.SetSpeed(newSpeed);
    }

    private void ToggleWeatherMode()
    {
        if (_weatherSource.Mode == WeatherMode.Manual)
        {
            _weatherSource.SetAuto();
            return;
        }

        double currentClouds = _engine.LastWeather?.CloudCoverPercent ?? 0;
        _weatherSource.SetManual(Math.Round(currentClouds / PercentStep) * PercentStep); // start from what is on screen, on the 10 % grid
    }

    private void ChangeManualClouds(double change)
    {
        if (_weatherSource.Mode != WeatherMode.Manual)
        {
            return; // auto: clouds drift by themselves
        }

        _weatherSource.SetManual(Math.Clamp(_weatherSource.ManualCloudCoverPercent + change, 0, 100));
    }

    private IRenderable BuildView()
    {
        var clock = _engine.Clock;
        var plant = _engine.Plant;
        var weather = _engine.LastWeather;

        string runState = "[green]RUNNING[/]";
        if (clock.IsPaused)
        {
            runState = "[yellow]PAUSED[/]";
        }

        if (clock.IsFinished)
        {
            runState = "[blue]FINISHED[/]";
        }

        var header = new Markup(
            $"[bold]=== PV plant {Markup.Escape(plant.Config.DeviceId)} | {_weatherSource.Mode.ToString().ToUpper()} | {runState} ===[/]\n" +
            $"Sim time: [bold]{clock.CurrentTime:yyyy-MM-dd HH:mm}[/]   Step: {clock.Step.TotalMinutes} min   Speed: {clock.Speed}x   Peak: {plant.Config.PeakPowerKw} kWp");

        var table = new Table().HideHeaders().Border(TableBorder.Rounded);
        table.AddColumn("Value");
        table.AddColumn("Current");
        table.AddRow("Status", StatusMarkup(plant.Status));
        table.AddRow("Power", $"{plant.PowerKw,6:F2} kW  {PowerBar(plant)}");
        table.AddRow("Energy today", $"{plant.EnergyTodayKwh:F2} kWh");
        table.AddRow("Energy total", $"{plant.TotalEnergyKwh:F2} kWh");
        table.AddRow("Power limit", $"{plant.PowerLimitPercent:F0} %");
        table.AddRow("Irradiance", $"{weather?.IrradianceWattPerSquareMeter ?? 0:F0} W/m²");
        table.AddRow("Clouds", $"{weather?.CloudCoverPercent ?? 0:F0} % ({WeatherText(weather)}){ManualCloudsHint()}");
        table.AddRow("Temperature", $"{weather?.TemperatureCelsius ?? 0:F1} °C");

        var keys = new Markup("[grey]space run/pause | n step | +/- speed | o on/off | f fault | up/down limit | m auto/manual | left/right clouds | q quit[/]");

        return new Rows(header, table, BuildDailyChart(), keys);
    }

    private IRenderable BuildDailyChart()
    {
        if (_dailyEnergyKwh.Count == 0)
        {
            return new Markup("[grey]Energy per day: no step yet[/]"); // spectre bar chart crashes without items
        }

        var dailyChart = new BarChart().Label("[bold]Energy per day (kWh)[/]").Width(60);
        foreach (var day in _dailyEnergyKwh)
        {
            dailyChart.AddItem(day.Key.ToString("MM-dd"), Math.Round(day.Value, 1), Color.Yellow);
        }

        return dailyChart;
    }

    private string ManualCloudsHint()
    {
        if (_weatherSource.Mode == WeatherMode.Manual)
        {
            return $"  [yellow]manual {_weatherSource.ManualCloudCoverPercent:F0} %[/]";
        }

        return "";
    }

    // [████████░░░░] 40 % of peak
    private static string PowerBar(PvPlant plant)
    {
        double share = Math.Clamp(plant.PowerKw / plant.Config.PeakPowerKw, 0, 1);
        int filled = (int)Math.Round(share * PowerBarWidth);
        return $"[green]{new string('█', filled)}[/][grey]{new string('░', PowerBarWidth - filled)}[/] {share * 100:F0} %";
    }

    private static string StatusMarkup(PvPlantStatus status)
    {
        switch (status)
        {
            case PvPlantStatus.Producing:
                return "[green]Producing[/]";
            case PvPlantStatus.Limited:
                return "[yellow]Limited[/]";
            case PvPlantStatus.Fault:
                return "[red]FAULT (inverter)[/]";
            case PvPlantStatus.Off:
                return "[grey]Off[/]";
            default:
                return "[blue]Night[/]";
        }
    }

    private static string WeatherText(WeatherSnapshot? weather)
    {
        if (weather == null)
        {
            return "no data yet";
        }

        if (weather.CloudCoverPercent < 20)
        {
            return "sunny";
        }

        if (weather.CloudCoverPercent < 70)
        {
            return "partly cloudy";
        }

        return "overcast";
    }

    private void PrintSummary()
    {
        var plant = _engine.Plant;
        AnsiConsole.MarkupLine($"Stopped at [bold]{_engine.Clock.CurrentTime:yyyy-MM-dd HH:mm}[/], energy total [bold]{plant.TotalEnergyKwh:F2} kWh[/]");

        if (_csvWriter.HasFile)
        {
            AnsiConsole.MarkupLine($"CSV: [bold]{Markup.Escape(_csvWriter.FilePath)}[/]");
            return;
        }

        AnsiConsole.MarkupLine("[grey]no CSV, no step was simulated[/]");
    }
}
