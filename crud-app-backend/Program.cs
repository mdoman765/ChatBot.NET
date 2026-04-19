using crud_app_backend;
using crud_app_backend.Bot.Services;
using crud_app_backend.Repositories;
using crud_app_backend.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
// Memory cache for session (Fix 1)
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Existing repositories ─────────────────────────────────────────────────────
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IWhatsAppSessionRepository, WhatsAppSessionRepository>();
builder.Services.AddScoped<IWhatsAppMessageRepository, WhatsAppMessageRepository>();
builder.Services.AddScoped<IWhatsAppComplaintRepository, WhatsAppComplaintRepository>();

// ── Existing services ─────────────────────────────────────────────────────────
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IWhatsAppSessionService, WhatsAppSessionService>();
builder.Services.AddScoped<IWhatsAppMessageService, WhatsAppMessageService>();
builder.Services.AddScoped<IWhatsAppComplaintService, WhatsAppComplaintService>();

// ── NEW Bot services ──────────────────────────────────────────────────────────
builder.Services.AddScoped<IBotService, BotService>();
builder.Services.AddScoped<IDialogClient, DialogClient>();
builder.Services.AddScoped<IHrisService, HrisService>();

// ── HTTP clients ──────────────────────────────────────────────────────────────

// CRM client (existing — used by WhatsAppComplaintService)
builder.Services.AddHttpClient("CrmClient", client =>
{
    var key = builder.Configuration["Crm:ApiKey"];
    if (!string.IsNullOrWhiteSpace(key))
        client.DefaultRequestHeaders.Add("access-token", key);
    client.Timeout = TimeSpan.FromMinutes(5);
});

// 360dialog client (NEW — used by DialogClient)
builder.Services.AddHttpClient("Dialog", client =>
{
    var key = builder.Configuration["Dialog:ApiKey"];
    if (!string.IsNullOrWhiteSpace(key))
        client.DefaultRequestHeaders.Add("D360-API-KEY", key);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// HRIS client (NEW — used by HrisService)
builder.Services.AddHttpClient("Hris", client =>
{
    var basicAuth = builder.Configuration["Hris:BasicAuth"];
    var apiKey = builder.Configuration["Hris:ApiKey"];
    if (!string.IsNullOrWhiteSpace(basicAuth))
        client.DefaultRequestHeaders.Add("Authorization", basicAuth);
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Add("S_KEYSD", apiKey);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// ── Form limits ───────────────────────────────────────────────────────────────
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 25 * 1024 * 1024; // 25 MB
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAngular");
app.UseStaticFiles();       // serves wwwroot/wa-media/ as public URLs
app.UseAuthorization();
app.MapControllers();

app.Run();
