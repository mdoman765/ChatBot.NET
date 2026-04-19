using System.Text.Json;

namespace crud_app_backend.Bot.Services
{
    /// <summary>
    /// Wraps the 360dialog v2 API.
    ///   Send:  POST https://waba-v2.360dialog.io/messages
    ///   Media: GET  https://waba-v2.360dialog.io/{mediaId}
    ///   Auth:  D360-API-KEY header (registered as "Dialog" named client)
    /// </summary>
    public class DialogClient : IDialogClient
    {
        private const string BaseUrl = "https://waba-v2.360dialog.io";

        private readonly IHttpClientFactory      _factory;
        private readonly ILogger<DialogClient>   _logger;

        public DialogClient(IHttpClientFactory factory, ILogger<DialogClient> logger)
        {
            _factory = factory;
            _logger  = logger;
        }

        // ── Send text ─────────────────────────────────────────────────────────

        public async Task SendTextAsync(string phone, string message,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var client = _factory.CreateClient("Dialog");
            var payload = new
            {
                messaging_product = "whatsapp",
                to   = phone,
                type = "text",
                text = new { body = message }
            };

            var resp = await client.PostAsJsonAsync($"{BaseUrl}/messages", payload, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("[Dialog] Send failed {Code} to {Phone}: {Body}",
                    (int)resp.StatusCode, phone, body.Length > 200 ? body[..200] : body);
            }
            else
            {
                _logger.LogDebug("[Dialog] Message sent to {Phone}", phone);
            }
        }

        // ── Download media ────────────────────────────────────────────────────

        public async Task<(byte[] Data, string MimeType)> DownloadMediaAsync(
            string mediaId, string fallbackMime,
            CancellationToken ct = default)
        {
            var client = _factory.CreateClient("Dialog");

            // Step 1 — get the CDN download URL for this media ID
            var metaResp = await client.GetAsync($"{BaseUrl}/{mediaId}", ct);
            metaResp.EnsureSuccessStatusCode();

            var metaJson = await metaResp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(metaJson);

            var url = doc.RootElement.TryGetProperty("url", out var urlEl)
                ? urlEl.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException(
                    $"360dialog returned no URL for mediaId={mediaId}");

            // lookaside.fbsbx.com must be routed through 360dialog proxy
            url = url.Replace("https://lookaside.fbsbx.com", BaseUrl);

            // Step 2 — download binary
            var binResp = await client.GetAsync(url, ct);
            binResp.EnsureSuccessStatusCode();

            var mime  = binResp.Content.Headers.ContentType?.MediaType ?? fallbackMime;
            var bytes = await binResp.Content.ReadAsByteArrayAsync(ct);

            _logger.LogDebug("[Dialog] Downloaded mediaId={Id}: {Bytes}b mime={Mime}",
                mediaId, bytes.Length, mime);

            return (bytes, mime);
        }
    }
}
