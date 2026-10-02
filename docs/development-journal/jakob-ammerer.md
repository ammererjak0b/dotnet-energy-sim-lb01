27;2;13~# Entwicklungsjournal – Jakob Ammerer

<!--
Verpflichtende Tags pro Eintrag:

- Done: Was wurde bearbeitet und welches Ergebnis liegt vor?
- KI: Werkzeug, Modell, Einsatzform und Umgang mit dem Ergebnis; bei keiner KI-Nutzung: keine.
- Artefact: Betroffene Dateien, CSV/Kurve, Screenshot, Dokumentation oder andere Ergebnisse.

Optionale Tags bei Relevanz:
- Comment: Entscheidung, Problem, Erkenntnis oder nächster Schritt.
- Test: Durchgeführter manueller oder automatisierter Test.
-->

## 09/28/26: 
- Done: initial creation of repository. Project Structure Setup (Class lib & Cli setup)
- KI: Claude Code Sonnet 5.0: Used /grill-me skill to discuss the project structure with claude. Used parts of it, gave clear instructions on how i want the structure to look like. in .claude Decisions.md are design decisions i discussed with claude. The artefacts section in this files is also written by claude cuz im lazy. 

- Artefact: `src/Uas.Aj.Pv.Simulation/` (solution, `Uas.Aj.Pv.Simulation.Core`, `Uas.Aj.Pv.Simulation.Cli`), `.claude/DECISIONS.md`, `.gitignore`, this journal.

## 09/30/26:
- Done:
    - Research about good Console Applications and what to use => came to Spectre.Console: https://spectreconsole.net/ 
    - Research about what a PV does and what would make sense to simulate
    - Json File with wheater data for 1 week, made a class which inherits form IWeatherService for specific wheater from json wheater implementation

- KI: Claude Sonnet 5 => research about what the PV's does and what worth simulating

- Artefact: `data/salzburg-2026-09-23_2026-09-29.json`, `Core/Weather/` (`WeatherSnapshot`, `IWeatherSource`, `JsonFileWeatherSource`), `Core/Simulation/` (`SimulationClock`, `SimulationOptions`), `Cli/Program.cs`.

## 03/10/26:

- Done: PV plant model (power from irradiance (bestrahlung) + temperature, power limit, fault, on/off, daily/total energy), --check for  manual testing, debug profile

- KI: Claude Sonnet 5 => formatting console output, ideas and information on the pvplant. Helping me to figure out how a PV plant model should work. Used grill-me skill on that specific thing. 

- Artefacts: `Core/Device/` (`PvPlant.cs`, `PvPlantConfig.cs`, `PvPlantStatus.cs`), `Cli/PvPlantCheck.cs`, `Cli/Properties/launchSettings.json`, `Cli/Program.cs`
