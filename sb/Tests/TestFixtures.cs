namespace sb.Tests;

/// <summary>Locates the real-song fixture files under Tests/TestFiles/ at test run time.</summary>
internal static class TestFixtures
{
    public static string Path(params string[] relativeParts)
    {
        var parts = new[] { AppContext.BaseDirectory, "Tests", "TestFiles" }.Concat(relativeParts).ToArray();
        return System.IO.Path.Combine(parts);
    }
}
