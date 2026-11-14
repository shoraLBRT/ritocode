using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Ritocode.Api.Feedback;

/// <summary>
/// At most <see cref="FeedbackOptions.MaxPerWindow"/> messages per address in any sliding
/// <see cref="FeedbackOptions.Window"/>. Kept in memory: a restart forgets it, and a second API instance
/// would keep its own count. One instance runs (ADR 0011); the limit stops a script, not a determined
/// sender, and the captcha does the rest.
/// </summary>
internal sealed class FeedbackRateLimiter(IOptions<FeedbackOptions> options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _sent = new(StringComparer.Ordinal);

    /// <summary>Counts one message from <paramref name="address"/>, or says there is no room for it.</summary>
    public bool TryAcquire(string address)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow();
        var windowStart = now - settings.Window;
        var times = _sent.GetOrAdd(address, _ => new Queue<DateTimeOffset>());

        lock (times)
        {
            while (times.Count > 0 && times.Peek() <= windowStart)
            {
                times.Dequeue();
            }

            if (times.Count >= settings.MaxPerWindow)
            {
                return false;
            }

            times.Enqueue(now);
        }

        // Addresses that have gone quiet are dropped now and then, so the map does not grow forever.
        if (_sent.Count > 10_000)
        {
            foreach (var (key, queue) in _sent)
            {
                lock (queue)
                {
                    if (queue.Count == 0 || queue.Last() <= windowStart)
                    {
                        _sent.TryRemove(key, out _);
                    }
                }
            }
        }

        return true;
    }
}
