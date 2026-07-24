using SvgLiveEditor.Services;

namespace SvgLiveEditor.Tests;

[TestClass]
public sealed class AsyncDebouncerTests
{
    [TestMethod]
    public async Task DebounceAsync_RunsOnlyTheLatestPendingCallback()
    {
        using AsyncDebouncer debouncer = new(TimeSpan.FromMilliseconds(40));
        List<int> calls = [];

        Task first = debouncer.DebounceAsync(_ =>
        {
            calls.Add(1);
            return Task.CompletedTask;
        });
        Task second = debouncer.DebounceAsync(_ =>
        {
            calls.Add(2);
            return Task.CompletedTask;
        });
        Task third = debouncer.DebounceAsync(_ =>
        {
            calls.Add(3);
            return Task.CompletedTask;
        });

        await Task.WhenAll(first, second, third);

        CollectionAssert.AreEqual(new[] { 3 }, calls);
    }
}
