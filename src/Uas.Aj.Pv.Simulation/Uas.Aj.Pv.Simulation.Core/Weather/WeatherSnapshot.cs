namespace Uas.Aj.Pv.Simulation.Core.Weather;

// weather at one point 
public record WeatherSnapshot(
    DateTime Time, // simulated time
    double TemperatureCelsius, // ambient, 2m
    double CloudCoverPercent, // 0-100
    double IrradianceWattPerSquareMeter, // shortwave, 0-1000
    WeatherMode Mode = WeatherMode.Auto // manual = clouds set by user
);
