using Uas.Aj.Pv.Simulation.Core.Device;
using Uas.Aj.Pv.Simulation.Core.History;
using Uas.Aj.Pv.Simulation.Core.Simulation;
using Uas.Aj.Pv.Simulation.Core.Weather;

namespace Uas.Aj.Pv.Simulation.Cli;

// manual smoke check for PvPlant with real weather, start with --check
internal class PvPlantCheck
{
    private readonly PvPlantConfig _config;
    private readonly JsonFileWeatherSource _weatherSource; // real data, no random -> same result every run
    private readonly DateTime _firstDay;
    private readonly TimeSpan _step;
    private readonly SimulationOptions _options; // fresh clock per engine case
    private int _failedCount;

    public PvPlantCheck(PvPlantConfig config, JsonFileWeatherSource weatherSource, SimulationOptions options)
    {
        _config = config;
        _weatherSource = weatherSource;
        _firstDay = options.StartTime.Date;
        _step = options.Step;
        _options = options;
    }

    // runs all cases, exit code 0 = all pass
    public int Run()
    {
        Console.WriteLine($"PvPlant check | {_config.DeviceId} | {_config.PeakPowerKw} kWp | day {_firstDay:yyyy-MM-dd} | step {_step.TotalMinutes} min");
        Console.WriteLine();

        CheckNight();
        CheckNoon();
        CheckPowerLimit();
        CheckFault();
        CheckOnOff();
        CheckFullDay();
        CheckDayReset();
        CheckGuards();
        CheckEngineDay();
        CheckEnginePause();
        CheckEngineRunToEnd();
        CheckManualWeather();
        CheckAutoDrift();
        CheckHistoryRows();
        CheckCsvFile();

        Console.WriteLine();
        if (_failedCount == 0)
        {
            Console.WriteLine("all passed");
            return 0;
        }

        Console.WriteLine($"{_failedCount} failed");
        return 1;
    }

    private void CheckNight()
    {
        var plant = new PvPlant(_config);
        UpdateAt(plant, _firstDay.AddHours(2));

        Report("night 02:00 -> 0 kW, Night", plant.PowerKw == 0 && plant.Status == PvPlantStatus.Night, plant);
    }

    private void CheckNoon()
    {
        var plant = new PvPlant(_config);
        UpdateAt(plant, _firstDay.AddHours(12));

        bool isPlausible = plant.PowerKw > 0 && plant.PowerKw <= _config.PeakPowerKw; // never above nameplate
        Report("noon -> 0 < P <= peak, Producing", isPlausible && plant.Status == PvPlantStatus.Producing, plant);
    }

    private void CheckPowerLimit()
    {
        var plant = new PvPlant(_config);
        plant.SetPowerLimit(10); // low enough to always bind at noon
        UpdateAt(plant, _firstDay.AddHours(12));

        double limitKw = _config.PeakPowerKw * 0.1;
        Report("limit 10 % -> P = 10 % peak, Limited", Math.Abs(plant.PowerKw - limitKw) < 0.001 && plant.Status == PvPlantStatus.Limited, plant);

        plant.SetPowerLimit(100);
        UpdateAt(plant, _firstDay.AddHours(12));
        Report("limit back to 100 % -> Producing", plant.Status == PvPlantStatus.Producing, plant);
    }

    private void CheckFault()
    {
        var plant = new PvPlant(_config);
        plant.TriggerFault();
        Report("fault (no tick yet) -> status Fault at once", plant.Status == PvPlantStatus.Fault, plant);

        UpdateAt(plant, _firstDay.AddHours(12));
        Report("fault at noon -> 0 kW, Fault", plant.PowerKw == 0 && plant.Status == PvPlantStatus.Fault, plant);

        plant.ResetFault();
        UpdateAt(plant, _firstDay.AddHours(12));
        Report("reset fault at noon -> P > 0, Producing", plant.PowerKw > 0 && plant.Status == PvPlantStatus.Producing, plant);
    }

    private void CheckOnOff()
    {
        var plant = new PvPlant(_config);
        plant.TurnOff();
        UpdateAt(plant, _firstDay.AddHours(12));
        Report("off at noon -> 0 kW, Off", plant.PowerKw == 0 && plant.Status == PvPlantStatus.Off, plant);

        plant.TurnOn();
        UpdateAt(plant, _firstDay.AddHours(12));
        Report("on again at noon -> P > 0", plant.PowerKw > 0, plant);
    }

    private void CheckFullDay()
    {
        var plant = new PvPlant(_config);
        RunDay(plant, _firstDay);

        double maxPossibleKwh = _config.PeakPowerKw * 24; // full sun all day, upper bound
        bool isPlausible = plant.EnergyTodayKwh > 0 && plant.EnergyTodayKwh < maxPossibleKwh;
        Report("full day -> 0 < energy < peak * 24 h", isPlausible && plant.EnergyTodayKwh == plant.TotalEnergyKwh, plant);
    }

    private void CheckDayReset()
    {
        var plant = new PvPlant(_config);
        RunDay(plant, _firstDay);
        double totalAfterFirstDay = plant.TotalEnergyKwh;

        UpdateAt(plant, _firstDay.AddDays(1)); // midnight, next day
        Report("next day 00:00 -> today reset, total kept", plant.EnergyTodayKwh == 0 && plant.TotalEnergyKwh == totalAfterFirstDay, plant);
    }

    private void CheckGuards()
    {
        Report("peak 0 kW -> throws", Throws(() => new PvPlant(_config with { PeakPowerKw = 0 })), null);
        Report("empty device id -> throws", Throws(() => new PvPlant(_config with { DeviceId = " " })), null);
        Report("limit 150 % -> throws", Throws(() => new PvPlant(_config).SetPowerLimit(150)), null);
    }

    private void CheckEngineDay()
    {
        var engine = CreateEngine(new MemoryHistoryWriter());
        int ticksPerDay = (int)(TimeSpan.FromDays(1) / _step);
        foreach (var _ in Enumerable.Range(0, ticksPerDay))
        {
            engine.Tick();
        }

        bool isNextDay = engine.Clock.CurrentTime == _firstDay.AddDays(1);
        bool isLastWeatherLastStep = engine.LastWeather?.Time == _firstDay.AddDays(1) - _step; // weather of last tick, not of now
        Report("engine 1 day of ticks -> clock next day 00:00", isNextDay && isLastWeatherLastStep && engine.Plant.TotalEnergyKwh > 0, engine.Plant);
    }

    private void CheckEnginePause()
    {
        var engine = CreateEngine(new MemoryHistoryWriter());
        foreach (var _ in Enumerable.Range(0, 48)) // to noon, sun is up
        {
            engine.Tick();
        }

        var timeBefore = engine.Clock.CurrentTime;
        double energyBefore = engine.Plant.TotalEnergyKwh;
        engine.Clock.Pause();
        engine.Tick();
        engine.Tick();
        Report("engine paused -> no time, no energy", engine.Clock.CurrentTime == timeBefore && engine.Plant.TotalEnergyKwh == energyBefore, engine.Plant);

        engine.Clock.Resume();
        engine.Tick();
        Report("engine resumed -> time + energy go on", engine.Clock.CurrentTime > timeBefore && engine.Plant.TotalEnergyKwh > energyBefore, engine.Plant);
    }

    private void CheckEngineRunToEnd()
    {
        var engine = CreateEngine(new MemoryHistoryWriter());
        while (!engine.Clock.IsFinished)
        {
            engine.Tick(); // whole range, would throw if weather missing
        }

        var endTime = engine.Clock.CurrentTime;
        engine.Tick(); // after end -> nothing
        Report($"engine full run ({_options.Days} days) -> ends at end time", endTime == engine.Clock.EndTime && engine.Clock.CurrentTime == endTime, engine.Plant);
    }

    private void CheckManualWeather()
    {
        var weather = new AdjustableWeatherSource(_weatherSource);
        var noon = _firstDay.AddHours(12);
        var night = _firstDay.AddHours(2);
        var realNoon = _weatherSource.GetWeather(noon);

        weather.SetManual(100);
        var overcast = weather.GetWeather(noon);
        Report("manual 100 % clouds -> less sun than real", overcast.IrradianceWattPerSquareMeter < realNoon.IrradianceWattPerSquareMeter && overcast.CloudCoverPercent == 100, null);

        weather.SetManual(0);
        var clear = weather.GetWeather(noon);
        Report("manual 0 % clouds -> more sun, <= 1100 W/m2", clear.IrradianceWattPerSquareMeter >= realNoon.IrradianceWattPerSquareMeter && clear.IrradianceWattPerSquareMeter <= 1100, null);
        Report("manual 0 % clouds at night -> still 0 W/m2", weather.GetWeather(night).IrradianceWattPerSquareMeter == 0, null);
        Report("manual 150 % -> throws", Throws(() => weather.SetManual(150)), null);

        Console.WriteLine($"      real noon {realNoon.CloudCoverPercent:F0} % {realNoon.IrradianceWattPerSquareMeter:F0} W/m2 | manual 100 %: {overcast.IrradianceWattPerSquareMeter:F0} W/m2 | manual 0 %: {clear.IrradianceWattPerSquareMeter:F0} W/m2");
    }

    private void CheckAutoDrift()
    {
        var weather = new AdjustableWeatherSource(_weatherSource);
        var engine = new SimulationEngine(new SimulationClock(_options), new PvPlant(_config), weather, new MemoryHistoryWriter());
        bool isInRange = true;
        bool hasDrifted = false;
        double maxDifference = 0;

        while (!engine.Clock.IsFinished)
        {
            engine.Tick();
            var real = _weatherSource.GetWeather(engine.LastWeather!.Time);
            double difference = Math.Abs(engine.LastWeather.CloudCoverPercent - real.CloudCoverPercent);
            maxDifference = Math.Max(maxDifference, difference);

            if (engine.LastWeather.CloudCoverPercent < 0 || engine.LastWeather.CloudCoverPercent > 100 || difference > 50)
            {
                isInRange = false;
            }

            if (difference > 0)
            {
                hasDrifted = true;
            }
        }

        Report($"auto full run -> clouds drift (max {maxDifference:F0} % off real), 0-100", isInRange && hasDrifted, engine.Plant);
    }

    private void CheckHistoryRows()
    {
        var historyWriter = new MemoryHistoryWriter();
        var weather = new AdjustableWeatherSource(_weatherSource);
        var engine = new SimulationEngine(new SimulationClock(_options), new PvPlant(_config), weather, historyWriter);
        foreach (var _ in Enumerable.Range(0, 48)) // to noon
        {
            engine.Tick();
        }

        engine.Plant.TriggerFault();
        weather.SetManual(100);
        engine.Tick();
        engine.Plant.ResetFault();
        weather.SetAuto();
        engine.Tick();

        var rows = historyWriter.Rows;
        Report("history 1 row per tick, first row = start time", rows.Count == 50 && rows[0].Time == _firstDay, null);
        Report("history fault + manual visible in row", rows[48].Status == PvPlantStatus.Fault && rows[48].Mode == WeatherMode.Manual, null);
        Report("history reset + auto visible in next row", rows[49].Status == PvPlantStatus.Producing && rows[49].Mode == WeatherMode.Auto, null);
    }

    private void CheckCsvFile()
    {
        var csvOptions = _options with { HistoryFolderPath = Path.Combine(Path.GetTempPath(), "pv-check") }; // not into data/runs
        var csvWriter = new CsvHistoryWriter(csvOptions, _config);
        var engine = CreateEngine(csvWriter);
        int ticksPerDay = (int)(TimeSpan.FromDays(1) / _step);
        foreach (var _ in Enumerable.Range(0, ticksPerDay))
        {
            engine.Tick();
        }

        csvWriter.Dispose();
        var lines = File.ReadAllLines(csvWriter.FilePath);
        File.Delete(csvWriter.FilePath);

        bool hasHeader = lines[0].StartsWith("time;mode;status;power_kw");
        bool hasAllColumns = lines.All(line => line.Split(';').Length == 10);
        string noonLine = lines[1 + ticksPerDay / 2];
        Report("csv header + 1 line per tick, 10 columns", hasHeader && lines.Length == ticksPerDay + 1 && hasAllColumns, null);
        Report("csv decimal comma (libre office de)", noonLine.Contains(','), null);
        Console.WriteLine($"      noon line: {noonLine}");
    }

    private SimulationEngine CreateEngine(IHistoryWriter historyWriter)
    {
        return new SimulationEngine(new SimulationClock(_options), new PvPlant(_config), _weatherSource, historyWriter);
    }

    // keeps rows in memory, for checks without files
    private sealed class MemoryHistoryWriter : IHistoryWriter
    {
        public List<HistoryRow> Rows { get; } = new();

        public void Write(HistoryRow row)
        {
            Rows.Add(row);
        }
    }

    private void RunDay(PvPlant plant, DateTime day)
    {
        var time = day;
        while (time < day.AddDays(1))
        {
            UpdateAt(plant, time);
            time += _step;
        }
    }

    private void UpdateAt(PvPlant plant, DateTime time)
    {
        plant.Update(_weatherSource.GetWeather(time), _step);
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return true; // guard clauses throw ArgumentException or subtypes
        }

        return false;
    }

    private void Report(string caseName, bool passed, PvPlant? plant)
    {
        string result = "PASS";
        if (!passed)
        {
            result = "FAIL";
            _failedCount++;
        }

        string values = "";
        if (plant != null)
        {
            values = $"{plant.PowerKw,6:F2} kW  {plant.EnergyTodayKwh,6:F2} kWh today  {plant.Status}";
        }

        Console.WriteLine($"{result}  {caseName,-45} {values}");
    }
}
