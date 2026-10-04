namespace Uas.Aj.Pv.Simulation.Core.Simulation;

// start settings, filled from args later, registered once in DI
public record SimulationOptions(
    DateTime StartTime,
    int Days,
    TimeSpan Step, // simulated time per tick
    double Speed, // 1 = realtime
    string WeatherFilePath
);
