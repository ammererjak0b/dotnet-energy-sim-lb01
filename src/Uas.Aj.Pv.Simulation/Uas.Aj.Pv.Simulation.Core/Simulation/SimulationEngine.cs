using Uas.Aj.Pv.Simulation.Core.Device;
using Uas.Aj.Pv.Simulation.Core.History;
using Uas.Aj.Pv.Simulation.Core.Weather;

namespace Uas.Aj.Pv.Simulation.Core.Simulation;

// couples clock, weather and plant, one Tick = one sim step, no waiting (runner does real time)
public class SimulationEngine
{
    private readonly IWeatherSource _weatherSource;
    private readonly IHistoryWriter _historyWriter;

    public SimulationClock Clock { get; } 
    public PvPlant Plant { get; }
    public WeatherSnapshot? LastWeather { get; private set; } // null before first tick, for display + csv

    public SimulationEngine(SimulationClock clock, PvPlant plant, IWeatherSource weatherSource, IHistoryWriter historyWriter)
    {
        Clock = clock;
        Plant = plant;
        _weatherSource = weatherSource;
        _historyWriter = historyWriter;
    }
   
    // normal run: nothing while paused
    public void Tick()
    {
        if (Clock.IsPaused || Clock.IsFinished)
        {
            return; // paused: no energy, no time
        }

        Step();
    }

    // manual single step (key n), works while paused
    public void StepOnce()
    {
        if (Clock.IsFinished)
        {
            return;
        }

        Step();
    }

    // weather at current time -> plant -> csv row -> clock forward
    private void Step()
    {
        var weather = _weatherSource.GetWeather(Clock.CurrentTime);
        LastWeather = weather;
        Plant.Update(weather, Clock.Step); // power at t counts for t to t + step
        _historyWriter.Write(new HistoryRow(
            weather.Time,
            weather.Mode,
            Plant.Status,
            Plant.PowerKw,
            Plant.EnergyTodayKwh,
            Plant.TotalEnergyKwh,
            Plant.PowerLimitPercent,
            weather.IrradianceWattPerSquareMeter,
            weather.CloudCoverPercent,
            weather.TemperatureCelsius));
        Clock.Advance();
    }
}
