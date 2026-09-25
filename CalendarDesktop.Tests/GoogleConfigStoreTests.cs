using CalendarDesktop.Services;

namespace CalendarDesktop.Tests;

public class GoogleConfigStoreTests
{
    [Fact]
    public void IsUsable_ReturnsFalse_ForNullOrEmpty()
    {
        Assert.False(GoogleConfigStore.IsUsable(null));
        Assert.False(GoogleConfigStore.IsUsable(new GoogleCalendarOptions()));
        Assert.False(GoogleConfigStore.IsUsable(new GoogleCalendarOptions
        {
            ClientId = "abc",
            ClientSecret = ""
        }));
    }

    [Fact]
    public void IsUsable_ReturnsFalse_ForPlaceholderClientId()
    {
        Assert.False(GoogleConfigStore.IsUsable(new GoogleCalendarOptions
        {
            ClientId = "YOUR_CLIENT_ID",
            ClientSecret = "secret"
        }));
    }

    [Fact]
    public void IsUsable_ReturnsTrue_ForValidPair()
    {
        Assert.True(GoogleConfigStore.IsUsable(new GoogleCalendarOptions
        {
            ClientId = "123.apps.googleusercontent.com",
            ClientSecret = "GOCSPX-test"
        }));
    }
}
