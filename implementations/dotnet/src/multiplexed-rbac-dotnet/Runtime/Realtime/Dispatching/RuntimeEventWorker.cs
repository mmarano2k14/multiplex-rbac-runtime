using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MultiplexedRbac.Runtime.Realtime.Events;
using MultiplexedRbac.Runtime.Realtime.Events.Abstractions;

namespace MultiplexedRbac.Runtime.Realtime.Dispatching;

/// <summary>
/// Background worker responsible for consuming runtime events from the channel
/// and dispatching them to handlers outside of the request hot path.
///
/// This worker must never break the host startup or shutdown flow.
/// Cancellation is treated as a normal lifecycle event.
/// </summary>
public sealed class RuntimeEventWorker : BackgroundService
{
    private readonly ChannelReader<IRuntimeEvent> _reader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RuntimeEventWorker> _logger;

    public RuntimeEventWorker(
        Channel<IRuntimeEvent> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<RuntimeEventWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _reader = channel.Reader;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Continuously reads runtime events from the channel and dispatches them
    /// to registered handlers in a background loop.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                while (_reader.TryRead(out var runtimeEvent))
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();

                        var handlerDispatcher = scope.ServiceProvider
                            .GetRequiredService<IRuntimeEventHandlerDispatcher>();

                        await handlerDispatcher
                            .DispatchAsync(runtimeEvent, stoppingToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Normal shutdown path.
                        return;
                    }
                    catch (Exception ex)
                    {
                        // Realtime/observability must never crash the worker loop.
                        _logger.LogError(
                            ex,
                            "Unhandled exception while processing runtime event {EventType}.",
                            runtimeEvent.GetType().FullName);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            // Catch any unexpected startup/runtime issue so the background service
            // does not fail silently or destabilize the application host.
            _logger.LogError(ex, "Runtime event worker terminated unexpectedly.");
        }
    }
}