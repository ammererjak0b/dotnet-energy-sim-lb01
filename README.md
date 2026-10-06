# PV Plant Simulator (LB01)

Console simulator of a PV plant: power from real Salzburg weather data, manual and automatic mode, CSV history per run.

## Requirements

- .NET 10 SDK
- JetBrains Rider (or any terminal with `dotnet`)
- Start from the repo root, the weather file path `data/...` is relative to it

## Start in Rider

The run profiles come from `src/Uas.Aj.Pv.Simulation/Uas.Aj.Pv.Simulation.Cli/Properties/launchSettings.json`. Pick one in the run configuration dropdown (top right), then Run or Debug.

| Profile | Arguments | Use it when |
| --- | --- | --- |
| `Simulation manual` | none | **Manual debugging.** Starts paused with manual clouds. Step tick by tick with `n`, set breakpoints (e.g. `PvPlant.Update`, `SimulationEngine.Step`) and watch every value change. |
| `Simulation auto (2500x)` | `--speed 2500` | **Automatic run.** Starts at once, clouds drift randomly around the real weather. A full week takes ~4 min. Use it for a full CSV and screenshots. |
| `PvPlant check` | `--check` | Runs the model checks (PASS/FAIL per case), no simulation. Run after every change. |
| `Help` | `--help` | Prints all options, defaults and keys. |

Notes:

- Use the profiles above, not Rider's default config `Uas.Aj.Pv.Simulation.Cli`. That one starts in `bin/` and does not find the weather file.
- Keys do not react in Rider's run window? Enable **Use external console** in the run configuration.

## Start in a terminal

```bash
cd dotnet-energy-sim-lb01
dotnet run --project src/Uas.Aj.Pv.Simulation/Uas.Aj.Pv.Simulation.Cli                                  # manual mode
dotnet run --project src/Uas.Aj.Pv.Simulation/Uas.Aj.Pv.Simulation.Cli -- --speed 2500                   # automatic mode
dotnet run --project src/Uas.Aj.Pv.Simulation/Uas.Aj.Pv.Simulation.Cli -- --peak-kw 10 --days 1 --speed 2500  # own parameters
dotnet run --project src/Uas.Aj.Pv.Simulation/Uas.Aj.Pv.Simulation.Cli -- --help                         # all options
```

No options = manual mode, any option = automatic mode.

## Options (automatic mode)

Every option is `--name value`. Leave an option out to use its default. Passing at least one option starts the automatic mode, so `--speed 600` alone gives an automatic run with all other defaults.

| Option | Default | Meaning |
| --- | --- | --- |
| `--device-id` | `pv-01` | Name of the plant, also used in the CSV file name. Start the simulator several times with different IDs. |
| `--peak-kw` | `5` | Nominal power in kWp: output at full sun (1000 W/m²) and 25 °C cell temperature. |
| `--performance-ratio` | `0.8` | All losses (inverter, cables, dirt) as one factor, 0-1. `0.8` = 80 % of the ideal output. |
| `--start` | `2026-09-23` | First simulated day, format `yyyy-MM-dd`. Must lie inside the weather file. |
| `--days` | `7` | Number of simulated days. Start + days must fit into the weather file (here 2026-09-23 to 2026-09-29). |
| `--step` | `15` | Simulated minutes per tick = one CSV line. Smaller = more detail, more lines. |
| `--speed` | `600` | How many times faster than real time. Can be changed while running with `+` / `-`. |
| `--weather` | `data/salzburg-2026-09-23_2026-09-29.json` | Weather file (Open-Meteo, hourly temperature, clouds, irradiance). |
| `--history-folder` | `data/runs` | Folder for the CSV files. Created if missing. |

Numbers use `.` as decimal point (`--peak-kw 7.5`). A wrong option or value stops the program with a message and exit code 1, e.g. `unknown option --peek-kw` or `Required input Days cannot be zero or negative.`

Examples:

```bash
--speed 2500                                  # full week, fast, all defaults
--peak-kw 10 --days 1 --speed 1000            # bigger plant, one day
--device-id roof-west --peak-kw 3.2 --step 5  # second plant, finer steps, own CSV name
--start 2026-09-25 --days 2                   # only 25th and 26th September
```

## Keys

| Key | Action |
| --- | --- |
| `space` | run / pause |
| `n` | one step (while paused) |
| `+` / `-` | speed up / down (1, 10, 60, 600, 1000, 2500x) |
| `o` | plant on / off |
| `f` | trigger / reset inverter fault |
| `up` / `down` | power limit +-10 % |
| `m` | weather auto / manual |
| `left` / `right` | manual clouds -+10 % |
| `q` | quit, CSV stays complete |

All keys work in both modes, only the start state differs.

## Output

Every run writes `data/runs/<device-id>_<start time>.csv`, one line per tick:

```
time;mode;status;power_kw;energy_today_kwh;energy_total_kwh;power_limit_percent;irradiance_w_m2;cloud_cover_percent;temperature_c
```

`;` as separator and decimal comma, so it opens directly in LibreOffice / Excel (German). Fault, on/off, power limit and manual clouds are visible in the `status`, `power_limit_percent`, `mode` and `cloud_cover_percent` columns.
