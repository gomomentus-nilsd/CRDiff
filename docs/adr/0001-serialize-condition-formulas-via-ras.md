# 0001. Serialize conditional formulas (suppress, can grow, ...) via the RAS report definition

## Status

Accepted, 2026-11-05.

Verified against:

| Item | Value |
|---|---|
| Crystal Reports runtime | 13.0.40.5789 (`CrystalReports.Engine` / `ReportAppServer` NuGet 13.0.4003) |
| CRDiff base version | 0.1.0.16 (commit 87707ed plus local uncommitted changes) |
| Compiled with | Roslyn from .NET SDK 10.0.402 against .NET Framework 4.8 reference assemblies (not yet built through the normal VS/MSBuild pipeline) |
| Checked on | 2026-11-05, against 19 historical versions of one customer report (`Leistungsübersicht`, 15 non-trivial subreports/sections) |

Evidence legend: **[VERIFIED]** observed in code, output or a live run; **[INFERRED]** not directly observed.

## Context

CRDiff's JSON is used to compare report versions in git. Conditional formulas on areas, sections and objects (Suppress, Can Grow, Keep Together, New Page Before/After, ...) were not in the JSON, which the tool's own TODO already listed. [VERIFIED] A commit that fixed a missing-notes problem (an `EnableSuppress` formula on the item-notes subreport section) changed the `.rpt` binary but produced zero changes in the JSON, so the fix could not be traced through history. [VERIFIED]

The existing serializer dumps `ReportDocument` (Engine API) objects via reflection. That API does not expose conditional formulas. [VERIFIED] `rptClient.ReportDefController.ReportDefinition` (RAS) does, but its collections and format objects arrive as bare COM objects: `foreach` and `dynamic` member access fail on them. [VERIFIED]

## Decision

Add a separate `ConditionFormulas` array (main report and each subreport) written from the RAS report definition:

- Walk areas, sections and report objects by index (`Count` plus indexer), because the COM collections are not `IEnumerable`.
- Cast each format's `ConditionFormulas` to `ISCRSectionAreaFormatConditionFormulas` (areas, sections) or `ISCRObjectFormatConditionFormulas` (objects) and read every value of the matching `Cr...ConditionFormulaTypeEnum` through the interface's default indexer. Enumerating the enum keeps the condition names out of the code.
- Emit `Kind`, `Name`, `Path`, `Condition`, `Text` for non-empty formulas only.

Existing JSON keys are unchanged; the new array is additive.

## Alternatives considered

- **`dynamic` access plus `foreach`.** Rejected: fails on the COM objects (`InvalidCastException` for `IEnumerable`, `RuntimeBinderException` on members). [VERIFIED]
- **Reflection over `GetInterfaces()` to find the getter.** Rejected: the runtime type is `__ComObject` with no interfaces, so it silently found nothing. [VERIFIED]
- **Hard-coding the list of conditions (suppress, can grow, ...).** Rejected: misses new or rarely used conditions; the enum makes the list complete.
- **Parsing the `.rpt` OLE streams.** Rejected: the streams are binary and the formula text is not readable without the engine. [INFERRED from the streams that were inspected]

## Consequences

- Report diffs now show suppression and layout-condition changes; old JSON files do not contain the array, so diffing an old export against a new one lists every formula as added.
- Field-level format conditions (number, date, font colour, border, ...) are not serialized yet.
- Subreport links and table joins are still not serialized (checked: no link information anywhere in the JSON). [VERIFIED]
- Depends on the RAS interop types of runtime 13.0.x; re-check after a runtime upgrade.
