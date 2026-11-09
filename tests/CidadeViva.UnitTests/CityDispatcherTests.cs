using CidadeViva.Application.City;
using CidadeViva.Infrastructure.City;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CidadeViva.UnitTests;

public sealed class CityDispatcherTests
{
    [Fact]
    public async Task StopCompletesExecutionWhenTimerIsCancelled()
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        var clock = new TimerTrackingClock();
        using var dispatcher = new CityDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CityRuntimeOptions { Enabled = true }),
            clock,
            new DisconnectedTransport(),
            NullLogger<CityDispatcher>.Instance);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await dispatcher.StartAsync(timeout.Token);
        await clock.TimerCreated.Task.WaitAsync(timeout.Token);
        await dispatcher.StopAsync(timeout.Token);

        await dispatcher.ExecuteTask!.WaitAsync(timeout.Token);
    }

    private sealed class TimerTrackingClock : TimeProvider
    {
        public TaskCompletionSource TimerCreated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            ITimer timer = System.CreateTimer(callback, state, dueTime, period);
            TimerCreated.SetResult();
            return timer;
        }
    }

    private sealed class DisconnectedTransport : ICommandTransport
    {
        public bool IsConnected => false;

        public Task PublishAsync(string deviceKey, string json, CancellationToken ct) =>
            throw new InvalidOperationException("The transport is disconnected.");
    }
}
