namespace CalendarDesktop.Tests;

public class EventDialogModeTests
{
    [Fact]
    public void ViewEventMode_IsReadOnly_WithEditAndDelete()
    {
        EventDialogMode mode = new ViewEventMode();

        Assert.Equal("Event details", mode.WindowTitle);
        Assert.True(mode.IsReadOnly);
        Assert.True(mode.ShowEditButton);
        Assert.False(mode.ShowSaveButton);
        Assert.True(mode.ShowDeleteButton);
    }

    [Fact]
    public void EditEventMode_IsWritable_WithSaveAndDelete()
    {
        EventDialogMode mode = new EditEventMode();

        Assert.Equal("Edit event", mode.WindowTitle);
        Assert.False(mode.IsReadOnly);
        Assert.False(mode.ShowEditButton);
        Assert.True(mode.ShowSaveButton);
        Assert.True(mode.ShowDeleteButton);
    }

    [Fact]
    public void CreateEventMode_IsWritable_WithoutDelete()
    {
        EventDialogMode mode = new CreateEventMode();

        Assert.Equal("New event", mode.WindowTitle);
        Assert.False(mode.IsReadOnly);
        Assert.False(mode.ShowEditButton);
        Assert.True(mode.ShowSaveButton);
        Assert.False(mode.ShowDeleteButton);
    }

    [Fact]
    public void Modes_ArePolymorphicThroughBaseType()
    {
        EventDialogMode[] modes =
        [
            new ViewEventMode(),
            new EditEventMode(),
            new CreateEventMode()
        ];

        Assert.Equal(3, modes.Select(m => m.WindowTitle).Distinct().Count());
        Assert.Contains(modes, m => m.IsReadOnly);
        Assert.Equal(2, modes.Count(m => !m.IsReadOnly));
    }
}
