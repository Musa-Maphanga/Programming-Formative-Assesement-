using System.Globalization;
using System.Text.Json;

namespace RaceDay.Api.Services;

public record DailyForecast(string Summary, decimal? MinTempC, decimal? MaxTempC, int? RainChancePercent, decimal? MaxWindKmh);

/// <summary>Thrown when the external weather service is down or returns something unusable (maps to 502).</summary>
public class WeatherServiceUnavailableException : Exception
{
    public WeatherServiceUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

public interface IWeatherService
{
    /// <summary>Gets the forecast for one day at a location.</summary>
    Task<DailyForecast> GetDailyForecastAsync(decimal latitude, decimal longitude, DateOnly date, CancellationToken ct = default);
}

/// <summary>
/// Uses the free Open-Meteo forecast API (no API key needed). Open-Meteo only forecasts about
/// 16 days ahead, so the controller checks the date before calling this.
/// </summary>
public class OpenMeteoWeatherService : IWeatherService
{
    private readonly HttpClient _http;

    public OpenMeteoWeatherService(HttpClient http)
    {
        _http = http;
    }

    public async Task<DailyForecast> GetDailyForecastAsync(decimal latitude, decimal longitude, DateOnly date, CancellationToken ct = default)
    {
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = "v1/forecast"
                  + $"?latitude={latitude.ToString(CultureInfo.InvariantCulture)}"
                  + $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}"
                  + "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,wind_speed_10m_max"
                  + $"&timezone=Africa%2FJohannesburg&start_date={day}&end_date={day}";

        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                throw new WeatherServiceUnavailableException($"Weather service returned {(int)response.StatusCode}.");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var daily = doc.RootElement.GetProperty("daily");

            int? code = FirstInt(daily, "weather_code");
            return new DailyForecast(
                Summary: DescribeWeatherCode(code),
                MinTempC: FirstDecimal(daily, "temperature_2m_min"),
                MaxTempC: FirstDecimal(daily, "temperature_2m_max"),
                RainChancePercent: FirstInt(daily, "precipitation_probability_max"),
                MaxWindKmh: FirstDecimal(daily, "wind_speed_10m_max"));
        }
        catch (WeatherServiceUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new WeatherServiceUnavailableException("Could not get the forecast from the weather service.", ex);
        }
    }

    private static decimal? FirstDecimal(JsonElement daily, string name)
    {
        if (!daily.TryGetProperty(name, out var arr) || arr.GetArrayLength() == 0) return null;
        var v = arr[0];
        return v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
    }

    private static int? FirstInt(JsonElement daily, string name)
    {
        var d = FirstDecimal(daily, name);
        return d.HasValue ? (int)Math.Round(d.Value) : null;
    }

    // WMO weather codes used by Open-Meteo
    private static string DescribeWeatherCode(int? code) => code switch
    {
        null => "Unknown",
        0 => "Clear sky",
        1 or 2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        51 or 53 or 55 or 56 or 57 => "Drizzle",
        61 or 63 or 65 or 66 or 67 => "Rain",
        71 or 73 or 75 or 77 => "Snow",
        80 or 81 or 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 or 96 or 99 => "Thunderstorms",
        _ => "Mixed conditions"
    };
}
