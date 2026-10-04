namespace Uas.Aj.Pv.Simulation.Core.Weather;

// who controls the clouds
public enum WeatherMode
{
    Auto, // real data + random cloud drift
    Manual // cloud cover fixed by user
}
