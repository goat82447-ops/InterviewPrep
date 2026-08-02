$ErrorActionPreference = 'Stop'
$root  = Split-Path $PSScriptRoot -Parent
$src   = Join-Path $root 'dist\kr7'
$stage = Join-Path $root 'dist\kr7-zip'
$dl    = Join-Path $root 'InterviewPrep\downloads'

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

Copy-Item (Join-Path $src 'kr7.exe') $stage -Force
Copy-Item (Join-Path $src 'appsettings.json') $stage -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $src 'ANSWER_STYLE.md') $stage -Force -ErrorAction SilentlyContinue

$runme = @(
    'kr7 - CLI coding agent',
    '======================',
    '1) Keep all files in this folder together.',
    '2) Add a free AI key. Either set an environment variable:',
    '     setx GROQ_API_KEY "your-free-groq-key"',
    '   (open a NEW terminal after setx), OR create appsettings.Local.json here:',
    '     { "AiProviders": { "ApiKeys": { "groq": "your-free-groq-key" } } }',
    '3) Run it:  kr7   (double-click, or type kr7 in a terminal opened in this folder)',
    '4) Type a project description, pick a language; it builds + fixes + offers to run.',
    'Do NOT share appsettings.Local.json - it holds your private API key.'
)
Set-Content -Path (Join-Path $stage 'RUNME.txt') -Value $runme -Encoding UTF8

New-Item -ItemType Directory -Force -Path $dl | Out-Null
$zip = Join-Path $dl 'kr7.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
$stale = Join-Path $dl 'Krishnaagent.exe'
if (Test-Path $stale) { Remove-Item $stale -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force

Write-Host '--- staged files ---'
Get-ChildItem $stage | Select-Object Name, Length | Format-Table -AutoSize
Write-Host '--- downloads ---'
Get-ChildItem $dl | Select-Object Name, Length | Format-Table -AutoSize
