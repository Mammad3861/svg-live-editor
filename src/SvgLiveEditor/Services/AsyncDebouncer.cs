namespace SvgLiveEditor.Services;

public sealed class AsyncDebouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly object _gate = new();
    private CancellationTokenSource? _currentCancellation;
    private bool _isDisposed;

    public AsyncDebouncer(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        _delay = delay;
    }

    public Task DebounceAsync(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        CancellationTokenSource cancellation;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _currentCancellation?.Cancel();
            cancellation = new CancellationTokenSource();
            _currentCancellation = cancellation;
        }

        return RunAsync(action, cancellation);
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _currentCancellation?.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _currentCancellation?.Cancel();
            _currentCancellation?.Dispose();
            _currentCancellation = null;
        }
    }

    private async Task RunAsync(
        Func<CancellationToken, Task> action,
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(_delay, cancellation.Token).ConfigureAwait(false);
            await action(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer edit replaced this pending callback.
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_currentCancellation, cancellation))
                {
                    _currentCancellation = null;
                }
            }

            cancellation.Dispose();
        }
    }
}
