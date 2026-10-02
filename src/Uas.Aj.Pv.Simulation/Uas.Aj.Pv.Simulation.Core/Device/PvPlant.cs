using Ardalis.GuardClauses;
using Uas.Aj.Pv.Simulation.Core.Weather;

namespace Uas.Aj.Pv.Simulation.Core.Device;

// pv plant model: weather in, power and energy out
public class PvPlant
{
    private const double CellHeatingKPerWattPerSquareMeter = 0.025; // cell warms ~25 K at 1000 W/m2
    private const double PowerLossPerKelvin = 0.004; // -0.4 % per K above 25 C
    private const double ReferenceCellTemperature = 25;

    private bool _isOn = true;
    private bool _hasFault;
    private bool _isLimited; // limit cut power in last update
    private double _irradiance; // last seen, W/m2
    private DateTime? _currentDay; // for daily energy reset

    public PvPlantConfig Config { get; }
    public double PowerLimitPercent { get; private set; } = 100;
    public double PowerKw { get; private set; }
    public double EnergyTodayKwh { get; private set; }
    public double TotalEnergyKwh { get; private set; }

    public PvPlantStatus Status
    {
        get
        {
            if (_hasFault)
            {
                return PvPlantStatus.Fault;
            }

            if (!_isOn)
            {
                return PvPlantStatus.Off;
            }

            if (_irradiance <= 0)
            {
                return PvPlantStatus.Night;
            }

            if (_isLimited)
            {
                return PvPlantStatus.Limited;
            }

            return PvPlantStatus.Producing;
        }
    }

    public PvPlant(PvPlantConfig config)
    {
        Guard.Against.NullOrWhiteSpace(config.DeviceId, nameof(config.DeviceId));
        Guard.Against.NegativeOrZero(config.PeakPowerKw, nameof(config.PeakPowerKw));
        Guard.Against.OutOfRange(config.PerformanceRatio, nameof(config.PerformanceRatio), 0.01, 1);

        Config = config;
    }

    // one tick: new power from weather, add energy for the step
    public void Update(WeatherSnapshot weather, TimeSpan step)
    {
        Guard.Against.NegativeOrZero(step, nameof(step));

        if (_currentDay != weather.Time.Date)
        {
            EnergyTodayKwh = 0; // new day
            _currentDay = weather.Time.Date;
        }

        _irradiance = weather.IrradianceWattPerSquareMeter;
        PowerKw = CalculatePowerKw(weather);

        double energyKwh = PowerKw * step.TotalHours;
        EnergyTodayKwh += energyKwh;
        TotalEnergyKwh += energyKwh;
    }

    public void TurnOn()
    {
        _isOn = true;
    }

    public void TurnOff()
    {
        _isOn = false;
    }

    public void TriggerFault()
    {
        _hasFault = true;
    }

    public void ResetFault()
    {
        _hasFault = false;
    }

    public void SetPowerLimit(double percent)
    {
        Guard.Against.OutOfRange(percent, nameof(percent), 0, 100);

        PowerLimitPercent = percent;
    }

    private double CalculatePowerKw(WeatherSnapshot weather)
    {
        _isLimited = false;

        if (!_isOn || _hasFault)
        {
            return 0;
        }

        double cellTemperature = weather.TemperatureCelsius + CellHeatingKPerWattPerSquareMeter * weather.IrradianceWattPerSquareMeter;
        double temperatureFactor = 1 - PowerLossPerKelvin * (cellTemperature - ReferenceCellTemperature);
        double freePowerKw = Config.PeakPowerKw * (weather.IrradianceWattPerSquareMeter / 1000) * temperatureFactor * Config.PerformanceRatio;
        freePowerKw = Math.Max(0, freePowerKw); // no negative power

        double limitKw = Config.PeakPowerKw * PowerLimitPercent / 100;
        _isLimited = freePowerKw > limitKw;

        return Math.Min(freePowerKw, limitKw);
    }
}
