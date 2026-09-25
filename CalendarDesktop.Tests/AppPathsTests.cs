using CalendarDesktop.Services;

namespace CalendarDesktop.Tests;

public class AppPathsTests
{
    [Fact]
    public void Paths_UseWgcFolderAndExpectedFileNames()
    {
        Assert.Equal("WGC", AppPaths.AppFolderName);
        Assert.EndsWith($"{Path.DirectorySeparatorChar}WGC{Path.DirectorySeparatorChar}wgc.db", AppPaths.DatabasePath);
        Assert.EndsWith($"{Path.DirectorySeparatorChar}WGC{Path.DirectorySeparatorChar}logs", AppPaths.LogsDirectory);
        Assert.EndsWith($"{Path.DirectorySeparatorChar}WGC{Path.DirectorySeparatorChar}GoogleAuth", AppPaths.GoogleAuthDirectory);
        Assert.EndsWith($"{Path.DirectorySeparatorChar}WGC{Path.DirectorySeparatorChar}google-oauth.json", AppPaths.GoogleOAuthPath);
        Assert.EndsWith($"{Path.DirectorySeparatorChar}WGC{Path.DirectorySeparatorChar}credentials.json", AppPaths.CredentialsJsonPath);
    }
}
