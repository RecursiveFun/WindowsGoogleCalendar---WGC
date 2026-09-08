namespace CalendarDesktop;

/// <summary>
/// Polymorphic dialog behavior: view (read-only), edit, or create.
/// </summary>
public abstract class EventDialogMode
{
    public abstract string WindowTitle { get; }
    public abstract bool IsReadOnly { get; }
    public abstract bool ShowEditButton { get; }
    public abstract bool ShowSaveButton { get; }
    public abstract bool ShowDeleteButton { get; }
}

public sealed class ViewEventMode : EventDialogMode
{
    public override string WindowTitle => "Event details";
    public override bool IsReadOnly => true;
    public override bool ShowEditButton => true;
    public override bool ShowSaveButton => false;
    public override bool ShowDeleteButton => true;
}

public sealed class EditEventMode : EventDialogMode
{
    public override string WindowTitle => "Edit event";
    public override bool IsReadOnly => false;
    public override bool ShowEditButton => false;
    public override bool ShowSaveButton => true;
    public override bool ShowDeleteButton => true;
}

public sealed class CreateEventMode : EventDialogMode
{
    public override string WindowTitle => "New event";
    public override bool IsReadOnly => false;
    public override bool ShowEditButton => false;
    public override bool ShowSaveButton => true;
    public override bool ShowDeleteButton => false;
}
