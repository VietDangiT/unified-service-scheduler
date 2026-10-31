<#
.SYNOPSIS
    Mutation check for the booking rules.

.DESCRIPTION
    Injects known bugs, one at a time, into a temporary copy of the repository and runs the unit tests.
    KILLED   = at least one test failed: the tests protect that rule.
    SURVIVED = every test still passed: the tests would miss that bug.
    INVALID  = the mutation did not compile, so it proves nothing (fix the mutation).
    The repository itself is never modified. Exits with code 1 unless every mutation is killed.
    Add a mutation whenever you add a business rule (see .claude/rules/testing.md).

.EXAMPLE
    pwsh tools/mutation/mutate.ps1
#>
param([string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path)

$ErrorActionPreference = "Stop"

$Root = Join-Path ([IO.Path]::GetTempPath()) "service-scheduler-mutation"
if (Test-Path $Root) { Remove-Item -Recurse -Force $Root }
robocopy $Repo $Root /E /XD bin obj .git .claude /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying the repository to $Root failed (robocopy exit $LASTEXITCODE)." }

$V = "src\ServiceScheduler.Domain\Scheduling\BookingValidator.cs"
$W = "src\ServiceScheduler.Domain\Dealerships\WeeklySchedule.cs"
$T = "src\ServiceScheduler.Domain\Scheduling\TimeSlot.cs"
$L = "src\ServiceScheduler.Domain\Dealerships\LocalTimeRange.cs"
$R = "src\ServiceScheduler.Domain\Common\Result.cs"
$A = "src\ServiceScheduler.Domain\Scheduling\SlotAllocator.cs"
$BP = "src\ServiceScheduler.Domain\Scheduling\BestFitBayPolicy.cs"
$TP = "src\ServiceScheduler.Domain\Scheduling\BestFitTechnicianPolicy.cs"
$D = "src\ServiceScheduler.Domain\Dealerships\Dealership.cs"
$SB = "src\ServiceScheduler.Domain\Dealerships\ServiceBay.cs"
$TC = "src\ServiceScheduler.Domain\Dealerships\Technician.cs"
$BT = "src\ServiceScheduler.Domain\Dealerships\BayType.cs"

$ownerBlock = "        if (request.Vehicle.OwnerId != request.CustomerId)`n        {`n            return Result.Failure<TimeSlot>(BookingErrors.VehicleNotOwnedByCustomer);`n        }`n`n"
$offeredBlock = "        if (!IsOffered(request.Dealership, request.ServiceType))`n        {`n            return Result.Failure<TimeSlot>(BookingErrors.ServiceNotOfferedAtDealership);`n        }`n`n"
$pastBlock = "        if (request.RequestedStart <= now)`n        {`n            return Result.Failure<TimeSlot>(BookingErrors.StartInPast);`n        }`n`n"
$gridBlock = "        if (!IsOnGrid(request.RequestedStart, request.Dealership.TimeZone))`n        {`n            return Result.Failure<TimeSlot>(BookingErrors.StartNotOnGrid);`n        }`n`n"
$noTechnicianBlock = "        if (technician is null)`n        {`n            return Result.Failure<Allocation>(BookingErrors.NoQualifiedTechnicianAvailable);`n        }`n`n"
$noBayBlock = "        if (bay is null)`n        {`n            return Result.Failure<Allocation>(BookingErrors.NoCompatibleBayAvailable);`n        }`n`n"

# Each mutation is one plausible bug. Old/New may be arrays for multi-part edits;
# wrap concatenations in parentheses inside @(...), or the comma splits them.
$mutations = @(
    # Validation order and rules (BookingValidator)
    @{ Name = "Owner check removed";                    File = $V; Old = "if (request.Vehicle.OwnerId != request.CustomerId)"; New = "if (request.Vehicle.OwnerId != request.Vehicle.OwnerId)" }
    @{ Name = "Owner and past checks swapped";          File = $V; Old = ($ownerBlock + $offeredBlock + $pastBlock); New = ($pastBlock + $offeredBlock + $ownerBlock) }
    @{ Name = "Start == now accepted (<= to <)";        File = $V; Old = "request.RequestedStart <= now"; New = "request.RequestedStart < now" }
    @{ Name = "Same-day bookings rejected";             File = $V; Old = "request.RequestedStart <= now"; New = "request.RequestedStart.Date <= now.Date" }
    @{ Name = "Past compared on typed wall clock";      File = $V; Old = "request.RequestedStart <= now"; New = "request.RequestedStart.DateTime <= now.DateTime" }
    @{ Name = "Grid check moved after opening hours";   File = $V; Old = @($gridBlock, "        return Result.Success(slot);"); New = @("", ($gridBlock + "        return Result.Success(slot);")) }
    @{ Name = "Grid ignores seconds/milliseconds";      File = $V; Old = "TimeOfDay.Ticks % SlotGranularity.Ticks == 0"; New = "TimeOfDay.Minutes % 15 == 0" }
    @{ Name = "Hourly grid only";                       File = $V; Old = "TimeOfDay.Ticks % SlotGranularity.Ticks == 0"; New = "TimeOfDay.Ticks % TimeSpan.FromHours(1).Ticks == 0" }
    @{ Name = "Grid checked on request's own offset";   File = $V; Old = "TimeZoneInfo.ConvertTime(start, zone).TimeOfDay"; New = "start.TimeOfDay" }
    # Opening hours (WeeklySchedule)
    @{ Name = "Opening hours compared in UTC";          File = $W; Old = @("TimeZoneInfo.ConvertTime(slot.StartUtc, zone)", "TimeZoneInfo.ConvertTime(slot.EndUtc, zone)"); New = @("slot.StartUtc", "slot.EndUtc") }
    @{ Name = "Weekday taken from UTC";                 File = $W; Old = "_days.TryGetValue(localStart.DayOfWeek"; New = "_days.TryGetValue(slot.StartUtc.DayOfWeek" }
    @{ Name = "Saturday treated as closed";             File = $W; Old = "|| !_days.TryGetValue("; New = "|| localStart.DayOfWeek == DayOfWeek.Saturday || !_days.TryGetValue(" }
    @{ Name = "Ending exactly at closing rejected";     File = $W; Old = "TimeOnly.FromTimeSpan(localEnd.TimeOfDay) <= range.End"; New = "TimeOnly.FromTimeSpan(localEnd.TimeOfDay) < range.End" }
    @{ Name = "Starting exactly at opening rejected";   File = $W; Old = "range.Start <= TimeOnly.FromTimeSpan(localStart.TimeOfDay)"; New = "range.Start < TimeOnly.FromTimeSpan(localStart.TimeOfDay)" }
    @{ Name = "Cross-midnight check removed";           File = $W; Old = "localStart.Date != localEnd.Date || "; New = "" }
    @{ Name = "Local-midnight check uses UTC dates";    File = $W; Old = "localStart.Date != localEnd.Date"; New = "slot.StartUtc.Date != slot.EndUtc.Date" }
    # Value objects and Result
    @{ Name = "Back-to-back slots overlap (< to <=)";   File = $T; Old = "StartUtc < other.EndUtc && other.StartUtc < EndUtc"; New = "StartUtc <= other.EndUtc && other.StartUtc <= EndUtc" }
    @{ Name = "Slot not normalised to UTC";             File = $T; Old = "var startUtc = start.ToUniversalTime();"; New = "var startUtc = start;" }
    @{ Name = "Non-positive duration accepted";         File = $T; Old = "ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);"; New = "" }
    @{ Name = "Empty opening range accepted";           File = $L; Old = "if (end <= start)"; New = "if (end < start)" }
    @{ Name = "Result.Success accepts null";            File = $R; Old = "ArgumentNullException.ThrowIfNull(value);"; New = "" }
    @{ Name = "Result.Failure accepts null";            File = $R; Old = "ArgumentNullException.ThrowIfNull(error);"; New = "" }
    # Allocation (step 3): technician and bay eligibility
    @{ Name = "Technician busy intervals ignored";      File = $A; Old = "busy.TechnicianId == technician.Id && busy.Blocked.Overlaps(request.Slot)"; New = "false" }
    @{ Name = "Bay busy intervals ignored";             File = $A; Old = "busy.BayId == bay.Id && busy.Blocked.Overlaps(request.Slot)"; New = "false" }
    @{ Name = "Shift check tests only the start";       File = $TC; Old = "Shifts.Covers(slot, zone)"; New = "Shifts.Covers(TimeSlot.Create(slot.StartUtc, TimeSpan.FromMinutes(1)), zone)" }
    @{ Name = "Standard job requires exact Standard bay"; File = $SB; Old = "required == BayType.Standard || Type == required"; New = "Type == required" }
    @{ Name = "Specialised job accepts any bay type";   File = $SB; Old = "required == BayType.Standard || Type == required"; New = "true" }
    @{ Name = "Inactive filter removed";                File = $A; Old = @("technician.IsActive`n                && ", "bay.IsActive`n            && "); New = @("", "") }
    @{ Name = "Bay error returned when both missing";   File = $A; Old = ($noTechnicianBlock + $noBayBlock); New = ($noBayBlock + $noTechnicianBlock) }
    # Allocation policies: best fit and workload
    @{ Name = "Bay order EV before Lift";               File = $BT; Old = "    Lift,`n    Alignment,`n    EV,"; New = "    EV,`n    Lift,`n    Alignment," }
    @{ Name = "Workload compared before skill count";   File = $TP; Old = ".OrderBy(technician => technician.Skills.Count)`n            .ThenBy(technician => workload.GetValueOrDefault(technician.Id))"; New = ".OrderBy(technician => workload.GetValueOrDefault(technician.Id))`n            .ThenBy(technician => technician.Skills.Count)" }
    @{ Name = "Workload uses the UTC date";             File = $A; Old = @("TimeZoneInfo.ConvertTime(request.Slot.StartUtc, zone).Date", "TimeZoneInfo.ConvertTime(busy.Blocked.StartUtc, zone).Date"); New = @("request.Slot.StartUtc.Date", "busy.Blocked.StartUtc.Date") }
    @{ Name = "ID tie-break reversed";                  File = $TP; Old = ".ThenBy(technician => technician.Id.Value)"; New = ".ThenByDescending(technician => technician.Id.Value)" }
    @{ Name = "ID tie-break reversed (bay)";            File = $BP; Old = ".ThenBy(bay => bay.Id.Value)"; New = ".ThenByDescending(bay => bay.Id.Value)" }
    # Dealership aggregate and rule 3
    @{ Name = "Dealership accepts foreign resource";    File = $D; Old = @("technician.DealershipId != id", "bay.DealershipId != id"); New = @("false", "false") }
    @{ Name = "Rule 3 after StartInPast";               File = $V; Old = ($offeredBlock + $pastBlock); New = ($pastBlock + $offeredBlock) }
    @{ Name = "Rule 3 counts inactive resources";       File = $V; Old = @("technician.IsActive && ", "bay.IsActive && "); New = @("", "") }
    # Specialised jobs stay bookable (EC-71)
    @{ Name = "Bay serves only Standard jobs";          File = $SB; Old = "required == BayType.Standard || Type == required"; New = "required == BayType.Standard" }
    @{ Name = "Rule 3 rejects every non-Standard job";  File = $V; Old = "dealership.Technicians.Any("; New = "serviceType.RequiredBayType == BayType.Standard && dealership.Technicians.Any(" }
    @{ Name = "Rule 3 requires all technicians";        File = $V; Old = "dealership.Technicians.Any("; New = "dealership.Technicians.All(" }
    @{ Name = "Rule 3 requires all bays";               File = $V; Old = "dealership.Bays.Any("; New = "dealership.Bays.All(" }
)

$tests = Join-Path $Root "tests\ServiceScheduler.UnitTests"
$results = @()

foreach ($m in $mutations) {
    $path = Join-Path $Root $m.File
    $original = [IO.File]::ReadAllText($path)
    $text = $original -replace "`r`n", "`n"
    $olds = @($m.Old); $news = @($m.New)
    try {
        for ($i = 0; $i -lt $olds.Count; $i++) {
            if (-not $text.Contains($olds[$i])) { throw "pattern not found (update the mutation): $($olds[$i])" }
            $text = $text.Replace($olds[$i], $news[$i])
        }
        [IO.File]::WriteAllText($path, $text)
        dotnet build $tests -nologo -v q -p:TreatWarningsAsErrors=false 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { $status = "INVALID"; $detail = "does not compile" }
        else {
            $out = dotnet test $tests --no-build -nologo 2>&1 | Out-String
            $failed = [regex]::Matches($out, "(?m)^\s+Failed (\S+)") |
                ForEach-Object { ($_.Groups[1].Value -split '\.')[-1] -replace '\(.*$', '' } | Sort-Object -Unique
            if ($LASTEXITCODE -ne 0) { $status = "KILLED"; $detail = ($failed | Select-Object -First 2) -join ", " }
            else { $status = "SURVIVED"; $detail = "no test failed" }
        }
    }
    catch { $status = "ERROR"; $detail = $_.Exception.Message }
    finally { [IO.File]::WriteAllText($path, $original) }
    $results += [pscustomobject]@{ Mutation = $m.Name; Result = $status; CaughtBy = $detail }
}

$results | Format-Table -AutoSize -Wrap | Out-String -Width 220
$killed = @($results | Where-Object Result -eq "KILLED").Count
"Killed $killed / $($results.Count)"
if ($killed -ne $results.Count) { exit 1 }
