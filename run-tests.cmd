@echo off

rem © 2026 Devanand Bhowan. Licensed under the MIT License (see LICENSE).

rem Runs the Snipe-IT Playwright tests on Windows (Command Prompt).
rem (Same options as run-tests.ps1 and run-tests.sh)
rem
rem   run-tests.cmd -Username USER -Password PASS                            run all tests
rem   run-tests.cmd -Username USER -Password PASS -Filter "Name~ModifyAsset" run tests matching a filter
rem   run-tests.cmd -Username USER -Password PASS -Headed                    visible browser
rem
rem Options (case-insensitive):
rem   -Username USER   Snipe-IT username (else SNIPEIT_USERNAME)
rem   -Password PASS   Snipe-IT password (else SNIPEIT_PASSWORD)
rem   -Filter FILTER   dotnet test filter, e.g. "Name~ModifyAsset"
rem   -Headed          show the browser
rem
rem The account must be allowed to create, edit, check out and delete assets.
rem Credentials are never stored in this repo: pass them or set the environment variables.

setlocal

set "FILTER="

:parse
if "%~1"=="" goto run
if /i "%~1"=="-Username" goto username
if /i "%~1"=="-Password" goto password
if /i "%~1"=="-Filter"   goto filter
if /i "%~1"=="-Headed"   goto headed
echo Unknown option: %~1 1>&2
exit /b 2

:username
if "%~2"=="" (echo Missing value for %~1 1>&2 & exit /b 2)
set "SNIPEIT_USERNAME=%~2"
shift
shift
goto parse

:password
if "%~2"=="" (echo Missing value for %~1 1>&2 & exit /b 2)
set "SNIPEIT_PASSWORD=%~2"
shift
shift
goto parse

:filter
if "%~2"=="" (echo Missing value for %~1 1>&2 & exit /b 2)
set "FILTER=%~2"
shift
shift
goto parse

:headed
set "HEADED=1"
shift
goto parse

:run
if not defined SNIPEIT_BASE_URL  set "SNIPEIT_BASE_URL=https://demo.snipeitapp.com"
if not defined SNIPEIT_USERNAME goto missing
if not defined SNIPEIT_PASSWORD goto missing

cd /d "%~dp0"

echo Snipe-IT: %SNIPEIT_BASE_URL%  (account: %SNIPEIT_USERNAME%)

if not defined FILTER (
    dotnet test --tl:off --logger "console;verbosity=normal"
) else (
    dotnet test --tl:off --filter "%FILTER%" --logger "console;verbosity=normal"
)

exit /b %ERRORLEVEL%

:missing
echo Username and password are required: use -Username USER -Password PASS, or set SNIPEIT_USERNAME and SNIPEIT_PASSWORD. 1>&2
exit /b 2
