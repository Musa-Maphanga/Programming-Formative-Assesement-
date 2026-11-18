using System.Text.Json;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// MVC
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews();

// ---------------------------------------------------------------------------
// Session. This is where the signed-in user's identity lives: the JWT returned
// by the Part 2 API, together with their user id and role. Nothing is stored
// in a local database, because this project has no database of its own.
// ---------------------------------------------------------------------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(
        builder.Configuration.GetValue("Session:IdleTimeoutMinutes", 60));
});

builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// The API client. One JSON configuration is shared by the client and by model
// binding, and it is deliberately case-insensitive so that the application
// copes with either camelCase or PascalCase responses from Part 2.
// ---------------------------------------------------------------------------
builder.Services.AddSingleton(new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});

builder.Services.AddTransient<BearerTokenHandler>();

var apiBaseUrl = builder.Configuration["RaceDayApi:BaseUrl"]
                 ?? throw new InvalidOperationException(
                     "RaceDayApi:BaseUrl is not configured. Set it in appsettings.json " +
                     "or pass it as the RaceDayApi__BaseUrl environment variable.");

builder.Services.AddHttpClient<IRaceDayApiClient, RaceDayApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<BearerTokenHandler>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Anything the API rejects that reaches this far, plus 404s on our own routes,
// is shown as a styled page rather than a blank browser error.
app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

app.UseStaticFiles();
app.UseRouting();

// Session must come before the endpoints, since the role filter reads it.
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Used by the container health check and by the CI pipeline. It reports on this
// web application only; whether the API behind it is reachable is a separate
// question, answered by the API's own health endpoint.
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "RaceDay.Web",
    apiBaseUrl
}));

app.Run();
