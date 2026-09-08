using System.Globalization;
using System.Windows;
using CalendarDesktop.Models;

namespace CalendarDesktop;

public partial class EventDialog : Window
{
    public CalendarEvent Event { get; }
    public bool DeleteRequested { get; private set; }

    public EventDialog(CalendarEvent calendarEvent)
    {
        InitializeComponent();
        Event = calendarEvent;

        DialogTitle.Text = calendarEvent.Id == 0 ? "New event" : "Edit event";
        DeleteButton.Visibility = calendarEvent.Id == 0 ? Visibility.Collapsed : Visibility.Visible;

        TitleBox.Text = calendarEvent.Title;
        StartDate.SelectedDate = calendarEvent.StartDateTime.Date;
        EndDate.SelectedDate = calendarEvent.EndDateTime.Date;
        StartTime.Text = calendarEvent.StartDateTime.ToString("HH:mm");
        EndTime.Text = calendarEvent.EndDateTime.ToString("HH:mm");
        AllDayBox.IsChecked = calendarEvent.IsAllDay;
        LocationBox.Text = calendarEvent.Location ?? "";
        DescriptionBox.Text = calendarEvent.Description ?? "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show(this, "Title is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (StartDate.SelectedDate == null || EndDate.SelectedDate == null)
        {
            MessageBox.Show(this, "Start and end dates are required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseTime(StartTime.Text, out var startTime) || !TryParseTime(EndTime.Text, out var endTime))
        {
            MessageBox.Show(this, "Use time format HH:mm.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var start = StartDate.SelectedDate.Value.Date.Add(startTime);
        var end = EndDate.SelectedDate.Value.Date.Add(endTime);
        if (AllDayBox.IsChecked == true)
        {
            start = StartDate.SelectedDate.Value.Date;
            end = EndDate.SelectedDate.Value.Date.AddHours(23).AddMinutes(59);
        }

        if (end < start)
        {
            MessageBox.Show(this, "End must be after start.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Event.Title = TitleBox.Text.Trim();
        Event.StartDateTime = start;
        Event.EndDateTime = end;
        Event.IsAllDay = AllDayBox.IsChecked == true;
        Event.Location = string.IsNullOrWhiteSpace(LocationBox.Text) ? null : LocationBox.Text.Trim();
        Event.Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();

        DialogResult = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Delete this event?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        DeleteRequested = true;
        DialogResult = true;
    }

    private static bool TryParseTime(string text, out TimeSpan time) =>
        TimeSpan.TryParseExact(text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out time)
        || TimeSpan.TryParseExact(text.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out time);
}
