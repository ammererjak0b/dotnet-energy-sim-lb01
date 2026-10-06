using Ardalis.GuardClauses;

namespace Uas.Aj.Pv.Simulation.Core.Simulation;

// simulated time, fixed step per tick, speed only changes the real wait between ticks
public class SimulationClock
{
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }
    public DateTime CurrentTime { get; private set; }
    public TimeSpan Step { get; } // simulated time per tick
    public double Speed { get; private set; } // 1 = realtime, 600 = 600x
    public bool IsPaused { get; private set; }
    public bool IsFinished => CurrentTime >= EndTime;
    public TimeSpan RealTimePerTick => Step / Speed; // how long engine waits between ticks

    public SimulationClock(SimulationOptions options)
    {
        Guard.Against.NegativeOrZero(options.Days, nameof(options.Days));
        Guard.Against.NegativeOrZero(options.Step, nameof(options.Step));

        StartTime = options.StartTime;
        EndTime = options.StartTime.AddDays(options.Days);
        CurrentTime = options.StartTime;
        Step = options.Step;
        SetSpeed(options.Speed);
    }

    // one tick forward, does nothing after the end
    public void Advance()
    {
        if (IsFinished) // pause is checked by the engine, so single steps work while paused
        {
            return;
        }

        CurrentTime += Step;
    }

    public void Pause()
    {
        IsPaused = true;
    }

    public void Resume()
    {
        IsPaused = false;
    }

    public void SetSpeed(double speed)
    {
        Guard.Against.NegativeOrZero(speed, nameof(speed));

        Speed = speed;
    }
}
