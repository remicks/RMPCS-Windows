using System.Net;
using System.Text;
using System.Text.Json;
using RMPCS.Core;

internal static class Version06Checks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        ReleaseVersion V(string tag) => ReleaseVersion.TryParse(tag, out var version) ? version : throw new Exception("Invalid fixture version");
        check(V("v0.10.0").CompareTo(V("v0.9.9")) > 0, "updates compare numeric versions rather than lexicographic tags");
        check(V("1.0.0").CompareTo(V("1.0.0-rc.10")) > 0 && V("1.0.0-rc.10").CompareTo(V("1.0.0-rc.2")) > 0, "release ordering handles final and numeric preview identifiers");
        check(V("1.2.3+build.5").CompareTo(V("v1.2.3")) == 0 && V("1.0.0-alpha").CompareTo(V("1.0.0-alpha.1")) < 0, "build metadata does not change precedence and longer previews are ordered");
        foreach (string tag in new[] { "latest", "1.2", "01.2.3", "1.2.3-01", "v1.2.3/../../bad", "1.2.3\n", new string('9', 140) })
            check(!ReleaseVersion.TryParse(tag, out _), "malformed update version rejected: " + tag.Trim()[..Math.Min(tag.Trim().Length, 25)]);
        object Release(string tag, bool draft = false, bool preview = false, string? url = null) => new { tag_name = tag, draft, prerelease = preview, html_url = url ?? ProductInfo.Releases + "/tag/" + Uri.EscapeDataString(tag), body = "Plain text <script>test</script> notes" };
        byte[] Json(params object[] items) => JsonSerializer.SerializeToUtf8Bytes(items);
        byte[] feed = Json(Release("v0.8.0", preview: true), Release("v0.7.0"), Release("v9.0.0", draft: true), Release("v0.9.0-rc.1"));
        check(UpdateChecker.Select(feed, false)?.Tag == "v0.7.0", "stable channel excludes drafts and all prerelease forms");
        check(UpdateChecker.Select(feed, true)?.Tag == "v0.9.0-rc.1", "preview channel selects highest semantic release even out of order");
        foreach (string url in new[] { "https://evil.example/v0.9.0", "http://github.com/remicks/RMPCS-Windows/releases/tag/v0.9.0", "https://github.com.evil.example/remicks/RMPCS-Windows/releases/tag/v0.9.0", "https://github.com/other/repo/releases/tag/v0.9.0", ProductInfo.Releases + "/tag/v0.8.0", ProductInfo.Releases + "/tag/v0.9.0?redirect=evil" })
            check(UpdateChecker.Select(Json(Release("v0.9.0", url: url)), true) is null, "update link must exactly match the official repository and version");
        check(UpdateChecker.Select(Json(Release("v0.7.0")), true)!.Notes.Contains("<script>"), "release notes remain plain text data without HTML rendering");
        bool invalid = false; try { UpdateChecker.Select("{}"u8.ToArray(), true); } catch (InvalidDataException) { invalid = true; }
        check(invalid, "unexpected release JSON shapes are rejected");
        using (var client = new HttpClient(new FakeHandler(request =>
        {
            check(request.RequestUri?.AbsoluteUri == UpdateChecker.Feed && request.Method == HttpMethod.Get && request.Headers.Authorization is null && request.Headers.UserAgent.ToString().Contains("RMPCS/0.7.0"), "update request is fixed-host read-only and contains no credentials");
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(feed) };
        })))
        {
            var result = await new UpdateChecker(client).CheckAsync("0.7.0", true);
            check(result.Newer && result.Latest?.Tag == "v0.9.0-rc.1", "HTTP update flow reports newer preview releases");
        }
        using (var client = new HttpClient(new FakeHandler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Json(Release("v0.5.0"))) })))
            check(!(await new UpdateChecker(client).CheckAsync("0.7.0", true)).Newer, "older published builds are not offered as updates");
        foreach (var status in new[] { HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests, HttpStatusCode.NotFound })
        {
            using var client = new HttpClient(new FakeHandler(_ => new(status)));
            var result = await new UpdateChecker(client).CheckAsync("0.7.0", true);
            check(result.Latest is null && !result.Message.Contains("No newer"), "rate-limit and unavailable-feed errors never claim an up-to-date result");
        }
        using (var client = new HttpClient(new FakeHandler(_ => new(HttpStatusCode.Redirect))))
        { invalid = false; try { await new UpdateChecker(client).CheckAsync("0.7.0", true); } catch (InvalidDataException) { invalid = true; } check(invalid, "update transport refuses redirect responses"); }
        using (var client = new HttpClient(new FakeHandler(_ => { var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") }; response.Content.Headers.ContentLength = 2 * 1024 * 1024 + 1; return response; })))
        { invalid = false; try { await new UpdateChecker(client).CheckAsync("0.7.0", true); } catch (InvalidDataException) { invalid = true; } check(invalid, "oversized update responses are rejected before reading"); }
        using (var client = new HttpClient(new FakeHandler(_ => throw new HttpRequestException("offline"))))
        { invalid = false; try { await new UpdateChecker(client).CheckAsync("0.7.0", true); } catch (HttpRequestException) { invalid = true; } check(invalid, "offline failures propagate without a false success"); }
        using (var client = new HttpClient(new FakeHandler(_ => new(HttpStatusCode.OK))))
        using (var cancel = new CancellationTokenSource())
        { cancel.Cancel(); invalid = false; try { await new UpdateChecker(client).CheckAsync("0.7.0", true, cancel.Token); } catch (OperationCanceledException) { invalid = true; } check(invalid, "update checks respect caller cancellation"); }
        var defaults = new AppPreferences();
        check(!defaults.CheckUpdatesOnLaunch && defaults.IncludePrereleases && defaults.CleanupAgeDays == 7, "default settings preserve opt-in networking and conservative cleanup");
        var custom = defaults with { CloseToTray = false, MinimizeToTray = false, TrayNotifications = false, StartPage = "Help", ActivityFontSize = 16, CleanupAgeDays = 30 };
        check(AppPreferences.Parse(custom.ToJson()) == custom, "window, reading and cleanup preferences round-trip without task commands");
        var normalized = new AppPreferences { ActivityFontSize = 999, CleanupAgeDays = 0, StartPage = "https://evil.example" }.Normalize();
        check(normalized.ActivityFontSize == 12 && normalized.CleanupAgeDays == 7 && normalized.StartPage == "Health", "invalid preferences normalize to safe supported values");
        invalid = false; try { AppPreferences.Parse(new string('a', 16385)); } catch (InvalidDataException) { invalid = true; } check(invalid, "preference input is size bounded");
        check(HelpCatalog.Search("DNS").Count > 0 && HelpCatalog.Search("GITHUB preview").Any(a => a.Title.Contains("GitHub")), "offline help search supports case-insensitive multiword matching");
        check(HelpCatalog.Search("nonsenseXYZ").Count == 0 && HelpCatalog.Search("").Count == HelpCatalog.Articles.Count, "help handles no-result and empty searches");
        check(ProductInfo.Name == "Rusty's PC Toolkit" && ProductInfo.LootDrop == "https://github.com/remicks/LootDrop-Windows" && HelpCatalog.Search("LootDrop").Count > 0, "Rusty branding and verified LootDrop project are included in help");
    }
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
    }
}
