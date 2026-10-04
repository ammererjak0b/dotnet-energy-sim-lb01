using System.Globalization;
using System.Text.Json;
using Ardalis.GuardClauses;
using Uas.Aj.Pv.Simulation.Core.Simulation;

namespace Uas.Aj.Pv.Simulation.Core.Weather;

// reads cached open-meteo json, interpolates hourly values linear, was downloaded outside of this project so i have it locally here.
// every file problem -> InvalidDataException with path + what is wrong, checked at start (fail fast)
public class JsonFileWeatherSource : IWeatherSource
{
    private readonly List<WeatherSnapshot> _hourlySnapshots = new();
    private readonly string _filePath; // for error messages

    public JsonFileWeatherSource(SimulationOptions options)
    {
        Guard.Against.NullOrWhiteSpace(options.WeatherFilePath, nameof(options.WeatherFilePath));
        _filePath = options.WeatherFilePath;

        using var document = ParseFile();
        LoadHourlySnapshots(document.RootElement);
        CheckCoversSimulation(options.StartTime, options.StartTime.AddDays(options.Days));
    }

    private JsonDocument ParseFile()
    {
        if (!File.Exists(_filePath))
        {
            throw WeatherFileError("file not found (run from repo root?)");
        }

        try
        {
            return JsonDocument.Parse(File.ReadAllText(_filePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw WeatherFileError($"cannot read or parse: {exception.Message}", exception); // no access, broken json
        }
    }

    private void LoadHourlySnapshots(JsonElement root)
    {
        if (!root.TryGetProperty("hourly", out var hourly))
        {
            throw WeatherFileError("field 'hourly' missing");
        }

        var times = GetArray(hourly, "time");
        var temperatures = GetArray(hourly, "temperature_2m");
        var clouds = GetArray(hourly, "cloud_cover");
        var irradiances = GetArray(hourly, "shortwave_radiation");

        int count = times.GetArrayLength();
        if (count == 0)
        {
            throw WeatherFileError("no hourly values");
        }

        if (temperatures.GetArrayLength() != count || clouds.GetArrayLength() != count || irradiances.GetArrayLength() != count)
        {
            throw WeatherFileError("hourly arrays differ in length");
        }

        int index = 0; // position in the other arrays
        foreach (var timeElement in times.EnumerateArray())
        {
            if (!DateTime.TryParse(timeElement.GetString(), CultureInfo.InvariantCulture, out var time))
            {
                throw WeatherFileError($"bad time '{timeElement}' at index {index}");
            }

            _hourlySnapshots.Add(new WeatherSnapshot(
                time,
                GetNumber(temperatures, index, "temperature_2m", time),
                GetNumber(clouds, index, "cloud_cover", time),
                GetNumber(irradiances, index, "shortwave_radiation", time)));
            index++;
        }
    }

    // sim range must lie inside the data, else GetWeather fails mid run
    private void CheckCoversSimulation(DateTime simulationStart, DateTime simulationEnd)
    {
        var firstTime = _hourlySnapshots[0].Time;
        var endTime = _hourlySnapshots[^1].Time.AddHours(1); // last value covers its whole hour

        if (simulationStart < firstTime || simulationEnd > endTime)
        {
            throw WeatherFileError($"data covers {firstTime:yyyy-MM-dd HH:mm} to {endTime:yyyy-MM-dd HH:mm}, simulation needs {simulationStart:yyyy-MM-dd HH:mm} to {simulationEnd:yyyy-MM-dd HH:mm}");
        }
    }

    private JsonElement GetArray(JsonElement hourly, string name)
    {
        if (!hourly.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            throw WeatherFileError($"field 'hourly.{name}' missing or not an array");
        }

        return array;
    }

    private double GetNumber(JsonElement array, int index, string name, DateTime time)
    {
        var element = array[index];
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw WeatherFileError($"'{name}' has no value at {time:yyyy-MM-dd HH:mm}"); // open-meteo writes null for missing values
        }

        return element.GetDouble();
    }

    private InvalidDataException WeatherFileError(string problem, Exception? innerException = null)
    {
        return new InvalidDataException($"weather file '{_filePath}': {problem}", innerException);
    }

    public WeatherSnapshot GetWeather(DateTime time)
    {
        var firstTime = _hourlySnapshots[0].Time;
        var endTime = _hourlySnapshots[^1].Time.AddHours(1); // last value covers its whole hour

        if (time < firstTime || time >= endTime)
        {
            throw new ArgumentOutOfRangeException(nameof(time), $"no weather data for {time}"); // bug if hit, range checked at start
        }

        double hoursSinceStart = (time - firstTime).TotalHours;
        int lowerIndex = (int)Math.Floor(hoursSinceStart);

        if (lowerIndex >= _hourlySnapshots.Count - 1)
        {
            return _hourlySnapshots[^1] with { Time = time }; // inside last hour, hold value
        }

        var lower = _hourlySnapshots[lowerIndex];
        var upper = _hourlySnapshots[lowerIndex + 1];
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
