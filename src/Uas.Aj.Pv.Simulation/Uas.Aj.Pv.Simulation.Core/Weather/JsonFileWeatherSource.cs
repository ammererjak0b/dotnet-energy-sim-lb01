using System.Globalization;
using System.Text.Json;
using Uas.Aj.Pv.Simulation.Core.Simulation;

namespace Uas.Aj.Pv.Simulation.Core.Weather;

// reads cached open-meteo json, interpolates hourly values linear
public class JsonFileWeatherSource : IWeatherSource
{
    private readonly List<WeatherSnapshot> hourlySnapshots = new();

    public JsonFileWeatherSource(SimulationOptions options)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(options.WeatherFilePath));
        var hourly = document.RootElement.GetProperty("hourly");
        var times = hourly.GetProperty("time");
        var temperatures = hourly.GetProperty("temperature_2m");
        var clouds = hourly.GetProperty("cloud_cover");
        var irradiances = hourly.GetProperty("shortwave_radiation");

        for (int index = 0; index < times.GetArrayLength(); index++)
        {
            hourlySnapshots.Add(new WeatherSnapshot(
                DateTime.Parse(times[index].GetString()!, CultureInfo.InvariantCulture),
                temperatures[index].GetDouble(),
                clouds[index].GetDouble(),
                irradiances[index].GetDouble()));
        }
    }

    public WeatherSnapshot GetWeather(DateTime time)
    {
        var firstTime = hourlySnapshots[0].Time;
        var endTime = hourlySnapshots[^1].Time.AddHours(1); // last value covers its whole hour

        if (time < firstTime || time >= endTime)
        {
            throw new ArgumentOutOfRangeException(nameof(time), $"no weather data for {time}");
        }

        double hoursSinceStart = (time - firstTime).TotalHours;
        int lowerIndex = (int)Math.Floor(hoursSinceStart);

        if (lowerIndex >= hourlySnapshots.Count - 1)
        {
            return hourlySnapshots[^1] with { Time = time }; // inside last hour, hold value
        }

        var lower = hourlySnapshots[lowerIndex];
        var upper = hourlySnapshots[lowerIndex + 1];
        double fraction = hoursSinceStart - lowerIndex; // 0-1 between the two hours

        return new WeatherSnapshot(
            time,
            Lerp(lower.TemperatureCelsius, upper.TemperatureCelsius, fraction),
            Lerp(lower.CloudCoverPercent, upper.CloudCoverPercent, fraction),
            Lerp(lower.IrradianceWattPerSquareMeter, upper.IrradianceWattPerSquareMeter, fraction));
    }

    private static double Lerp(double from, double to, double fraction)
    {
        return from + (to - from) * fraction;
    }
}
