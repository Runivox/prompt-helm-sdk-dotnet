using System;
using System.Threading;
using System.Threading.Tasks;

namespace PromptHelm.Sdk.Internal;

internal sealed class RetryPolicy
{
    private const int DefaultBaseDelayMs = 250;
    private const int DefaultMaxDelayMs = 8000;

    private readonly int _maxRetries;
    private readonly Func<Exception, bool> _isRetryable;
    private readonly Func<int, CancellationToken, Task> _delay;
    private readonly Func<double> _random;
    private readonly int _baseDelayMs;
    private readonly int _maxDelayMs;

    public RetryPolicy(
        int maxRetries,
        Func<Exception, bool> isRetryable,
        Func<int, CancellationToken, Task>? delay = null,
        Func<double>? random = null,
        int? baseDelayMs = null,
        int? maxDelayMs = null)
    {
        if (maxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries));
        }

        _maxRetries = maxRetries;
        _isRetryable = isRetryable ?? throw new ArgumentNullException(nameof(isRetryable));
        _delay = delay ?? Task.Delay;
        _random = random ?? GenerateJitter;
        _baseDelayMs = baseDelayMs ?? DefaultBaseDelayMs;
        _maxDelayMs = maxDelayMs ?? DefaultMaxDelayMs;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                return await operation(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < _maxRetries && _isRetryable(ex))
            {
                int delayMs = ComputeBackoff(attempt);
                await _delay(delayMs, cancellationToken).ConfigureAwait(false);
                attempt++;
            }
        }
    }

    internal int ComputeBackoff(int attempt)
    {
        long exponential = (long)_baseDelayMs * (1L << attempt);
        int jitter = (int)Math.Floor(_random() * 100);
        long total = exponential + jitter;
        return (int)Math.Min(total, _maxDelayMs);
    }

    private static readonly Random SharedRandom = new();

    private static double GenerateJitter()
    {
        lock (SharedRandom)
        {
            return SharedRandom.NextDouble();
        }
    }
}
