# Vairas.LearningTracking — code coverage

**Line coverage 84.01 %** (1917/2282 executable lines), branch coverage 61.07 % — threshold 70 % **met**. Generated 2026-11-25.

| Assembly | Line % | Lines | Branch % |
|---|---:|---:|---:|
| Vairas.LearningTracking | 62.03 | 495/798 | 34.43 |
| Vairas.LearningTracking.Business | 95.8 | 1208/1261 | 67.57 |
| Vairas.LearningTracking.Domain | 100.0 | 83/83 | 79.81 |
| Vairas.LearningTracking.Persistance | 93.57 | 131/140 | 88.46 |

## How it was measured

- Tool: coverlet (`XPlat Code Coverage`) driven by `Backend/local/coverage.py`; settings `Vairas.LearningTracking/coverage.runsettings` (migrations and test assemblies excluded).
- Test projects: `Vairas.LearningTracking.Tests`; line hits of all test projects of the service are merged (maximum per line), so shared code is never counted twice.
- Filter: `TestCategory!=Broker`. Broker tests need the Service Bus emulator and are executed (and passed) separately by `local/test_all.py`.
- The numbers are measured line coverage, not a statement about functional completeness; acceptance status is tracked in `Documentation/Final-Verification/AC-Traceability-Matrix.xlsx`.

## Reproduce

```powershell
cd Backend
py -3 local/coverage.py --services Vairas.LearningTracking --filter "TestCategory!=Broker" --threshold 70
```

## Files

`coverage-summary.json` (machine-readable, per file) and `index.html` (per-file table, lowest coverage first) are in this folder.

## Least covered files

| File | Line % | Lines |
|---|---:|---:|
| `Vairas.LearningTracking.Business/ILearningService.cs` | 0.0 | 0/1 |
| `Vairas.LearningTracking.Business/NotificationSourceService.cs` | 0.0 | 0/10 |
| `Security/LessonActions.cs` | 0.0 | 0/61 |
| `Controllers/NotificationSourceController.cs` | 0.0 | 0/2 |
| `Configuration/Options/HttpsOrigin.cs` | 0.0 | 0/1 |
| `Vairas.LearningTracking.Persistance/DesignTimeFactory.cs` | 0.0 | 0/2 |
| `Security/ReadPreviewDelegation.cs` | 8.75 | 7/80 |
| `Integrations/InboxWorker.cs` | 10.64 | 5/47 |
