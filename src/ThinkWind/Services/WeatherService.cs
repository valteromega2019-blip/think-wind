using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ThinkWind.Services;

internal sealed record WeatherPlace(string Name, double Latitude, double Longitude)
{ public override string ToString() => Name; }
internal sealed record WeatherReading(double Temperature, double FeelsLike, double Minimum, double Maximum, int Code, DateTimeOffset Updated);
internal static class WeatherService
{
    private static readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(10) };
    internal static async Task<List<WeatherPlace>> FindAsync(string city, CancellationToken token)
    {
        var result = new List<WeatherPlace>();
        using var response = await client.GetAsync("https://geocoding-api.open-meteo.com/v1/search?count=5&language=pt&format=json&name=" + Uri.EscapeDataString(city), token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if (!json.RootElement.TryGetProperty("results", out var cities)) return result;
        foreach (var item in cities.EnumerateArray())
        {
            string Part(string key) => item.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
            string name = string.Join(", ", new[] { Part("name"), Part("admin1"), Part("country") }.WhereNotEmpty());
            result.Add(new(name, item.GetProperty("latitude").GetDouble(), item.GetProperty("longitude").GetDouble()));
        }
        return result;
    }
    private static IEnumerable<string> WhereNotEmpty(this IEnumerable<string> values)
    { foreach (var value in values) if (value.Length > 0) yield return value; }
    internal static async Task<WeatherReading> ReadAsync(double latitude, double longitude, CancellationToken token)
    {
        string url = "https://api.open-meteo.com/v1/forecast?latitude=" + latitude.ToString(CultureInfo.InvariantCulture)
            + "&longitude=" + longitude.ToString(CultureInfo.InvariantCulture)
            + "&current=temperature_2m,apparent_temperature,weather_code&daily=temperature_2m_min,temperature_2m_max&forecast_days=1&timezone=auto";
        using var response = await client.GetAsync(url, token); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var current = json.RootElement.GetProperty("current"); var daily = json.RootElement.GetProperty("daily");
        return new(current.GetProperty("temperature_2m").GetDouble(), current.GetProperty("apparent_temperature").GetDouble(),
            daily.GetProperty("temperature_2m_min")[0].GetDouble(), daily.GetProperty("temperature_2m_max")[0].GetDouble(),
            current.GetProperty("weather_code").GetInt32(), DateTimeOffset.Now);
    }
    internal static string Description(int code) => code switch
    {
        0 => "Céu limpo", 1 => "Predomínio de sol", 2 => "Parcialmente nublado", 3 => "Nublado",
        45 or 48 => "Neblina", 51 or 53 or 55 or 56 or 57 => "Garoa",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "Chuva",
        71 or 73 or 75 or 77 or 85 or 86 => "Neve", 95 or 96 or 99 => "Trovoadas", _ => "Condições atuais"
    };
}
