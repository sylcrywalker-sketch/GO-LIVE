param([Parameter(Mandatory = $true)][string]$Model, [string]$Gpu = 'max')
# Unloads every LM Studio model, measures idle VRAM, loads one model (4096 context, like the baseline) and
# measures again. Prints the lines the report uses for resource comparison.
$lms = 'C:\Users\delus\.lmstudio\bin\lms.exe'
function Used { [int](nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits).Trim() }
& $lms unload --all 2>&1 | Out-Null
Start-Sleep -Seconds 3
$idle = Used
$estimate = & $lms load $Model --estimate-only -c 4096 --gpu $Gpu -y 2>&1 | Out-String
$started = Get-Date
& $lms load $Model -c 4096 --gpu $Gpu -y 2>&1 | Select-Object -Last 3
$loadSeconds = ((Get-Date) - $started).TotalSeconds
Start-Sleep -Seconds 2
$loaded = Used
$proc = Get-Process llama-server -ErrorAction SilentlyContinue | Measure-Object WorkingSet64 -Sum
"MODEL $Model"
"VRAM idle=${idle}MiB loaded=${loaded}MiB delta=$($loaded - $idle)MiB"
"llama-server RAM working set=$([int]($proc.Sum / 1MB))MiB  load=$([int]$loadSeconds)s"
"ESTIMATE " + ($estimate -replace "`r?`n", ' | ')
& $lms ps 2>&1 | Select-Object -Last 3
