namespace Uas.Aj.Pv.Simulation.Core.Device;

// fixed plant settings, one config per started instance
public record PvPlantConfig(
    string DeviceId,
    double PeakPowerKw, // kWp at 1000 W/m2 and 25 C
    double PerformanceRatio = 0.8 // losses (inverter, cable, dirt) as one factor, 0-1
);
