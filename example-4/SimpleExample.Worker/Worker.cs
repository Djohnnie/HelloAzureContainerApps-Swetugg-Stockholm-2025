using Microsoft.Extensions.Logging;
using System.Text;

namespace SimpleExample.Worker;

public class Worker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IConfiguration configuration,
        ILogger<Worker> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var statusEndpointUri = _configuration.GetValue<string>("STATUS_ENDPOINT_URI");
        var idleTime = _configuration.GetValue<int>("IDLE_TIME_IN_SECONDS");
        var repeats = _configuration.GetValue<int>("REPEATS");
        var count = 0;
        var waitForIt = 1;
        var shouldIncrease = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            var results = new Dictionary<string, int>();

            var tasks = Enumerable.Range(0, count == 0 ? 1 : count).Select(x => Task.Run(async () =>
            {
                using var httpClient = new HttpClient();
                return await httpClient.GetStringAsync(statusEndpointUri, stoppingToken);
            }));

            await Task.WhenAll(tasks);

            foreach (var task in tasks)
            {
                var result = task.Result.Substring(task.Result.Length - 32, 5);
                if (results.ContainsKey(result))
                {
                    results[result]++;
                }
                else
                {
                    results.Add(result, 1);
                }
            }

            _logger.LogInformation($"{(count == 0 ? 1 : count)} parallel requests have been sent to the API [{waitForIt}/{repeats}]", DateTimeOffset.Now);

            var logBuilder = new StringBuilder();
            foreach (var result in results.OrderByDescending(x => x.Value))
            {
                logBuilder.Append($"{result.Value}*'{result.Key}' ");
            }

            _logger.LogInformation(logBuilder.ToString());

            waitForIt++;

            if (waitForIt > repeats)
            {
                waitForIt = 1;

                count += shouldIncrease ? 10 : -10;

                if (count > 110)
                {
                    shouldIncrease = false;
                    count = 90;
                }

                if (count < 0)
                {
                    shouldIncrease = true;
                    count = 10;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(idleTime));
        }
    }
}