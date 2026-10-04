using Uas.Aj.Pv.Simulation.Core.Device;
using Uas.Aj.Pv.Simulation.Core.Simulation;
using Uas.Aj.Pv.Simulation.Core.Weather;

namespace Uas.Aj.Pv.Simulation.Cli;

// manual smoke check for PvPlant with real weather, start with --check
internal class PvPlantCheck
{
    private readonly PvPlantConfig _config;
    private readonly IWeatherSource _weatherSource;
    private readonly DateTime _firstDay;
    private readonly TimeSpan _step;
    private int _failedCount;

    public PvPlantCheck(PvPlantConfig config, IWeatherSource weatherSource, SimulationOptions options)
    {
        _config = config;
        _weatherSource = weatherSource;
        _firstDay = options.StartTime.Date;
        _step = options.Step;
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
