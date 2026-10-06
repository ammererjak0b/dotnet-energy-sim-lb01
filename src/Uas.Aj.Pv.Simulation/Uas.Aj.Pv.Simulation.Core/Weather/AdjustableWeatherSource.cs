using Ardalis.GuardClauses;

namespace Uas.Aj.Pv.Simulation.Core.Weather;


// Decorater Pattern: https://refactoring.guru/design-patterns/decorator
// wraps real weather: auto -> clouds drift randomly around real data, manual -> clouds set by user
// irradiance rescaled to the new clouds, temperature stays real
public class AdjustableWeatherSource : IWeatherSource
{
    private const double MaxDriftPercentPerHour = 10; // ~ +-5 % per 15 min step
    private const double MaxCloudOffsetPercent = 50; // auto stays near real data
    private const double PullBackHours = 6; // offset fades back to real data over 6 
    private const double MaxIrradianceWattPerSquareMeter = 1100; // clear sky limit at ground

    private readonly JsonFileWeatherSource _realWeatherSource;
    private readonly Random _random = new(); // new values every start, no seed
    private double _cloudOffsetPercent; // auto drift, added to real clouds
    private DateTime? _lastTime; // drift scaled by sim time, not by call count

    public WeatherMode Mode { get; private set; } = WeatherMode.Auto;
    public double ManualCloudCoverPercent { get; private set; }

    public AdjustableWeatherSource(JsonFileWeatherSource realWeatherSource)
    {
        _realWeatherSource = realWeatherSource;
    }

    public void SetAuto()
    {
        Mode = WeatherMode.Auto;
    }

    public void SetManual(double cloudCoverPercent)
    {
        Guard.Against.OutOfRange(cloudCoverPercent, nameof(cloudCoverPercent), 0, 100);

        ManualCloudCoverPercent = cloudCoverPercent;
        Mode = WeatherMode.Manual;
    }

    public WeatherSnapshot GetWeather(DateTime time)
    {
        var realWeather = _realWeatherSource.GetWeather(time);
        Drift(time);

        double cloudCoverPercent = ManualCloudCoverPercent;
        if (Mode == WeatherMode.Auto)
        {
            cloudCoverPercent = Math.Clamp(realWeather.CloudCoverPercent + _cloudOffsetPercent, 0, 100);
        }

        // real irradiance already has real clouds in it -> swap their effect for the new clouds
        double irradiance = realWeather.IrradianceWattPerSquareMeter * CloudFactor(cloudCoverPercent) / CloudFactor(realWeather.CloudCoverPercent);
        irradiance = Math.Min(irradiance, MaxIrradianceWattPerSquareMeter); // 0 % clouds on overcast data can overshoot

        return realWeather with
        {
            CloudCoverPercent = cloudCoverPercent,
            IrradianceWattPerSquareMeter = irradiance,
            Mode = Mode
        };
    }

    // random walk with pull back to real data, scaled by elapsed sim time -> same behaviour for any step width
    private void Drift(DateTime time)
    {
        if (_lastTime != null && time > _lastTime)
        {
            double hours = (time - _lastTime.Value).TotalHours;
            double drift = (_random.NextDouble() * 2 - 1) * MaxDriftPercentPerHour * Math.Sqrt(hours); // -max to +max
            double pullBack = Math.Exp(-hours / PullBackHours); // < 1, shrinks old offset a bit each step
            _cloudOffsetPercent = Math.Clamp(_cloudOffsetPercent * pullBack + drift, -MaxCloudOffsetPercent, MaxCloudOffsetPercent);
        }

        _lastTime = time;
    }

    // share of clear sky irradiance that gets through, Kasten-Czeplak: 1 at 0 %, 0.25 at 100 % 
    private static double CloudFactor(double cloudCoverPercent)
    {
        return 1 - 0.75 * Math.Pow(cloudCoverPercent / 100, 3.4);
    }
}
