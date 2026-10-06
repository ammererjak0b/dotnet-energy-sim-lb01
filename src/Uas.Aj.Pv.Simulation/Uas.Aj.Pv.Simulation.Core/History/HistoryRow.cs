using Uas.Aj.Pv.Simulation.Core.Device;
using Uas.Aj.Pv.Simulation.Core.Weather;

namespace Uas.Aj.Pv.Simulation.Core.History;

// one tick in the run history = one csv line
public record HistoryRow(
    DateTime Time, // start of the tick
    WeatherMode Mode,
    PvPlantStatus Status,
    double PowerKw,
    double EnergyTodayKwh,
    double EnergyTotalKwh,
    double PowerLimitPercent,
    double IrradianceWattPerSquareMeter,
    double CloudCoverPercent,
    double TemperatureCelsius
);
