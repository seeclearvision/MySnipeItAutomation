namespace MySnipeIt.Tests;

/// <summary>
/// © 2026 Devanand Bhowan. Licensed under the MIT License (see LICENSE).
/// Ensures the Playwright Chromium build matching the package version is present before any test runs.
/// </summary>
[SetUpFixture]
public sealed class PlaywrightInstall
{
    [OneTimeSetUp]
    public void InstallBrowsers()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);

        if (exitCode != 0)
            throw new InvalidOperationException($"Playwright browser install failed with exit code {exitCode}.");
    }
}
