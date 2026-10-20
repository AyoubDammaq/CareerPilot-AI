using System.Threading.Channels;
using CareerPilot.Application.Jobs;

namespace CareerPilot.API.Jobs;

/// <summary>In-process queue of search runs waiting to execute (single consumer).</summary>
public sealed class SearchRunQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid runId) => _channel.Writer.TryWrite(runId);
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>Executes queued search runs one at a time, so an HTTP request never waits for a whole search.</summary>
public sealed class SearchRunWorker(SearchRunQueue queue, IServiceScopeFactory scopes, ILogger<SearchRunWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using (var scope = scopes.CreateScope())
                await scope.ServiceProvider.GetRequiredService<JobSearchService>().RecoverInterruptedAsync(stoppingToken);

            await foreach (var runId in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<JobSearchService>().ExecuteAsync(runId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Search run {RunId} crashed the worker loop iteration", runId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // normal shutdown
        }
    }
}
