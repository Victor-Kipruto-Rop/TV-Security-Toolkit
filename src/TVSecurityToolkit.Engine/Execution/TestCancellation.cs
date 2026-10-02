namespace TVSecurityToolkit.Engine.Execution;

public static class TestCancellation
{
    public static CancellationTokenSource WithTimeout(CancellationToken outer, TimeSpan timeout)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(outer);
        cts.CancelAfter(timeout);
        return cts;
    }
}
