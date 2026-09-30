# Entwicklungsjournal – Jakob Ammerer

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
- KI: Claude Code Sonnet 5.0: Used /grill-me skill to discuss the project structure with claude. Used parts of it, gave clear instructions on how i want the structure to look like. 

- Artefact: `src/Uas.Aj.Pv.Simulation/` (solution, `Uas.Aj.Pv.Simulation.Core`, `Uas.Aj.Pv.Simulation.Cli`), `.claude/DECISIONS.md`, `.gitignore`, this journal.

## 09/30/26:
- Done:
    - Research about good Console Applications and what to use => came to Spectre.Console: https://spectreconsole.net/ 
    - Research about what a PV does and what would make sense to simulate
    - Json File with wheater data for 1 week, made a class which inherits form IWeatherService for specific wheater from json wheater implementation

- KI: Claude Sonnet 5 => research about what the PV's does and what worth simulating

- Artefact: `data/salzburg-2026-09-23_2026-09-29.json`, `Core/Weather/` (`WeatherSnapshot`, `IWeatherSource`, `JsonFileWeatherSource`), `Core/Simulation/` (`SimulationClock`, `SimulationOptions`), `Cli/Program.cs`.

