using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CalendarDesktop.Models;
using CalendarDesktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CalendarDesktop;

public partial class MainWindow : Window
{
    private static readonly TimeSpan AutoSyncInterval = TimeSpan.FromMinutes(5);

    private readonly EventService _events;
    private readonly GoogleCalendarService _google;
    private readonly TrayIconService _tray;
    private readonly DispatcherTimer _autoSyncTimer;
    private DateTime _visibleMonth;
    private List<CalendarEvent> _monthEvents = [];
    private bool _syncInProgress;
    private DateTime _lastAutoSyncUtc = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        _events = App.Services.GetRequiredService<EventService>();
        _google = App.Services.GetRequiredService<GoogleCalendarService>();
        _tray = App.Services.GetRequiredService<TrayIconService>();
        _visibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        _autoSyncTimer = new DispatcherTimer { Interval = AutoSyncInterval };
        _autoSyncTimer.Tick += async (_, _) => await AutoSyncAsync(force: false);
        Activated += async (_, _) => await AutoSyncAsync(force: false);

        Loaded += async (_, _) =>
        {
            UpdateSetupButtonVisibility();
            await AutoSyncAsync(force: true);
            await RefreshAsync();
            _autoSyncTimer.Start();
        };

        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_tray.IsExitRequested)
        {
            _autoSyncTimer.Stop();
            return;
        }

        // Close hides to the notification area so reminders keep running.
        e.Cancel = true;
        _tray.HideMainWindowToTray(this);
    }

    private void UpdateSetupButtonVisibility()
    {
        // Production builds ship OAuth embedded; setup is only for rescue/dev.
        GoogleSetupButton.Visibility = _google.IsConfigured ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task AutoSyncAsync(bool force)
    {
        if (_syncInProgress) return;
        if (!force && DateTime.UtcNow - _lastAutoSyncUtc < AutoSyncInterval) return;
        if (!await _google.IsConnectedAsync()) return;

        _syncInProgress = true;
        try
        {
            FooterText.Text = "Syncing with Google…";
            var pull = await _google.PullAsync();
            _lastAutoSyncUtc = DateTime.UtcNow;
            FooterText.Text = pull.Message;
            if (pull.Success)
            {
                await RefreshAsync();
            }
            else
            {
                AppLog.Logger.Warning("Auto-sync failed: {Message}", pull.Message);
            }
        }
        finally
        {
            _syncInProgress = false;
        }
    }

    private async Task RefreshAsync()
    {
        await _events.NormalizeAllDayEndsAsync();

        MonthTitle.Text = _visibleMonth.ToString("MMMM yyyy");

        var start = _visibleMonth;
        var end = _visibleMonth.AddMonths(1).AddTicks(-1);
        _monthEvents = await _events.GetRangeAsync(start, end);
        EventList.ItemsSource = _monthEvents;

        BuildDayGrid();
        await UpdateAuthStatusAsync();
        UpdateSetupButtonVisibility();
    }

    private async Task UpdateAuthStatusAsync()
    {
        var connected = await _google.IsConnectedAsync();
        var email = await _google.GetConnectedEmailAsync();
        if (connected)
        {
            StatusText.Text = string.IsNullOrEmpty(email) ? "Google connected" : email;
            GoogleSignInButton.Content = "Sign out";
        }
        else
        {
            StatusText.Text = _google.IsConfigured ? "Not signed in" : "Google not configured";
            GoogleSignInButton.Content = "Sign in with Google";
        }
    }

    private void BuildDayGrid()
    {
        DayGrid.Children.Clear();
        var first = _visibleMonth;
        var startOffset = (int)first.DayOfWeek;
        var daysInMonth = DateTime.DaysInMonth(first.Year, first.Month);
        var cells = 42;

        for (var i = 0; i < cells; i++)
        {
            var dayNumber = i - startOffset + 1;
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                BorderThickness = new Thickness(0.5),
                Margin = new Thickness(2),
                Padding = new Thickness(4),
                CornerRadius = new CornerRadius(4),
                Background = Brushes.Transparent
            };

            var panel = new DockPanel();
            if (dayNumber >= 1 && dayNumber <= daysInMonth)
            {
                var date = new DateTime(first.Year, first.Month, dayNumber);
                if (date.Date == DateTime.Today)
                {
                    border.Background = new SolidColorBrush(Color.FromArgb(60, 255, 152, 0));
                }

                var dayLabel = new TextBlock
                {
                    Text = dayNumber.ToString(),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                DockPanel.SetDock(dayLabel, Dock.Top);
                panel.Children.Add(dayLabel);

                var dayEvents = _monthEvents
                    .Where(e => e.StartDateTime.Date <= date.Date && e.EndDateTime.Date >= date.Date)
                    .Take(3)
                    .ToList();

                var stack = new StackPanel();
                foreach (var ev in dayEvents)
                {
                    var chip = new TextBlock
                    {
                        Text = ev.Title,
                        FontSize = 11,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Foreground = Brushes.Black,
                        Background = new SolidColorBrush(Color.FromRgb(255, 183, 77)),
                        Padding = new Thickness(4, 2, 4, 2),
                        Margin = new Thickness(0, 0, 0, 2),
                        ToolTip = ev.Title,
                        Cursor = Cursors.Hand,
                        Tag = ev
                    };
                    chip.MouseLeftButtonUp += EventChip_Click;
                    stack.Children.Add(chip);
                }

                panel.Children.Add(stack);
                border.Tag = date;
                border.MouseLeftButtonDown += DayCell_Click;
                border.Cursor = Cursors.Hand;
            }
            else
            {
                border.Opacity = 0.35;
            }

            border.Child = panel;
            DayGrid.Children.Add(border);
        }
    }

    private async void EventChip_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock { Tag: CalendarEvent item }) return;
        e.Handled = true;
        await OpenEventAsync(item);
    }

    private async void DayCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: DateTime date }) return;
        if (e.ClickCount < 2) return;

        var draft = new CalendarEvent
        {
            Title = "",
            StartDateTime = date.Date.AddHours(9),
            EndDateTime = date.Date.AddHours(10)
        };
        await OpenEventAsync(draft, isNew: true);
    }

    private async void NewEvent_Click(object sender, RoutedEventArgs e)
    {
        var draft = new CalendarEvent
        {
            Title = "",
            StartDateTime = DateTime.Now.AddMinutes(30),
            EndDateTime = DateTime.Now.AddHours(1.5)
        };
        await OpenEventAsync(draft, isNew: true);
    }

    private async void EventList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (EventList.SelectedItem is CalendarEvent selected)
        {
            await OpenEventAsync(selected);
        }
    }

    private async Task OpenEventAsync(CalendarEvent item, bool isNew = false)
    {
        EventDialogMode mode = isNew
            ? new CreateEventMode()
            : new ViewEventMode();

        while (true)
        {
            var dialog = new EventDialog(item, mode) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (dialog.EditRequested)
            {
                mode = new EditEventMode();
                continue;
            }

            if (dialog.DeleteRequested && !isNew)
            {
                await _google.DeleteGoogleEventAsync(item.GoogleEventId);
                await _events.DeleteAsync(item.Id);
                FooterText.Text = "Event deleted.";
                await RefreshAsync();
                return;
            }

            if (mode.IsReadOnly)
            {
                return;
            }

            CalendarEvent saved;
            if (isNew)
            {
                saved = await _events.CreateAsync(dialog.Event);
            }
            else
            {
                saved = await _events.UpdateAsync(dialog.Event);
            }

            if (await _google.IsConnectedAsync())
            {
                FooterText.Text = "Syncing to Google Calendar…";
                var push = await _google.PushEventAsync(saved);
                FooterText.Text = push.Message;
                if (!push.Success)
                {
                    MessageBox.Show(this, push.Message, "Google sync", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                FooterText.Text = isNew
                    ? "Event saved locally. Sign in with Google to sync."
                    : "Event updated locally. Sign in with Google to sync.";
            }

            await RefreshAsync();
            return;
        }
    }

    private void GoogleSetup_Click(object sender, RoutedEventArgs e)
    {
        var setup = new GoogleSetupWindow { Owner = this };
        if (setup.ShowDialog() == true)
        {
            _google.ReloadOptionsFromDisk();
            FooterText.Text = "Google OAuth settings saved. Try Sign in with Google.";
            UpdateSetupButtonVisibility();
            _ = UpdateAuthStatusAsync();
        }
    }

    private async void GoogleSignIn_Click(object sender, RoutedEventArgs e)
    {
        GoogleSignInButton.IsEnabled = false;
        try
        {
            if (await _google.IsConnectedAsync())
            {
                await _google.SignOutAsync();
                FooterText.Text = "Signed out of Google.";
            }
            else
            {
                FooterText.Text = "Opening Google sign-in in your browser…";
                var result = await Task.Run(async () => await _google.SignInAsync());
                FooterText.Text = result.Message;
                if (result.Success)
                {
                    var pull = await _google.PullAsync();
                    _lastAutoSyncUtc = DateTime.UtcNow;
                    FooterText.Text = pull.Message;
                }
                else if (!_google.IsConfigured)
                {
                    var openSetup = MessageBox.Show(
                        this,
                        result.Message + "\n\nOpen Advanced Google setup now?",
                        "Google sign-in",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (openSetup == MessageBoxResult.Yes)
                    {
                        GoogleSetup_Click(sender, e);
                    }
                }
                else
                {
                    MessageBox.Show(this, result.Message, "Google sign-in", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            await RefreshAsync();
        }
        finally
        {
            GoogleSignInButton.IsEnabled = true;
        }
    }

    private async void PullGoogle_Click(object sender, RoutedEventArgs e)
    {
        FooterText.Text = "Syncing with Google…";
        var result = await _google.PullAsync();
        _lastAutoSyncUtc = DateTime.UtcNow;
        FooterText.Text = result.Message;
        if (!result.Success)
        {
            MessageBox.Show(this, result.Message, "Google sync", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await RefreshAsync();
    }

    private async void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = _visibleMonth.AddMonths(-1);
        await RefreshAsync();
    }

    private async void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = _visibleMonth.AddMonths(1);
        await RefreshAsync();
    }

    private async void Today_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await RefreshAsync();
    }
}
