using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;

namespace Test.Vault.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;

    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }

    
    [HttpGet("OutgoingHttp")]
    public async Task OutgoingHttp([FromQuery]string url)
    {
        var client = new HttpClient();
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
    }

    [HttpGet("Metrictest")]
    public IActionResult Metrictest()
    {
        Meter s_meter = new Meter("Test.Counter", "1.0.0");
        Counter<int> s_hatsSold = s_meter.CreateCounter<int>("hits");
        s_hatsSold.Add(1);
        //var routeTemplate = httpContext.GetMetricsCurrentRouteName();
        return Ok("test");
    }
}

