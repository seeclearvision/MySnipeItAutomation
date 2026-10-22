# My SnipeIt Automation TestSuite

Automated UI tests for the [Snipe-IT](https://snipeitapp.com) asset management demo
(<https://demo.snipeitapp.com>), written in C# with Playwright and NUnit.

The tests log in, create an asset (a *Macbook Pro 13"*, status *Ready to Deploy*) checked out to a random
user, find it in the assets list, validate its details page and History tab, modify it, delete it, and log out.
Every step is verified on the page and reported in the console. A negative test checks that a read-only
user (e.g. the demo's `testuser`) is refused access to the assets.

## Versions

| Component | Version |
|---|---|
| .NET | **.NET 10** (`net10.0`, tested with SDK 10.0.402) |
| Playwright | **Microsoft.Playwright.NUnit 1.63.0** (Chromium) |
| NUnit | 4.3.2 (NUnit3TestAdapter 5.0.0, Microsoft.NET.Test.Sdk 17.14.0) |
| PowerShell (optional) | Windows PowerShell 5.1, or PowerShell 7+ on Windows and macOS (tested with 7.6.7) |

## Prerequisites

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and check it with `dotnet --version`.
2. Have a Snipe-IT account. On the public demo, use the credentials shown on the demo's login page:
   - an account that can **create, edit, check out and delete assets** runs the asset tests;
   - a **read-only** account (the demo's `testuser`) runs the negative access test.

You don't need to install anything else. The Playwright Chromium browser is downloaded automatically the
first time the tests run (`PlaywrightInstall.cs`).

## Credentials

**No username or password is stored in this repository.** Pass them to the run script, or set them as
environment variables (handy for CI, where they come from secrets):

| Script option | Environment variable | Meaning |
|---|---|---|
| `-Username USER` | `SNIPEIT_USERNAME` | Snipe-IT username (required) |
| `-Password PASS` | `SNIPEIT_PASSWORD` | Snipe-IT password (required) |
| — | `SNIPEIT_BASE_URL` | Site to test (default `https://demo.snipeitapp.com`) |
| `-Filter FILTER` | — | Run only matching tests, e.g. `"Name~ModifyAsset"` or `"Category=ReadOnly"` |
| `-Headed` | `HEADED=1` | Show the browser while the tests run |

If the username or password is missing, the script stops before running anything.

## Running the tests

All three scripts take the same options.

### Windows – Command Prompt

```bat
cd MySnipeItAutomation
run-tests.cmd -Username USER -Password PASS
run-tests.cmd -Username USER -Password PASS -Filter "Name~ModifyAsset"
run-tests.cmd -Username USER -Password PASS -Headed
```

Put the password in double quotes if it contains `& ^ = ; ,`.

### Windows – PowerShell

```powershell
cd MySnipeItAutomation
.\run-tests.ps1 -Username USER -Password PASS
.\run-tests.ps1 -Username USER -Password PASS -Filter "Name~ModifyAsset"
.\run-tests.ps1 -Username USER -Password PASS -Headed
```

If scripts are blocked, allow them for this session only:
`Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`.

### macOS – Bash (also Linux)

```bash
cd MySnipeItAutomation
./run-tests.sh -Username USER -Password PASS
./run-tests.sh -Username USER -Password PASS -Filter "Name~ModifyAsset"
./run-tests.sh -Username USER -Password PASS -Headed
```

If .NET was installed with Homebrew, the script sets `DOTNET_ROOT` for you. `./run-tests.sh -Help` shows
all options.

### macOS – PowerShell

Install PowerShell 7 once with `brew install powershell`. Then either start `pwsh` and run the script:

```powershell
cd MySnipeItAutomation
./run-tests.ps1 -Username USER -Password PASS
./run-tests.ps1 -Username USER -Password PASS -Filter "Name~ModifyAsset"
./run-tests.ps1 -Username USER -Password PASS -Headed
```

or call it straight from a Bash/zsh terminal:

```bash
pwsh -File ./run-tests.ps1 -Username USER -Password PASS
```

### Without the scripts

```bash
export SNIPEIT_USERNAME=USER SNIPEIT_PASSWORD=PASS
dotnet test
```

On Windows, use `set SNIPEIT_USERNAME=...` or `$env:SNIPEIT_USERNAME = '...'` instead of `export`.

### Results

- Every test is listed as Passed / Failed / Skipped in the console.
- Every step prints what it did and verified, so each test's output shows exactly what it covered, e.g.:

  ```
  Created AUTO-20261022091302538 (id 2631)
  Verified AUTO-20261022091302538 created: name "Automation MBP13 20261022091302538", serial SN20261022091302538, Macbook Pro 13", Ready to Deploy
  Checked out AUTO-20261022091302538 to Clay Adams (yvonrueden) - #5108
  Verified AUTO-20261022091302538 checked out: Deployed to Clay Adams
  Found AUTO-20261022091302538 in the assets list (Automation MBP13 20261022091302538, Macbook Pro 13", Ready to Deploy, Clay Adams) and opened it
  Cleanup: deleted AUTO-20261022091302538 (id 2631)
    Passed FindAsset_InAssetsList [19 s]
  ```

  `Cleanup:` lines come from the teardown that tidies up after each test. Only `DeleteAsset_RemovedFromList`
  deletes as part of the test itself.
- If a test fails, a full-page screenshot is saved under
  `tests/MySnipeIt.Tests/bin/Debug/net10.0/screenshots/` and attached to the test result.

## Test cases

All tests are in `tests/MySnipeIt.Tests/MySnipeIt_Tests.cs` and run as the account you pass in.

Every asset test first creates its own asset and verifies it: the new asset page shows the entered name,
serial, model and *Ready to Deploy*, and after checkout it shows *Deployed* to the chosen user.

| Test | Category | What it checks |
|---|---|---|
| `Login_And_Logout` | — | Runs first. Logs in, checks whether the account can open the asset pages, and logs out |
| `ReadOnlyUser_CannotAccessAssets` | `ReadOnly` | **Negative test:** a read-only account gets *403 Forbidden* on the assets list and the create-asset form |
| `CreateAsset_ReadyToDeploy_CheckedOutToRandomUser` | `Assets` | Creates the asset and checks it out to a random user, verifying both |
| `FindAsset_InAssetsList` | `Assets` | The new asset appears in the assets list with the right name, model, status and user |
| `ValidateAsset_DetailsPage` | `Assets` | The details page shows the tag, name, serial, model, status and assigned user |
| `VerifyHistory_CreateAndCheckout` | `Assets` | The History tab logged the *create new* and *checkout* actions |
| `ModifyAsset_NameAndSerial` | `Assets` | Editing the name and serial is saved, shown on the details page and logged as *update* in History |
| `DeleteAsset_RemovedFromList` | `Assets` | Deleting the asset removes it from the assets list |

Which tests run depends on the account:

| Account | Expected result |
|---|---|
| Can use assets (the demo's asset manager) | `Assets` tests run; the `ReadOnly` test is skipped. **7 passed, 1 skipped** |
| Read-only (the demo's `testuser`) | The `ReadOnly` negative test runs; the `Assets` tests are skipped. **2 passed, 6 skipped** |
| Wrong username or password | `Login_And_Logout` **fails** and every other test is skipped |

A 403 for a read-only account is the expected result, so it counts as a pass, not an error. To run one group,
use `-Filter "Category=Assets"` or `-Filter "Category=ReadOnly"`.

**Note:** the public demo is shared, reset regularly and rate-limited. It sometimes answers *503 Service
Unavailable* or *405 Not Allowed* for a while. A test that fails this way after the first successful login
only fails itself; wait a few minutes and run again.

## Project structure

```
MySnipeItAutomation.slnx            solution
run-tests.cmd / .ps1 / .sh          run scripts for Command Prompt, PowerShell and macOS/Linux
tests/MySnipeIt.Tests/
  MySnipeItTestBase.cs              settings, Login/Logout, Select2 dropdown helpers, screenshots and cleanup
  MySnipeIt_Tests.cs                the test cases (Assets and ReadOnly) and their steps (Create, Find, Validate, History, Modify, Delete)
  PlaywrightInstall.cs              installs the Playwright Chromium build once before any test runs
```

## Best practices used

### Code

- **Readable steps.** Each test reads like the scenario it checks (`Login → CreateAsset → FindAsset → ValidateAsset`).
  Each step is a small, named method with a comment saying what "done" looks like.
- **Shared code lives in one base class.** `MySnipeItTestBase` holds login, logout, settings, the dropdown
  helpers and the failure handling. Test classes contain only test logic.
- **Stable locators.** The tests use ids, roles and visible text, not long CSS paths or screen positions.
  The site's Select2 dropdowns are handled by one helper.
- **No secrets in the code.** Credentials come only from parameters or environment variables, and the
  password is never printed.

### Testing

- **Independent tests.** Each test creates its own uniquely named asset (timestamp plus random number), so
  tests can run alone, in any order, or again without clashing. One test failing doesn't break the others.
- **Always clean up.** A teardown deletes the asset and logs out after every test, even a failed one.
  A cleanup problem is reported as a warning and doesn't hide the real test result.
- **Web-first assertions.** Playwright's `Expect(...)` waits and retries until the page is ready.
  There are no fixed `sleep`s, and the timeout is 10 s because the public demo can be slow.
- **Verify every step.** Each action (create, check out, find, modify, delete) is followed by assertions on
  the page, and the console shows what was verified.
- **Positive and negative tests.** The asset workflow is tested with an account that has permission, and a
  read-only account is tested to make sure it is refused (*403 Forbidden*).
- **Fail fast.** The first test detects bad credentials or missing permissions, and tests that can't apply
  to the account are skipped straight away instead of failing. Once a login has worked, a later one-off site
  error fails only that test.
- **Evidence on failure.** A screenshot is taken before cleanup changes the page.

### Architecture

- **NUnit + Playwright `PageTest`.** Each test gets a fresh, isolated browser context, so no cookies or
  sessions leak between tests.
- **Configuration from the environment.** The same code runs locally, on any OS and in CI. Only the
  environment variables change, following the [twelve-factor](https://12factor.net/config) approach.
- **Same script options on every platform.** `run-tests.cmd`, `run-tests.ps1` and `run-tests.sh`
  behave the same way, so the instructions match on Windows and macOS.
- **Self-contained setup.** The browser installs itself, and `dotnet test` restores the NuGet packages.
  A fresh clone needs only the .NET SDK.

## License

MIT. See [LICENSE](LICENSE).
