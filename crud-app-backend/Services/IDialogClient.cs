namespace crud_app_backend.Bot.Services
{
    public interface IDialogClient
    {
        /// <summary>Send a plain-text WhatsApp message via 360dialog v2.</summary>
        Task SendTextAsync(string phone, string message,
            CancellationToken ct = default);

        /// <summary>
        /// Download a media file from 360dialog.
        /// Returns (bytes, mimeType). Throws on failure.
        /// </summary>
        Task<(byte[] Data, string MimeType)> DownloadMediaAsync(
            string mediaId, string fallbackMime,
            CancellationToken ct = default);
    }
}
