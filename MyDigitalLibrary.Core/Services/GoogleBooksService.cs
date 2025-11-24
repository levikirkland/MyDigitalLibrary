using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;
using System.Threading;

namespace MyDigitalLibrary.Core.Services;

public class GoogleBooksService : IGoogleBooksService
{
    private readonly HttpClient _http;

    public GoogleBooksService(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<GoogleBook[]> SearchByTitleAsync(string? title, string? author = null, int maxResults = 10, CancellationToken cancellation = default)
    {
        // Validate HttpClient base address to avoid SSRF-like misconfiguration
        if (_http.BaseAddress == null || !_http.BaseAddress.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) || !_http.BaseAddress.Host.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("GoogleBooksService HttpClient must be configured with a secure BaseAddress pointing to googleapis.com");
        }

        // Google Books API limits maxResults to 40
        maxResults = Math.Clamp(maxResults, 1, 40);

        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(author)) return Array.Empty<GoogleBook>();

        // use a cancellation timeout to avoid long-running requests
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        var token = cts.Token;

        // try operator-based query first (intitle:/inauthor:)
        var terms = new List<string>();
        if (!string.IsNullOrWhiteSpace(title)) terms.Add($"intitle:{Uri.EscapeDataString(title)}");
        if (!string.IsNullOrWhiteSpace(author)) terms.Add($"inauthor:{Uri.EscapeDataString(author)}");

        var q = string.Join("%20", terms);
        var url = $"/books/v1/volumes?q={q}&maxResults={maxResults}";
        var resp = await _http.GetAsync(url, token);
        if (!resp.IsSuccessStatusCode) return Array.Empty<GoogleBook>();

        // guard response size where possible
        if (resp.Content.Headers.ContentLength.HasValue && resp.Content.Headers.ContentLength.Value > 1_000_000)
        {
            return Array.Empty<GoogleBook>();
        }

        using var stream = await resp.Content.ReadAsStreamAsync(token);
        var doc = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: token);
        var results = ParseDocument(doc);

        // If results are fewer than desired, try a fallback broad search (plain title [+ author])
        if (results.Length < maxResults && !string.IsNullOrWhiteSpace(title))
        {
            try
            {
                var plain = Uri.EscapeDataString(string.IsNullOrWhiteSpace(author) ? title : (title + " " + author));
                var fallbackUrl = $"/books/v1/volumes?q={plain}&maxResults={maxResults}&orderBy=relevance";
                var resp2 = await _http.GetAsync(fallbackUrl, token);
                if (resp2.IsSuccessStatusCode)
                {
                    if (resp2.Content.Headers.ContentLength.HasValue && resp2.Content.Headers.ContentLength.Value > 1_000_000)
                    {
                        // don't consume excessively large response
                    }
                    else
                    {
                        using var stream2 = await resp2.Content.ReadAsStreamAsync(token);
                        var doc2 = await System.Text.Json.JsonDocument.ParseAsync(stream2, cancellationToken: token);
                        var more = ParseDocument(doc2);
                        // merge unique by Id, prefer original order
                        var map = new Dictionary<string, GoogleBook>(StringComparer.Ordinal);
                        foreach (var r in results) map[r.Id] = r;
                        foreach (var r in more) if (!map.ContainsKey(r.Id)) map[r.Id] = r;
                        results = map.Values.Take(maxResults).ToArray();
                    }
                }
            }
            catch
            {
                // ignore fallback failures
            }
        }

        return results;
    }

    private static GoogleBook[] ParseDocument(System.Text.Json.JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("items", out var items)) return Array.Empty<GoogleBook>();
        var list = new List<GoogleBook>();
        foreach (var it in items.EnumerateArray())
        {
            try
            {
                var gb = new GoogleBook();
                gb.Id = it.GetProperty("id").GetString() ?? string.Empty;
                if (it.TryGetProperty("volumeInfo", out var vi))
                {
                    var v = new GoogleBook.VolumeInfoData();
                    if (vi.TryGetProperty("title", out var t)) v.Title = t.GetString();
                    if (vi.TryGetProperty("authors", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var authors = new List<string>();
                        foreach (var au in a.EnumerateArray()) if (au.ValueKind == System.Text.Json.JsonValueKind.String) authors.Add(au.GetString()!);
                        v.Authors = authors.ToArray();
                    }
                    if (vi.TryGetProperty("publisher", out var p)) v.Publisher = p.GetString();
                    if (vi.TryGetProperty("publishedDate", out var pd)) v.PublishedDate = pd.GetString();
                    if (vi.TryGetProperty("description", out var d)) v.Description = d.GetString();
                    if (vi.TryGetProperty("pageCount", out var pc) && pc.TryGetInt32(out var pcc)) v.PageCount = pcc;
                    if (vi.TryGetProperty("language", out var lang)) v.Language = lang.GetString();

                    if (vi.TryGetProperty("industryIdentifiers", out var ids) && ids.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var idList = new List<GoogleBook.IndustryIdentifier>();
                        foreach (var idEl in ids.EnumerateArray())
                        {
                            var ii = new GoogleBook.IndustryIdentifier();
                            if (idEl.TryGetProperty("type", out var typ)) ii.Type = typ.GetString() ?? string.Empty;
                            if (idEl.TryGetProperty("identifier", out var ident)) ii.Identifier = ident.GetString() ?? string.Empty;
                            idList.Add(ii);
                        }
                        v.IndustryIdentifiers = idList.ToArray();
                    }

                    // imageLinks (optional)
                    if (vi.TryGetProperty("imageLinks", out var il) && il.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (il.TryGetProperty("thumbnail", out var thumb)) v.ThumbnailLink = thumb.GetString();
                        if (il.TryGetProperty("smallThumbnail", out var small)) v.SmallThumbnailLink = small.GetString();
                    }

                    gb.VolumeInfo = v;
                }
                list.Add(gb);
            }
            catch
            {
                // ignore malformed item
            }
        }
        return list.ToArray();
    }

    public void ApplyToEntity(BookEntity entity, GoogleBook source, bool overwrite = false)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (source == null) throw new ArgumentNullException(nameof(source));

        var vi = source.VolumeInfo;
        if (!string.IsNullOrWhiteSpace(vi?.Title) && (overwrite || string.IsNullOrWhiteSpace(entity.Title))) entity.Title = vi.Title!;
        if (vi?.Authors != null && (overwrite || string.IsNullOrWhiteSpace(entity.Authors))) entity.Authors = string.Join(", ", vi.Authors);
        if (!string.IsNullOrWhiteSpace(vi?.Description) && (overwrite || string.IsNullOrWhiteSpace(entity.Description))) entity.Description = vi.Description;
        if (!string.IsNullOrWhiteSpace(vi?.Publisher) && (overwrite || string.IsNullOrWhiteSpace(entity.Publisher))) entity.Publisher = vi.Publisher;
        if (!string.IsNullOrWhiteSpace(vi?.PublishedDate) && (overwrite || string.IsNullOrWhiteSpace(entity.PublishedAt))) entity.PublishedAt = vi.PublishedDate;
        if (vi?.PageCount.HasValue == true && (overwrite || entity.TotalPages == null)) entity.TotalPages = vi.PageCount;
        if (!string.IsNullOrWhiteSpace(vi?.Language) && (overwrite || string.IsNullOrWhiteSpace(entity.Language))) entity.Language = vi.Language;

        // ISBN: prefer ISBN_13 then ISBN_10
        if (vi?.IndustryIdentifiers != null && (overwrite || string.IsNullOrWhiteSpace(entity.Isbn)))
        {
            var isbn13 = vi.IndustryIdentifiers.FirstOrDefault(i => i.Type?.Equals("ISBN_13", StringComparison.OrdinalIgnoreCase) == true)?.Identifier;
            var isbn10 = vi.IndustryIdentifiers.FirstOrDefault(i => i.Type?.Equals("ISBN_10", StringComparison.OrdinalIgnoreCase) == true)?.Identifier;
            entity.Isbn = isbn13 ?? isbn10 ?? entity.Isbn;
        }

        entity.UpdatedAt = DateTime.UtcNow;
    }
}
