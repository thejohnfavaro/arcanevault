using System.Diagnostics;

/// <summary>
/// Clear, reusable assertions for the Windows regression runner.
/// Keep the expected behavior in the test message, not hidden in a helper.
/// </summary>
internal static class RegressionExpect
{
    internal static void That(bool condition, string behavior)
    {
        if (!condition)
            throw new InvalidOperationException("Expected: " + behavior);
    }

    internal static void Equal<T>(T actual, T expected, string behavior)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new InvalidOperationException(
                $"Expected: {behavior}. Expected value: [{expected}]; actual: [{actual}].");
    }

    internal static void Reject<TException>(Action action, string behavior)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                $"Expected: {behavior}. Rejected with {error.GetType().Name} rather than " +
                $"{typeof(TException).Name}.", error);
        }
        throw new InvalidOperationException(
            $"Expected: {behavior}. Invalid operation completed without an exception.");
    }

    internal static void Step(string name, Action action)
    {
        var watch = Stopwatch.StartNew();
        Console.WriteLine($"  CHECK {name}");
        try
        {
            action();
            Console.WriteLine($"  PASS  {name} ({watch.ElapsedMilliseconds} ms)");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"  FAIL  {name}: {error.Message}");
            throw;
        }
    }
}
