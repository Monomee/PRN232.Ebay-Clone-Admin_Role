using EbayClone.Application.Users.Risk;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Services;

/// Loads the cloud IP ranges AWS and Google publish into IpIntelligence at startup and once a day, so "signed up /
/// signed in from a rented server" covers the providers' whole fleet instead of a sample. Read-only (no database):
/// every pod keeps its own copy. A failed download keeps whatever is loaded (the sample at worst).
public class DatacenterRangesService(IHttpClientFactory http, ILogger<DatacenterRangesService> logger) : BackgroundService
{
    public const string AwsUrl = "https://ip-ranges.amazonaws.com/ip-ranges.json";
    public const string GoogleCloudUrl = "https://www.gstatic.com/ipranges/cloud.json";
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RefreshAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var ranges = new List<(System.Net.IPNetwork, string)>();
        foreach (var (url, parse) in new (string, Func<string, List<(System.Net.IPNetwork, string)>>)[]
                 { (AwsUrl, IpIntelligence.ParseAwsRanges), (GoogleCloudUrl, IpIntelligence.ParseGoogleCloudRanges) })
        {
            try
            {
                var client = http.CreateClient(nameof(DatacenterRangesService));
                ranges.AddRange(parse(await client.GetStringAsync(url, ct)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning("Could not download datacenter IP ranges from {Url}: {Error}", url, ex.GetType().Name);
            }
        }
        if (ranges.Count == 0) return;
        IpIntelligence.UseDatacenterRanges(ranges);
        logger.LogInformation("Datacenter IP ranges loaded: {Count}", ranges.Count);
    }
}
