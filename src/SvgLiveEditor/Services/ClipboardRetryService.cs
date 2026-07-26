using System.Runtime.InteropServices;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

public sealed class ClipboardRetryService
{
    public const int DefaultMaximumAttempts = 4;

    private const int ClipboardCannotOpenHResult =
        unchecked((int)0x800401D0);

    private readonly int _maximumAttempts;
    private readonly TimeSpan _retryDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public ClipboardRetryService(
        int maximumAttempts = DefaultMaximumAttempts,
        TimeSpan? retryDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        if (maximumAttempts < 1 || maximumAttempts > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        }

        _maximumAttempts = maximumAttempts;
        _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(50);
        if (_retryDelay < TimeSpan.Zero || _retryDelay > TimeSpan.FromSeconds(1))
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }

        _delayAsync = delayAsync ?? Task.Delay;
    }

    public async Task<ClipboardWriteResult> TryWriteAsync(
        Action write,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);

        for (int attempt = 1; attempt <= _maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                write();
                return ClipboardWriteResult.Success(attempt);
            }
            catch (ExternalException exception)
                when (exception.HResult == ClipboardCannotOpenHResult
                    && attempt < _maximumAttempts)
            {
                await _delayAsync(_retryDelay, cancellationToken);
            }
            catch (ExternalException exception)
            {
                return ClipboardWriteResult.Failure(
                    attempt,
                    exception.Message);
            }
            catch (Exception exception)
            {
                return ClipboardWriteResult.Failure(
                    attempt,
                    exception.Message);
            }
        }

        return ClipboardWriteResult.Failure(
            _maximumAttempts,
            "The Windows clipboard remained busy.");
    }
}
