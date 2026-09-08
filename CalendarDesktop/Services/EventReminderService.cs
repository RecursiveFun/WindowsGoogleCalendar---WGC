using System.Collections.Concurrent;
using CalendarDesktop.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Uwp.Notifications;

namespace CalendarDesktop.Services;

/// <summary>
/// Polls local events and shows a Windows toast when one is about to start.
/// </summary>
public sealed class EventReminderService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultLeadTime = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, byte> _notifiedKeys = new();
    private readonly TimeSpan _leadTime;
    private System.Threading.Timer? _timer;
    private int _checking;

    public EventReminderService(IServiceScopeFactory scopeFactory, TimeSpan? leadTime = null)
    {
        _scopeFactory = scopeFactory;
        _leadTime = leadTime is { TotalMinutes: > 0 } ? leadTime.Value : DefaultLeadTime;
    }

    public void Start()
    {
        if (_timer != null) return;

        AppLog.Logger.Information("Event reminders started (lead={LeadMinutes} min)", _leadTime.TotalMinutes);
        _timer = new System.Threading.Timer(
            async _ => await CheckAsync(),
            null,
            TimeSpan.Zero,
            PollInterval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Stop();

    private async Task CheckAsync()
    {
        if (Interlocked.Exchange(ref _checking, 1) == 1) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var events = scope.ServiceProvider.GetRequiredService<EventService>();

            var now = DateTime.Now;
            var windowEnd = now.Add(_leadTime);
            var upcoming = await events.GetStartingBetweenAsync(now, windowEnd);

            foreach (var item in upcoming)
            {
                // Timed events only — all-day items are noisy for toast reminders.
                if (item.IsAllDay) continue;
                if (item.StartDateTime <= now) continue;

                var key = $"{item.Id}|{item.StartDateTime:O}";
                if (!_notifiedKeys.TryAdd(key, 0)) continue;

                ShowToast(item);
            }

            // Drop stale keys so memory stays bounded across long runs.
            foreach (var key in _notifiedKeys.Keys)
            {
                var parts = key.Split('|');
                if (parts.Length == 2
                    && DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out var start)
                    && start < now.AddHours(-1))
                {
                    _notifiedKeys.TryRemove(key, out _);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Logger.Warning(ex, "Event reminder check failed");
        }
        finally
        {
            Interlocked.Exchange(ref _checking, 0);
        }
    }

    private static void ShowToast(CalendarEvent item)
    {
        try
        {
            var when = item.StartDateTime.ToString("h:mm tt");
            var body = string.IsNullOrWhiteSpace(item.Location)
                ? $"Starts at {when}"
                : $"Starts at {when} · {item.Location}";

            new ToastContentBuilder()
                .AddArgument("action", "openEvent")
                .AddArgument("eventId", item.Id)
                .AddText("Upcoming event")
                .AddText(item.Title)
                .AddText(body)
                .AddButton(new ToastButton()
                    .SetContent("Dismiss")
                    .AddArgument("action", "dismiss")
                    .SetDismissActivation())
                // Reminder toasts stay visible until dismissed (not the short default popup).
                .SetToastScenario(ToastScenario.Reminder)
                .SetToastDuration(ToastDuration.Long)
                .Show(toast =>
                {
                    toast.ExpirationTime = DateTimeOffset.Now.AddHours(2);
                });

            AppLog.Logger.Information("Toast shown for event {EventId} ({Title})", item.Id, item.Title);
        }
        catch (Exception ex)
        {
            AppLog.Logger.Warning(ex, "Failed to show toast for event {EventId}", item.Id);
        }
    }
}
