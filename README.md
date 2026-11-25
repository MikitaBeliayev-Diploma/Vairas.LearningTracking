# Vairas Learning Tracking

Owner of learning programmes, student training progress, confirmed practical/theory evidence, journal records,
readiness, completion and document-source facts (ASP.NET Core 10, PostgreSQL, EF Core/Npgsql). It consumes
Scheduling and School events and publishes learning events through an outbox. School owns tenancy; Scheduling owns
lessons; Documents renders certificates from Learning's authoritative facts.

## Projects

| Project | Responsibility |
|---|---|
| `Vairas.LearningTracking` | HTTP API: controllers, `Configuration/Configure*`, security, owner clients, workers |
| `Vairas.LearningTracking.Business` | Learning service, ports and contracts |
| `Vairas.LearningTracking.Domain` | Training entities and rules |
| `Vairas.LearningTracking.Persistance` | `LearningDbContext`, migrations, stores, outbox |
| `Vairas.LearningTracking.Tests` | NUnit tests (PostgreSQL via Testcontainers; multi-service HTTP scenarios) |

## Dependencies

PostgreSQL 17, Authentication, User Management, School Management, Scheduling; Service Bus for the outbox/inbox
(emulator locally). Local origin `https://localhost:7500`.

## Essential configuration

Options are bound and validated at startup (`Configuration/ConfigureOptions.cs`). Use `__` in environment variables.
Outside Testing the runtime database role must be `v_learning_tracking_runtime` and dependency origins must be HTTPS.

| Section | Notes |
|---|---|
| `ConnectionStrings:LearningTracking` | restricted runtime role |
| `ConnectionStrings:Delivery` | separate worker role; required when `ServiceBus:Enabled` or `Reconciliation:Enabled` |
| `Authentication` | HTTPS `Authority`/`Issuer`, audiences, `WorkloadClients` allowlist |
| `Dependencies` | HTTPS owner origins; workload `ClientId`/`ClientSecret`/`Scope` |
| `ChecklistFile`, `Checklist` | external, verified checklist policies; `SyntheticMode` only in Development/Testing |
| `ServiceBus`, `Reconciliation` | disabled integrations need no credentials |

## Run locally

```powershell
dotnet run --project Vairas.LearningTracking --launch-profile https
```

The complete local stack is started from `../local/Start.ps1`. Database roles and grants: `database/`;
migrations are applied by the migration role, never on startup.

## Tests

```powershell
dotnet test Vairas.LearningTracking.slnx        # Docker required; broker tests also need a Service Bus emulator
./scripts/verify.ps1 -SkipIntegration           # unit tests with the coverage gate
./scripts/test-broker.ps1                       # Service Bus emulator tests
```

Contract notes: [CONTRACT-NOTES.md](CONTRACT-NOTES.md). API exports: `api/`. Extended notes, policy documentation,
validation reports and evidence: `../../Documentation/Backend/Vairas.LearningTracking/`.

## Tests and coverage

Requirements: .NET SDK 10 and Docker (PostgreSQL integration tests start a throw-away container via Testcontainers).

```powershell
dotnet test Vairas.LearningTracking.slnx                  # unit + integration tests
dotnet test Vairas.LearningTracking.slnx --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory TestResults   # measure coverage
```

Broker tests (category `Broker`) need the Azure Service Bus emulator (Docker, Microsoft EULA): run `./scripts/test-broker.ps1 -AcceptMicrosoftEula` or set `VAIRAS_TEST_SERVICEBUS_CONNECTION`; exclude them with `--filter "TestCategory!=Broker"`.

**Coverage report:** [docs/coverage/README.md](docs/coverage/README.md) (summary, per-assembly table, least covered files); machine-readable `docs/coverage/coverage-summary.json` and `docs/coverage/index.html` (per-file table). The raw cobertura XML of a local run is written to `TestResults/`.