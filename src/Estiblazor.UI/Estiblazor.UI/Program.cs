using System.Security.Claims;
using Estiblazor.Application;
using Estiblazor.Domain;
using Estiblazor.Infrastructure;
using Estiblazor.UI.Components;
using Estiblazor.UI.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Trust HTTPS information only from the configured private reverse-proxy network.
if (builder.Configuration["ReverseProxy:KnownNetwork"] is { Length: > 0 } proxyNetwork)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(proxyNetwork));
    });
}
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is required.");
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(
        redisConnection + ",abortConnect=false,connectTimeout=3000,asyncTimeout=3000"
    )
);
builder.Services.AddSingleton<RedisRoomStore>();
builder.Services.AddSingleton<IRoomStore>(sp => sp.GetRequiredService<RedisRoomStore>());
builder.Services.AddSingleton<IRoomDirectory>(sp => sp.GetRequiredService<RedisRoomStore>());
builder.Services.AddSingleton<IRoomUpdates, RoomUpdates>();
builder.Services.AddSingleton<RoomService>();
builder.Services.AddScoped<IBrowserSession, BrowserSession>();
builder.Services.AddScoped<CircuitPresence>();
builder.Services.AddScoped<CircuitHandler>(sp => sp.GetRequiredService<CircuitPresence>());
builder.Services.AddAuthorization();
builder.Services.AddAuthentication();
builder.Services.AddAntiforgery(options =>
    options.Cookie.SecurePolicy = builder.Configuration.GetValue("Cookies:Secure", true)
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.None
);
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder
    .Services.AddDataProtection()
    .SetApplicationName("Estiblazor")
    .PersistKeysToFileSystem(
        new DirectoryInfo(builder.Configuration["DataProtection:KeysPath"] ?? "/data/keys")
    );
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
var app = builder.Build();
app.UseForwardedHeaders();
app.UseResponseCompression();
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.Use(
    async (context, next) =>
    {
        var protector = context
            .RequestServices.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("browser-identity-v1");
        Guid user = Guid.Empty;
        if (context.Request.Cookies.TryGetValue("estiblazor.browser", out var cookie))
        {
            try
            {
                Guid.TryParse(protector.Unprotect(cookie), out user);
            }
            catch (System.Security.Cryptography.CryptographicException) { }
        }
        if (user == Guid.Empty)
        {
            user = Guid.NewGuid();
            context.Response.Cookies.Append(
                "estiblazor.browser",
                protector.Protect(user.ToString()),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = builder.Configuration.GetValue("Cookies:Secure", true),
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromDays(365),
                    Path = "/",
                    IsEssential = true,
                }
            );
        }
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], "Browser")
        );
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (
            context.Request.Path.StartsWithSegments("/rooms")
            || context.Request.Path.StartsWithSegments("/manage")
            || (context.Request.Path == "/create" && context.Request.Query.ContainsKey("from"))
        )
        {
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
            context.Response.Headers.CacheControl = "no-store";
        }
        await next();
    }
);
app.MapStaticAssets();
app.UseAntiforgery();
app.MapGet("/manage/{code}", async (HttpContext context, string code, RoomService rooms) =>
{
    context.Response.Headers.CacheControl = "no-store";
    try
    {
        await rooms.RecoverAsync(
            code,
            Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            context.Request.Query["secret"].ToString()
        );
        // HTTP redirect replaces the secret-bearing navigation before loading the application.
        context.Response.Redirect("/rooms/" + Uri.EscapeDataString(code.ToUpperInvariant()));
    }
    catch (Exception ex) when (ex is RoomException or StorageUnavailableException)
    {
        context.Response.Redirect("/?error=" + Uri.EscapeDataString(ex.Message));
    }
});

// Public discovery endpoints never include room codes or management links.
app.MapGet("/join", async (HttpContext context, RoomService rooms) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    try
    {
        var value = context.Request.Query["code"].ToString().Trim();
        if (value.Length > 2048)
            throw new RoomException("Use a valid room code or invitation link.");
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            value = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault()
                ?? "";
        var room = await rooms.ReadAsync(
            value,
            Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!)
        );
        return Results.Redirect("/rooms/" + room.Code);
    }
    catch (Exception ex) when (ex is RoomException or StorageUnavailableException)
    {
        return Results.Redirect("/?error=" + Uri.EscapeDataString(ex.Message));
    }
});
app.MapGet("/robots.txt", (HttpContext context, IConfiguration configuration) =>
    Results.Text(
        "User-agent: *\nAllow: /\nDisallow: /manage/\nDisallow: /join\nDisallow: /health\nDisallow: /_blazor\nSitemap: "
            + PublicOrigin(context, configuration)
            + "/sitemap.xml\n",
        "text/plain"
    )
);
app.MapGet("/sitemap.xml", (HttpContext context, IConfiguration configuration) =>
{
    var origin = System.Security.SecurityElement.Escape(PublicOrigin(context, configuration));
    var urls = new[] { "/", "/create", "/how-it-works", "/accessibility" };
    return Results.Text(
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">"
            + string.Join("", urls.Select(path => "<url><loc>" + origin + path + "</loc></url>"))
            + "</urlset>",
        "application/xml"
    );
});
app.MapGet("/health", async (IRoomStore store) =>
{
    try
    {
        await store.ReadAsync("health");
        return Results.Ok(new { status = "ready" });
    }
    catch (StorageUnavailableException)
    {
        return Results.StatusCode(503);
    }
});
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

static string PublicOrigin(HttpContext context, IConfiguration configuration) =>
    (
        configuration["Site:PublicUrl"]
        ?? $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}"
    ).TrimEnd('/');
