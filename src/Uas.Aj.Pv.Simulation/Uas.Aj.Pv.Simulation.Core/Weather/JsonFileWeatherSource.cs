using System.Globalization;
using System.Text.Json;
using Ardalis.GuardClauses;
using Uas.Aj.Pv.Simulation.Core.Simulation;

namespace Uas.Aj.Pv.Simulation.Core.Weather;

// reads cached open-meteo json, interpolates hourly values linear, was downloaded outside of this project so i have it locally here.
public class JsonFileWeatherSource : IWeatherSource
{
    private readonly List<WeatherSnapshot> hourlySnapshots = new();

    public JsonFileWeatherSource(SimulationOptions options)
    {
        Guard.Against.NullOrWhiteSpace(options.WeatherFilePath, nameof(options.WeatherFilePath));

        try
        {
            LoadHourlySnapshots(options.WeatherFilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException or IndexOutOfRangeException)
        {
            // file missing, no access, broken json, missing field, wrong type, bad date, arrays differ in length
            throw new InvalidDataException($"cannot read weather file '{options.WeatherFilePath}': {exception.Message}", exception);
        }

        Guard.Against.NullOrEmpty(hourlySnapshots, nameof(hourlySnapshots));
    }

    private void LoadHourlySnapshots(string filePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        var hourly = document.RootElement.GetProperty("hourly");
        var times = hourly.GetProperty("time");
        var temperatures = hourly.GetProperty("temperature_2m");
        var clouds = hourly.GetProperty("cloud_cover");
        var irradiances = hourly.GetProperty("shortwave_radiation");

        int index = 0; // position in the other arrays
        foreach (var time in times.EnumerateArray())
        {
            hourlySnapshots.Add(new WeatherSnapshot(
                DateTime.Parse(time.GetString()!, CultureInfo.InvariantCulture),
                temperatures[index].GetDouble(),
                clouds[index].GetDouble(),
                irradiances[index].GetDouble()));
            index++;
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
