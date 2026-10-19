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

// ---------- Database (EF Core, code-first, SQL Server) ----------
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));

// ---------- Services ----------
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddHttpClient<IWeatherService, OpenMeteoWeatherService>(client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ---------- Authentication: JWT bearer tokens ----------
// The token is the user's session: it is issued at login, sent with every request in the
// Authorization header, and carries the user's id and role until it expires.
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be set and be at least 32 characters long.");

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
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "API for the RaceDay road running, walking and cycling event system (PROG6212 POE Part 2). " +
                      "Log in with POST /api/auth/login, click **Authorize** and paste the token. " +
                      "Sample accounts (password `Password123!`): organiser `thandiwe.nkosi@raceday.co.za`, " +
                      "participant `sipho.mahlangu@gmail.com`."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Paste the token from /api/auth/login (without the word Bearer)."
    });
    c.OperationFilter<AuthorizationOperationFilter>();

    // Group endpoints by the sections of the Part 1 plan, in the same order
    c.TagActionsBy(api =>
    {
        var tag = api.ActionDescriptor.EndpointMetadata.OfType<TagsAttribute>().FirstOrDefault()?.Tags.FirstOrDefault();
        return new[] { tag ?? api.ActionDescriptor.RouteValues["controller"] ?? "Other" };
    });
    c.OrderActionsBy(api =>
    {
        var tag = api.ActionDescriptor.EndpointMetadata.OfType<TagsAttribute>().FirstOrDefault()?.Tags.FirstOrDefault() ?? "Z";
        return $"{tag}_{api.RelativePath}_{api.HttpMethod}";
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Create/upgrade the database from the EF Core migrations when the app starts.
// The test project runs with the "Testing" environment and builds its own SQLite database instead.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.Migrate();
}

// Swagger is on in every environment so the endpoints can always be tested in a browser
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    c.DocumentTitle = "RaceDay API";
    c.DisplayRequestDuration();
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Lets the test project start the API in memory with WebApplicationFactory<Program>
public partial class Program { }
