using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace MySnipeIt.Tests;

/// <summary>
/// © 2026 Devanand Bhowan. Licensed under the MIT License (see LICENSE).
/// Shared settings and the Login / Logout steps used by every test.
/// </summary>
public abstract class MySnipeItTestBase : PageTest
{
    // Settings from environment variables
    // Credentials are never stored in the code. Set them in the environment, or pass them to
    // run-tests.cmd / run-tests.ps1 / run-tests.sh with -Username USER -Password PASS:
    //   SNIPEIT_USERNAME / SNIPEIT_PASSWORD - the Snipe-IT account to test with
    protected static string BaseUrl => Environment.GetEnvironmentVariable("SNIPEIT_BASE_URL") ?? "https://demo.snipeitapp.com";
    protected static string Username => RequiredEnv("SNIPEIT_USERNAME");
    protected static string Password => RequiredEnv("SNIPEIT_PASSWORD");

    private static string RequiredEnv(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Environment variable {name} is not set. Set it, or run the tests with run-tests.cmd, run-tests.ps1 or run-tests.sh.");

    // The public demo can be slow, so give every Expect(...) up to 10 seconds (default is 5).
    [SetUp]
    public void UseLongerExpectTimeout() => SetDefaultExpectTimeout(10_000);

    // Every test gets a fresh browser page, so it always starts logged out
    // (even if the previous test's logout failed and left LoggedIn set).
    [SetUp]
    public void StartLoggedOut() => LoggedIn = false;

    /// <summary>True between a successful Login and Logout (used by cleanup).</summary>
    protected bool LoggedIn { get; private set; }

    // After every test
    // - If the test failed, save a screenshot of the page *before* cleanup navigates away.
    // - Clean up (test classes can add their own steps by overriding Cleanup).
    // - A cleanup problem is reported as a WARNING instead of failing a test whose steps passed.
    [TearDown]
    public async Task AfterEachTest()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            var file = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screenshots", $"{TestContext.CurrentContext.Test.Name}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await Page.ScreenshotAsync(new() { Path = file, FullPage = true });
            TestContext.AddTestAttachment(file);
            TestContext.Progress.WriteLine($"Screenshot of the failure: {file}");
        }

        try
        {
            await Cleanup();
        }
        catch (Exception e)
        {
            TestContext.Progress.WriteLine($"WARNING: cleanup after {TestContext.CurrentContext.Test.Name} failed: {e.Message.Split('\n')[0]}");
        }
    }

    /// <summary>Runs after every test, even a failed one. By default: log out if logged in.</summary>
    protected virtual async Task Cleanup()
    {
        if (LoggedIn)
            await Logout();
    }

    /// <summary>Logs in with the account from SNIPEIT_USERNAME / SNIPEIT_PASSWORD.</summary>
    protected Task Login() => Login(Username, Password);

    protected async Task Login(string username, string password)
    {
        await Page.GotoAsync($"{BaseUrl}/login");

        await Page.Locator("#username").FillAsync(username);
        await Page.Locator("#password-field").FillAsync(password);
        await Page.Locator("#submit").ClickAsync();

        // Logged in = we left the login page and the account menu (top right) is shown.
        await Expect(Page).Not.ToHaveURLAsync(new Regex("/login"));
        await Expect(Page.Locator("li.dropdown.user")).ToBeVisibleAsync();
        LoggedIn = true;
    }

    protected async Task Logout()
    {
        // The account menu (top right) shows the logged-in user's name and holds the Logout link.
        await Page.Locator("li.dropdown.user > .dropdown-toggle").ClickAsync();
        await Page.Locator("li.dropdown.user").GetByRole(AriaRole.Link, new() { Name = "Logout" }).ClickAsync();

        // Logged out = the login form is shown and the account menu is gone.
        // (Don't check the exact URL: the site sometimes lands on "/" instead of "/login".)
        await Expect(Page.Locator("#username")).ToBeVisibleAsync();
        await Expect(Page.Locator("li.dropdown.user")).ToHaveCountAsync(0);
        LoggedIn = false;
    }

    // Select2 dropdown helpers
    // Snipe-IT uses "Select2" dropdowns: the real select element (with the given id) is hidden and
    // the clickable widget is the element right after it. Open and click an option in the open list.

    private ILocator OpenOptions => Page.Locator(
        ".select2-container--open .select2-results__option" +
        ":not(.loading-results):not(.select2-results__message):not(.select2-results__option--load-more)");

    protected async Task SelectOption(string selectId, string optionText, bool typeToSearch = false)
    {
        await Page.Locator($"#{selectId} + .select2-container").ClickAsync();
        if (typeToSearch)
            await Page.Locator(".select2-container--open .select2-search__field").FillAsync(optionText);
        await OpenOptions.Filter(new() { HasText = optionText }).First.ClickAsync();
    }

    /// <summary>Picks a random option and returns (id, label) of what was chosen.</summary>
    protected async Task<(string Id, string Label)> SelectRandomOption(string selectId)
    {
        await Page.Locator($"#{selectId} + .select2-container").ClickAsync();
        await Expect(OpenOptions.First).ToBeVisibleAsync();
        await OpenOptions.Nth(Random.Shared.Next(await OpenOptions.CountAsync())).ClickAsync();

        var id = await Page.Locator($"#{selectId}").InputValueAsync();
        var label = await Page.Locator($"#select2-{selectId}-container").GetAttributeAsync("title") ?? "";
        return (id, label);
    }
}
