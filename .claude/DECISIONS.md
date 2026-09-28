# Architecture decisions

## 2026-09-28 – Project structure (Core/Console split)

- **Decision:** Two projects, `Uas.Aj.Pv.Simulation.Core` (device model, simulation clock, weather, history/CSV row + `IHistoryWriter` interface) and `Uas.Aj.Pv.Simulation.Cli` (Program.cs, DI composition root, console rendering, keyboard input, CSV file I/O implementing `IHistoryWriter`).
- **Why:** wanted DI + testable core logic without inventing layers (Domain/Application/Infrastructure) that have nothing to abstract yet at WarmUp scale. PDF explicitly warns against building the full later system prematurely.
- **DI:** bare `Microsoft.Extensions.DependencyInjection` (`ServiceCollection` + `BuildServiceProvider`), no `Microsoft.Extensions.Hosting` — single console loop doesn't need config/logging/lifetime infrastructure.
- **Core folders:** by concern (`Device/`, `Simulation/`, `Weather/`, `History/`), namespaces mirror folders — matches .NET convention, scales per-device for the semester project.
- **Tests:** deferred. Rubric doesn't require them and there isn't enough behavior yet at LB01 scale to make unit tests pay off. Revisit at LB02 when Core grows.
- **CLI boundary:** presentation only, no business logic. The one exception (CSV file writing) is mechanical I/O behind an interface Core defines, not a decision — keeps Core testable without a full Infrastructure project.
