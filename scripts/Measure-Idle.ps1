#requires -Version 7.0
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$clock = [Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath (Resolve-Path -LiteralPath $Executable).Path -ArgumentList '--diagnostics-sample' -PassThru -WindowStyle Hidden
try {
    if (!$process.WaitForInputIdle(10000)) { throw 'O processo demorou a iniciar.' }
    $ready = $clock.ElapsedMilliseconds
    Start-Sleep -Seconds 3
    $process.Refresh()
    $initial = $process.TotalProcessorTime.TotalMilliseconds
    $start = $clock.ElapsedMilliseconds
    $samples = foreach ($i in 1..12) {
        Start-Sleep -Milliseconds 900
        $process.Refresh()
        if ($process.HasExited) { throw 'O processo encerrou antes da medição.' }
        [pscustomobject]@{Working=$process.WorkingSet64; Private=$process.PrivateMemorySize64; Handles=$process.HandleCount}
    }
    $cpu = $process.TotalProcessorTime.TotalMilliseconds - $initial
    $elapsed = $clock.ElapsedMilliseconds - $start
    if (!$process.WaitForExit(20000) -or $process.ExitCode -ne 0) { throw 'A sessão de diagnóstico falhou.' }
    [pscustomobject]@{
        InputIdleMs = $ready; DurationMs = $elapsed; CpuMs = $cpu; LogicalCpus = [Environment]::ProcessorCount
        WholeMachineCpuPct = [Math]::Round(100*$cpu/$elapsed/[Environment]::ProcessorCount,3)
        WorkingSetMinMiB = [Math]::Round(($samples.Working | Measure-Object -Minimum).Minimum/1MB,1)
        WorkingSetMaxMiB = [Math]::Round(($samples.Working | Measure-Object -Maximum).Maximum/1MB,1)
        PrivateMinMiB = [Math]::Round(($samples.Private | Measure-Object -Minimum).Minimum/1MB,1)
        PrivateMaxMiB = [Math]::Round(($samples.Private | Measure-Object -Maximum).Maximum/1MB,1)
        HandlesFirst = $samples[0].Handles; HandlesLast = $samples[-1].Handles
    }
} finally { if (!$process.HasExited) { $process.Kill() }; $process.Dispose() }
