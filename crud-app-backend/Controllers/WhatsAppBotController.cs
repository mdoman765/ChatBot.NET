//using System.Text.Json;
//using crud_app_backend.Bot.Services;
//using Microsoft.AspNetCore.Mvc;

//namespace crud_app_backend.Bot.Controllers
//{
//    /// <summary>
//    /// Receives 360dialog webhook POST requests.
//    /// Returns 200 OK IMMEDIATELY — processes the message in the background.
//    /// This prevents 360dialog from timing out and resending duplicate messages.
//    /// </summary>
//    [ApiController]
//    [Route("webhook")]
//    public class WhatsAppBotController : ControllerBase
//    {
//        private readonly IBotService           _bot;
//        private readonly ILogger<WhatsAppBotController> _logger;

//        public WhatsAppBotController(
//            IBotService bot,
//            ILogger<WhatsAppBotController> logger)
//        {
//            _bot    = bot;
//            _logger = logger;
//        }

//        /// <summary>
//        /// 360dialog calls this endpoint on every incoming WhatsApp message.
//        /// Configure this URL in the 360dialog dashboard as your webhook.
//        /// URL: https://your-server/api/bot/webhook
//        /// </summary>
//        [HttpPost("whatsapp-webhook")]
//        public IActionResult Webhook([FromBody] JsonElement body)
//        {
//            // Return 200 to 360dialog IMMEDIATELY — no waiting
//            // If this takes > 5s, 360dialog will retry and users get duplicate replies
//            _ = Task.Run(async () =>
//            {
//                try   { await _bot.ProcessAsync(body); }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "[Webhook] Background ProcessAsync crashed");
//                }
//            });

//            return Ok(new { status = "received" });
//        }

//        /// <summary>Health check endpoint for keep-warm pings.</summary>
//        [HttpGet("health")]
//        public IActionResult Health() => Ok(new { status = "ok", time = DateTime.UtcNow });
//    }
//}
using System.Text.Json;
using crud_app_backend.Bot.Services;
using Microsoft.AspNetCore.Mvc;

namespace crud_app_backend.Bot.Controllers
{
    /// <summary>
    /// Receives 360dialog webhook POST requests.
    /// Returns 200 OK IMMEDIATELY — processes the message in the background.
    /// This prevents 360dialog from timing out and resending duplicate messages.
    /// </summary>
    [ApiController]
    [Route("webhook")]
    public class WhatsAppBotController : ControllerBase
    {
        // Use IServiceScopeFactory instead of IBotService directly.
        // Scoped services (BotService, DbContext, etc.) are tied to the HTTP request
        // scope and get disposed when the response is sent. A background Task.Run
        // that captures those scoped instances will crash with ObjectDisposedException.
        // Creating a fresh scope inside the background task gives it its own lifetime.
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WhatsAppBotController> _logger;

        public WhatsAppBotController(
            IServiceScopeFactory scopeFactory,
            ILogger<WhatsAppBotController> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// 360dialog calls this endpoint on every incoming WhatsApp message.
        /// Configure this URL in the 360dialog dashboard as your webhook.
        /// URL: https://your-server/api/bot/webhook
        /// </summary>
        [HttpPost("whatsapp-webhook")]
        public IActionResult Webhook([FromBody] JsonElement body)
        {
            // Capture the raw JSON bytes before the request scope is disposed.
            // JsonElement is backed by a JsonDocument tied to the request body
            // stream; cloning it produces a self-contained copy safe to use
            // after the HTTP response has been sent.
            var bodyCopy = body.Clone();

            // Return 200 to 360dialog IMMEDIATELY — no waiting.
            // If this takes > 5s, 360dialog will retry and users get duplicate replies.
            _ = Task.Run(async () =>
            {
                // Create a brand-new DI scope for the background work.
                // This scope (and its AppDbContext) lives until the using block exits,
                // completely independent of the now-disposed HTTP request scope.
                await using var scope = _scopeFactory.CreateAsyncScope();
                var bot = scope.ServiceProvider.GetRequiredService<IBotService>();

                try
                {
                    await bot.ProcessAsync(bodyCopy);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Webhook] Background ProcessAsync crashed");
                }
            });

            return Ok(new { status = "received" });
        }

        /// <summary>Health check endpoint for keep-warm pings.</summary>
        [HttpGet("health")]
        public IActionResult Health() => Ok(new { status = "ok", time = DateTime.UtcNow });
    }
}
