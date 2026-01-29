namespace CultBot.Services;

/// <summary>Shared signal set when the Discord client is ready; used by background services to avoid polling.</summary>
public interface IBotReadySignal
{
    Task WaitForReadyAsync(CancellationToken cancellationToken = default);
    void SetReady();
}

public class BotReadySignal : IBotReadySignal
{
    private readonly TaskCompletionSource _tcs = new();

    public void SetReady() => _tcs.TrySetResult();

    public async Task WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        if (_tcs.Task.IsCompleted)
            return;
        try
        {
            await Task.WhenAny(_tcs.Task, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);
    }
}
