namespace Uas.Aj.Pv.Simulation.Core.History;

// where history rows go, file i/o lives in cli
public interface IHistoryWriter
{
    void Write(HistoryRow row);
}
