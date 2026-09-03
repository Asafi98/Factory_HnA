namespace HusnaFactory.Services;

/// <summary>
/// Delays an async action until the caller stops invoking it for <paramref name="delayMs"/>.
/// Used by search-as-you-type inputs so each keystroke only updates the bound text
/// immediately, while the expensive DB-backed search fires once after typing pauses.
/// One instance per input; not shared across components/users.
/// </summary>
public class Debouncer
{
    private CancellationTokenSource? _cts;

    public async Task DebounceAsync(Func<Task> action, int delayMs = 300)
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            await Task.Delay(delayMs, cts.Token);
            await action();
        }
        catch (TaskCanceledException)
        {
            // superseded by a newer keystroke — ignore
        }
    }
}
