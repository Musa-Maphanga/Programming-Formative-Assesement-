using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Infrastructure;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));

// Services
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddHttpClient<IWeatherService, OpenMeteoWeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Server-side sessions (user id + role), kept in memory
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<IUserSessionStore, DistributedCacheSessionStore>();

// Authentication: JWT holds the session id, the session holds the user
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };

        // After the token checks out, make sure its session still exists on the server
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal!;
                var sessionId = principal.FindFirstValue(RaceDayClaims.SessionId);
                var sessions = context.HttpContext.RequestServices.GetRequiredService<IUserSessionStore>();
                var session = sessionId is null ? null : await sessions.GetAsync(sessionId);

                if (session is null)
                {
                    context.Fail("Session has ended. Please log in again.");
                    return;
                }

                var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                var role = principal.FindFirstValue(ClaimTypes.Role);
                if (userId != session.UserId.ToString() || role != session.Role)
                {
                    context.Fail("Token does not match the session.");
                    return;
                }

                context.HttpContext.Items[nameof(SessionInfo)] = session;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "RaceDay API (PROG6212 Part 2). Log in with POST /api/auth/login, then click Authorize and paste the token."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Paste the token from /api/auth/login."
    });
    c.OperationFilter<AuthorizationOperationFilter>();

    // Show sections in the same order as the Part 1 plan
    string[] sectionOrder = { "Auth", "Profile", "Event Types", "Events", "Categories", "Enrolments", "Results" };
    string TagOf(Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription api) =>
        api.ActionDescriptor.EndpointMetadata.OfType<TagsAttribute>().FirstOrDefault()?.Tags.FirstOrDefault()
        ?? api.ActionDescriptor.RouteValues["controller"] ?? "Other";
    c.TagActionsBy(api => new[] { TagOf(api) });
    c.OrderActionsBy(api =>
    {
        var index = Array.IndexOf(sectionOrder, TagOf(api));
        return $"{(index < 0 ? 99 : index):D2}_{api.RelativePath}_{api.HttpMethod}";
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Apply migrations on startup (tests use their own SQLite database)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    c.DocumentTitle = "RaceDay API";
    c.DefaultModelsExpandDepth(-1); // hide the Schemas list at the bottom
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Needed by the tests (WebApplicationFactory<Program>)
public partial class Program { }