using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace MySnipeIt.Tests;

/// <summary>
/// © 2026 Devanand Bhowan. Licensed under the MIT License (see LICENSE).
/// Snipe-IT tests, run as the account passed to the run-tests scripts (SNIPEIT_USERNAME / SNIPEIT_PASSWORD).
/// Every asset test creates its own asset, and Cleanup deletes it afterwards (even if the test failed) and logs out.
///
/// Which tests run depends on the account's permissions (checked once, right after the first login):
///   - Account that can use assets (e.g. "assets"):  the "Assets" tests run; the "ReadOnly" negative test is skipped.
///   - Read-only account (e.g. "testuser"):          the "ReadOnly" negative test checks the asset pages return
///                                                   403 Forbidden; the "Assets" tests are skipped.
/// A failed login is a real failure, and every later test is skipped.
/// </summary>
public class MySnipeIt_Tests : MySnipeItTestBase
{
    private const string Assets = "Assets";     // test category: needs an account that can use assets
    private const string ReadOnly = "ReadOnly"; // test category: needs an account that cannot

    private static readonly string[] AssetPages = ["/hardware", "/hardware/create"];

    private const string ModelName = "Macbook Pro 13\"";
    private const string StatusName = "Ready to Deploy";

    // The asset created by the current test.
    private string _assetId = "";
    private string _assetTag = "";
    private string _assetName = "";
    private string _serial = "";
    private string _userName = ""; // the random user it is checked out to, e.g. "Bailey Lowell"
    private bool _deleted;

    // Account checks, done once per run.
    private string? _loginFailed;  // set if the first login failed: every later test is skipped
    private bool _loggedInOnce;    // the credentials are known to work
    private bool? _hasAssetAccess; // null until checked
    private string _noAccessDetail = ""; // e.g. "/hardware returned 403"

    private string AssetUrl => $"{BaseUrl}/hardware/{_assetId}";

    // Test Cases

    [Test, Order(1)] // runs first, so a bad account is detected before any asset is created
    public async Task Login_And_Logout()
    {
        await LoginOrSkipTheRest();
        var hasAssetAccess = await CheckAssetAccess();
        await Logout();

        TestContext.Progress.WriteLine(hasAssetAccess
            ? $"\"{Username}\" can use assets: running the {Assets} tests."
            : $"\"{Username}\" is read-only ({_noAccessDetail}): running the {ReadOnly} test, skipping the {Assets} tests.");
    }

    // Negative test: a read-only account (e.g. testuser) must not be able to see or create assets.
    [Test, Order(2), Category(ReadOnly)]
    public async Task ReadOnlyUser_CannotAccessAssets()
    {
        await LoginOrSkipTheRest();
        if (await CheckAssetAccess())
            Assert.Ignore($"Skipped: \"{Username}\" can use assets; this negative test needs a read-only account such as testuser.");

        var afterLogin = Page.Url;
        foreach (var path in AssetPages)
        {
            var response = await Page.GotoAsync($"{BaseUrl}{path}");
            Assert.That(response?.Status, Is.EqualTo(403), $"{path} should be forbidden for \"{Username}\"");
            await Expect(Page.Locator("body")).ToContainTextAsync("403 Forbidden");
        }
        TestContext.Progress.WriteLine($"As expected, \"{Username}\" gets 403 Forbidden on {string.Join(" and ", AssetPages)}.");

        await Page.GotoAsync(afterLogin); // the 403 page has no account menu, so go back before logging out
        await Logout();
    }

    [Test, Category(Assets)]
    public async Task CreateAsset_ReadyToDeploy_CheckedOutToRandomUser()
    {
        await LoginWithAssetAccess();
        await CreateAsset();
    }

    [Test, Category(Assets)]
    public async Task FindAsset_InAssetsList()
    {
        await LoginWithAssetAccess();
        await CreateAsset();
        await FindAsset();
    }

    [Test, Category(Assets)]
    public async Task ValidateAsset_DetailsPage()
    {
        await LoginWithAssetAccess();
        await CreateAsset();
        await FindAsset();
        await ValidateAsset();
    }

    [Test, Category(Assets)]
    public async Task VerifyHistory_CreateAndCheckout()
    {
        await LoginWithAssetAccess();
        await CreateAsset();
        await FindAsset();
        await VerifyHistory();
    }

    [Test, Category(Assets)]
    public async Task ModifyAsset_NameAndSerial()
    {
        await LoginWithAssetAccess();
        await CreateAsset();
        await ModifyAsset();
    }

    [Test, Category(Assets)]
    public async Task DeleteAsset_RemovedFromList()
    {
        await LoginWithAssetAccess();
        await CreateAsset();
        await DeleteAsset();
    }

    // NUnit reuses one instance for all tests in this class, so start each test with a clean slate.
    [SetUp]
    public void ResetAsset()
    {
        // Skip without even opening the site when the account checks already decided this test can't apply.
        if (_loginFailed != null)
            Assert.Ignore($"Skipped: {_loginFailed}");
        var categories = TestContext.CurrentContext.Test.Properties["Category"];
        if (_hasAssetAccess == false && categories.Contains(Assets))
            Assert.Ignore($"Skipped: \"{Username}\" cannot access the assets ({_noAccessDetail})");
        if (_hasAssetAccess == true && categories.Contains(ReadOnly))
            Assert.Ignore($"Skipped: \"{Username}\" can use assets; this negative test needs a read-only account such as testuser.");

        _assetId = _assetTag = _assetName = _serial = _userName = "";
        _deleted = false;
    }

    // Runs after every test (see MySnipeItTestBase): delete the asset this test created, then log out.
    protected override async Task Cleanup()
    {
        if (_assetId != "" && !_deleted)
            await DeleteAsset(checkRemovedFromList: false);
        await base.Cleanup();
    }

    // Logs in, then skips this test if the account cannot use assets.
    private async Task LoginWithAssetAccess()
    {
        await LoginOrSkipTheRest();
        if (!await CheckAssetAccess())
            Assert.Ignore($"Skipped: \"{Username}\" cannot access the assets ({_noAccessDetail})");
    }

    // A failed login fails this test. If no login has worked yet (bad credentials), the rest are skipped too;
    // after that, a failed login is a one-off site problem and only fails this test.
    private async Task LoginOrSkipTheRest()
    {
        try
        {
            await Login();
            _loggedInOnce = true;
        }
        catch when (!_loggedInOnce)
        {
            _loginFailed = $"could not log in as \"{Username}\"";
            throw;
        }
    }

    // Once per run: can the logged-in account open the assets list and the create-asset form?
    // Returns to the page shown after login, which has the account menu, so Logout still works.
    private async Task<bool> CheckAssetAccess()
    {
        if (_hasAssetAccess is { } known)
            return known;

        var afterLogin = Page.Url;
        _hasAssetAccess = true;
        foreach (var path in AssetPages)
        {
            var response = await Page.GotoAsync($"{BaseUrl}{path}");
            if (response?.Status is not 200)
            {
                _hasAssetAccess = false;
                _noAccessDetail = $"{path} returned {response?.Status}";
                break;
            }
        }
        await Page.GotoAsync(afterLogin);
        return _hasAssetAccess.Value;
    }

    private async Task CreateAsset()
    {
        var unique = $"{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}";
        _assetTag = $"AUTO-{unique}";
        _assetName = $"Automation MBP13 {unique}";
        _serial = $"SN{unique}";

        // Create the asset.
        await Page.GotoAsync($"{BaseUrl}/hardware/create");
        await Page.Locator("#asset_tag").FillAsync(_assetTag);
        await Page.Locator("#serial_1").FillAsync(_serial);
        await SelectOption("model_select_id", ModelName, typeToSearch: true);
        await SelectOption("status_select_id", StatusName);
        await Page.Locator("#optional_info").ClickAsync(); // "Asset Name" is in this collapsed section
        await Page.Locator("#name").FillAsync(_assetName);
        await Page.Locator("select[name=redirect_option]").SelectOptionAsync("item", new() { Force = true }); // go to the asset after saving
        await Page.Locator("#submit_button").ClickAsync();

        // Saved = we land on the new asset's page showing our tag.
        await Expect(Page).ToHaveURLAsync(new Regex(@"/hardware/\d+$"));
        await Expect(Page.Locator(".js-copy-asset_tag")).ToHaveTextAsync(_assetTag);
        _assetId = Regex.Match(Page.Url, @"/hardware/(\d+)$").Groups[1].Value;
        TestContext.Progress.WriteLine($"Created {_assetTag} (id {_assetId})");

        // Verify the new asset page shows what was entered.
        await Expect(Page.Locator(".js-copy-asset_name")).ToHaveTextAsync(_assetName);
        await Expect(Page.Locator("#serial-number code")).ToHaveTextAsync(_serial);
        await Expect(Page.Locator("#model .js-copy-asset_model")).ToHaveTextAsync(ModelName);
        await Expect(Page.Locator("#details .well").First).ToContainTextAsync(StatusName);
        TestContext.Progress.WriteLine($"Verified {_assetTag} created: name \"{_assetName}\", serial {_serial}, {ModelName}, {StatusName}");

        // Check it out to a random user.
        await Page.GotoAsync($"{AssetUrl}/checkout");
        await Page.Locator("input[name=checkout_to_type][value=user]").CheckAsync();
        var (_, userLabel) = await SelectRandomOption("assigned_user_select");
        _userName = userLabel.Split(" (")[0].Trim(); // Choice based upon manual checks: "Bailey Lowell (zack.leannon) - #4589" -> "Bailey Lowell"
        await Page.Locator("select[name=redirect_option]").SelectOptionAsync("item", new() { Force = true }); // back to the asset after saving
        await Page.Locator("#submit_button").ClickAsync();

        // Checked out
        // Snipe-IT seems to leave the checkout form and returns to the asset page.
        await Expect(Page).ToHaveURLAsync(new Regex($"/hardware/{_assetId}$"));
        TestContext.Progress.WriteLine($"Checked out {_assetTag} to {userLabel}");

        // Verify the asset page now shows it deployed to that user.
        var statusPanel = Page.Locator("#details .well").First;
        await Expect(statusPanel).ToContainTextAsync("Deployed");
        await Expect(statusPanel).ToContainTextAsync(_userName);
        TestContext.Progress.WriteLine($"Verified {_assetTag} checked out: Deployed to {_userName}");
    }

    private async Task FindAsset()
    {
        await Page.GotoAsync($"{BaseUrl}/hardware");
        var search = Page.Locator(".bootstrap-table .search input");
        await search.FillAsync(_assetTag);
        await search.PressAsync("Enter");

        var row = Page.Locator(".bootstrap-table table tbody tr", new() { HasText = _assetTag });
        await Expect(row).ToHaveCountAsync(1);
        await Expect(row).ToContainTextAsync(_assetName);
        await Expect(row).ToContainTextAsync(ModelName);
        await Expect(row).ToContainTextAsync(StatusName);
        await Expect(row).ToContainTextAsync(_userName);

        // Open the asset page from the list.
        await row.GetByRole(AriaRole.Link, new() { Name = _assetTag, Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"/hardware/{_assetId}$"));
        TestContext.Progress.WriteLine($"Found {_assetTag} in the assets list ({_assetName}, {ModelName}, {StatusName}, {_userName}) and opened it");
    }

    private async Task ValidateAsset()
    {
        await Expect(Page.Locator(".js-copy-asset_tag")).ToHaveTextAsync(_assetTag);
        await Expect(Page.Locator(".js-copy-asset_name")).ToHaveTextAsync(_assetName);
        await Expect(Page.Locator("#serial-number code")).ToHaveTextAsync(_serial);
        await Expect(Page.Locator("#model .js-copy-asset_model")).ToHaveTextAsync(ModelName);

        // Status panel: "Ready to Deploy  Deployed  ->  user name".
        // (A non-admin account can't open user profiles, so the user's name is plain text, not a link.)
        var statusPanel = Page.Locator("#details .well").First;
        await Expect(statusPanel).ToContainTextAsync(StatusName);
        await Expect(statusPanel).ToContainTextAsync("Deployed");
        await Expect(statusPanel).ToContainTextAsync(_userName);
        TestContext.Progress.WriteLine($"Validated {_assetTag} details page: name \"{_assetName}\", serial {_serial}, {ModelName}, {StatusName} / Deployed to {_userName}");
    }

    private async Task VerifyHistory()
    {
        await OpenHistory();

        var created = HistoryRows("create new");
        await Expect(created).ToHaveCountAsync(1);
        await Expect(created).ToContainTextAsync(_assetTag);

        var checkout = HistoryRows("checkout");
        await Expect(checkout).ToHaveCountAsync(1);
        await Expect(checkout).ToContainTextAsync(_assetTag);
        await Expect(checkout).ToContainTextAsync(_userName);
        TestContext.Progress.WriteLine($"Verified {_assetTag} history: \"create new\" and \"checkout\" to {_userName}");
    }

    private async Task ModifyAsset()
    {
        var newName = $"{_assetName} (edited)";
        var newSerial = $"{_serial}-EDIT";

        await Page.GotoAsync($"{AssetUrl}/edit");
        await Expect(Page.Locator("#asset_tag")).ToHaveValueAsync(_assetTag);
        if (!await Page.Locator("#name").IsVisibleAsync())
            await Page.Locator("#optional_info").ClickAsync();
        await Page.Locator("#name").FillAsync(newName);
        await Page.Locator("#serial_1").FillAsync(newSerial);
        await Page.Locator("#submit_button").ClickAsync();
        await Expect(Page).Not.ToHaveURLAsync(new Regex("/edit$")); // saved = we left the edit form
        TestContext.Progress.WriteLine($"Modified {_assetTag}: name \"{_assetName}\" -> \"{newName}\", serial {_serial} -> {newSerial}");

        _assetName = newName;
        _serial = newSerial;

        // The asset page shows the new values, it is still checked out, and History logged the update.
        await Page.GotoAsync(AssetUrl);
        await ValidateAsset();
        await OpenHistory();
        await Expect(HistoryRows("update")).ToHaveCountAsync(1);
        TestContext.Progress.WriteLine($"Verified {_assetTag} history: \"update\" logged");
    }

    private async Task DeleteAsset(bool checkRemovedFromList = true)
    {
        await Page.GotoAsync(AssetUrl);
        await Page.Locator("button.delete-asset[data-target='#dataConfirmModal']").ClickAsync(); // "Checkin and Delete"
        await Page.Locator("#dataConfirmModal #dataConfirmOK").ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/hardware$")); // back on the assets list
        _deleted = true;
        TestContext.Progress.WriteLine(checkRemovedFromList
            ? $"Deleted {_assetTag} (id {_assetId})"
            : $"Cleanup: deleted {_assetTag} (id {_assetId})");
        if (!checkRemovedFromList)
            return;

        // It no longer appears in the assets list.
        await Page.GotoAsync($"{BaseUrl}/hardware");
        var search = Page.Locator(".bootstrap-table .search input");
        await search.FillAsync(_assetTag);
        await search.PressAsync("Enter");
        await Expect(Page.Locator(".bootstrap-table table tbody tr.no-records-found")).ToBeVisibleAsync();
        TestContext.Progress.WriteLine($"Verified {_assetTag} is no longer in the assets list");
    }

    // Opens the History tab and waits until its rows have loaded.
    private async Task OpenHistory()
    {
        await Page.Locator("a[href='#history']").ClickAsync();
        await Expect(Page.Locator("#history table tbody tr[data-index]").First).ToBeVisibleAsync();
    }

    // History tab rows whose action column is exactly e.g. "checkout".
    private ILocator HistoryRows(string action) =>
        Page.Locator("#history table tbody tr[data-index]")
            .Filter(new() { Has = Page.Locator("td", new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(action)}\s*$") }) });
}
