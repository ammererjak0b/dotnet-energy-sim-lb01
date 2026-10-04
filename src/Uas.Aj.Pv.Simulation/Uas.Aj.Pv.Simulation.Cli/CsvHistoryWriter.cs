using System.Globalization;
using Uas.Aj.Pv.Simulation.Core.Device;
using Uas.Aj.Pv.Simulation.Core.History;
using Uas.Aj.Pv.Simulation.Core.Simulation;

namespace Uas.Aj.Pv.Simulation.Cli;

// one csv line per tick, ; + decimal comma -> opens direct in libre office / excel (german)
internal sealed class CsvHistoryWriter : IHistoryWriter, IDisposable
{
    private const string Header = "time;mode;status;power_kw;energy_today_kwh;energy_total_kwh;power_limit_percent;irradiance_w_m2;cloud_cover_percent;temperature_c";
    private static readonly CultureInfo GermanNumbers = CultureInfo.GetCultureInfo("de-AT"); // 2,44 instead of 2.44

    private StreamWriter? _writer; // opened on first row -> no empty files from --check or failed starts

    public string FilePath { get; }

    public CsvHistoryWriter(SimulationOptions options, PvPlantConfig config)
    {
        string fileName = $"{config.DeviceId}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv"; // device id + real start time -> runs dont overwrite
        FilePath = Path.Combine(options.HistoryFolderPath, fileName);
    }

    public void Write(HistoryRow row)
    {
        try
        {
            var writer = _writer ?? Open();
            writer.WriteLine(FormatRow(row));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"cannot write history file '{FilePath}': {exception.Message}", exception);
        }
    }

    private StreamWriter Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        _writer = new StreamWriter(FilePath);
        _writer.AutoFlush = true; // every row on disk at once, crash keeps data so far
        _writer.WriteLine(Header);
        return _writer;
    }

    private static string FormatRow(HistoryRow row)
    {
        return string.Join(';',
            row.Time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            row.Mode,
            row.Status,
            FormatNumber(row.PowerKw),
            FormatNumber(row.EnergyTodayKwh),
            FormatNumber(row.EnergyTotalKwh),
            FormatNumber(row.PowerLimitPercent),
            FormatNumber(row.IrradianceWattPerSquareMeter),
            FormatNumber(row.CloudCoverPercent),
            FormatNumber(row.TemperatureCelsius));
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.###", GermanNumbers); // max 3 decimals, no thousands dot
    }

    // closes the file, called by DI when the provider is disposed
    public void Dispose()
    {
        _writer?.Dispose();
    }
}
