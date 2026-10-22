<#
.SYNOPSIS
    © 2026 Devanand Bhowan. Licensed under the MIT License (see LICENSE).
    Runs the Snipe-IT Playwright tests (Windows PowerShell 5.1 or PowerShell 7 on any OS).
    # (Same options as run-tests.sh and run-tests.cmd)

.PARAMETER Username
    Snipe-IT username. Defaults to $env:SNIPEIT_USERNAME.
    The account must be allowed to create, edit, check out and delete assets.

.PARAMETER Password
    Snipe-IT password. Defaults to $env:SNIPEIT_PASSWORD.
    Credentials are never stored in this repo: pass them or set the environment variables.

.EXAMPLE
    ./run-tests.ps1 -Username USER -Password PASS                            # run all tests
    ./run-tests.ps1 -Username USER -Password PASS -Filter "Name~ModifyAsset" # run tests matching a filter
    ./run-tests.ps1 -Username USER -Password PASS -Headed                    # visible browser
#>
param(
    [string] $Username,
    [string] $Password,
    [string] $Filter,
    [switch] $Headed
)

$ErrorActionPreference = 'Stop'

# Parameters override the environment variables (for this process only).
if ($Username) { $env:SNIPEIT_USERNAME = $Username }
if ($Password) { $env:SNIPEIT_PASSWORD = $Password }
if (-not $env:SNIPEIT_BASE_URL) { $env:SNIPEIT_BASE_URL = 'https://demo.snipeitapp.com' }

if (-not $env:SNIPEIT_USERNAME -or -not $env:SNIPEIT_PASSWORD) {
    Write-Error 'Username and password are required: use -Username USER -Password PASS, or set SNIPEIT_USERNAME and SNIPEIT_PASSWORD.' -ErrorAction Continue
    exit 2
}

if ($Headed) {
    $env:HEADED = '1'
}

Set-Location $PSScriptRoot

Write-Host "Snipe-IT: $env:SNIPEIT_BASE_URL  (account: $env:SNIPEIT_USERNAME)"

$testArgs = @('test', '--tl:off', '--logger', 'console;verbosity=normal')

if ($Filter) {
    $testArgs += @('--filter', $Filter)
}

& dotnet @testArgs

exit $LASTEXITCODE
