namespace Uas.Aj.Pv.Simulation.Core.Weather;

// gives weather for any simulated time
public interface IWeatherSource
{
    WeatherSnapshot GetWeather(DateTime time);
}
