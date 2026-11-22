using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RMPCS.Core;

public sealed record ReleaseOffer(string Tag, string Url, string Notes, ReleaseVersion Version, bool Preview);
public sealed record UpdateResult(ReleaseOffer? Latest, bool Newer, string Message);

public sealed class UpdateChecker(HttpClient client)
{
    public const string Feed = "https://api.github.com/repos/" + ProductInfo.Repository + "/releases?per_page=100";
    public async Task<UpdateResult> CheckAsync(string currentVersion, bool includePreview, CancellationToken cancellation = default)
    {
        if (!ReleaseVersion.TryParse(currentVersion, out var current)) throw new ArgumentException("Unsupported application version.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Get, Feed);
        request.Headers.UserAgent.ParseAdd("RMPCS/" + currentVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) return new(null, false, "GitHub rate-limited or denied this check. Try again later or open Releases.");
        if (response.StatusCode == HttpStatusCode.NotFound) return new(null, false, "The release feed is unavailable or private. Open the official Releases page to check access.");
        if ((int)response.StatusCode is >= 300 and < 400) throw new InvalidDataException("GitHub redirected the request. Open the official Releases page instead.");
        response.EnsureSuccessStatusCode();
        const int limit = 2 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Release response exceeds the size limit.");
        using var source = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var buffer = new MemoryStream(); byte[] bytes = new byte[16384]; int read;
        while ((read = await source.ReadAsync(bytes, deadline.Token).ConfigureAwait(false)) != 0)
        { if (buffer.Length + read > limit) throw new InvalidDataException("Release response exceeds the size limit."); await buffer.WriteAsync(bytes.AsMemory(0, read), deadline.Token).ConfigureAwait(false); }
        var latest = Select(buffer.ToArray(), includePreview);
        if (latest is null) return new(null, false, "No compatible releases were found in the newest 100 published releases for this channel.");
        bool newer = latest.Version.CompareTo(current) > 0;
        return new(latest, newer, newer ? $"Update available: {latest.Tag}. Review the release before installing." : $"No newer version found. Newest matching release: {latest.Tag}.");
    }
    public static ReleaseOffer? Select(ReadOnlyMemory<byte> json, bool includePreview)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("GitHub returned an unexpected release response.");
        ReleaseOffer? best = null;
        foreach (var item in document.RootElement.EnumerateArray().Take(100))
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False ||
                !item.TryGetProperty("prerelease", out var pre) || pre.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !item.TryGetProperty("tag_name", out var tagElement) || tagElement.ValueKind != JsonValueKind.String) continue;
            string tag = tagElement.GetString()!;
            if (!ReleaseVersion.TryParse(tag, out var version)) continue;
            bool preview = pre.GetBoolean() || version.Preview.Length != 0;
            if (preview && !includePreview) continue;
            string expected = ProductInfo.Releases + "/tag/" + Uri.EscapeDataString(tag);
            if (!item.TryGetProperty("html_url", out var url) || url.ValueKind != JsonValueKind.String || url.GetString() != expected) continue;
            string notes = item.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String ? body.GetString()! : "No release notes supplied.";
            if (notes.Length > 12000) notes = notes[..12000] + "\n[Open the release for the full notes.]";
            var offer = new ReleaseOffer(tag, expected, notes, version, preview);
            if (best is null || offer.Version.CompareTo(best.Version) > 0) best = offer;
        }
        return best;
    }
}
