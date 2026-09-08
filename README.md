# WindowsGoogleCalendar---WGC

Native Windows desktop calendar with two-way Google Calendar sync.

Built with **WPF** (.NET 9), **Material Design**, **SQLite**, and the **Google Calendar API**.

---

## Features

- Month view with event list and create / edit / delete
- Sign in with Google (Desktop OAuth)
- Push local creates, updates, and deletes to Google Calendar
- Pull / auto-sync from Google (startup, every 5 minutes, and when the window is focused)
- Removes local events that were deleted in Google Calendar
- Dark + orange Material Design UI
- Local SQLite storage under `%LocalAppData%\CalendarApp`

---

## Requirements

- Windows 10/11 (x64)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (to build)
- A Google Cloud project with:
  - **Google Calendar API** enabled
  - OAuth client type **Desktop app** (not Web)

---

## Quick start (development)

```powershell
cd "E:\Coding and Development\Bot\DotNetApp"
dotnet restore CalendarApp.sln
dotnet run --project CalendarDesktop\CalendarDesktop.csproj
```

Or open `CalendarApp.sln` in Visual Studio and press F5.

### First-time Google setup (dev)

1. In [Google Cloud Console](https://console.cloud.google.com/):
   - Enable **Google Calendar API**
   - Create OAuth client → Application type: **Desktop app**
   - Copy **Client ID** and **Client Secret**
2. In the app, if OAuth is not embedded yet, use **Advanced Google setup** and paste those values.
3. Click **Sign in with Google** and finish the browser consent flow.
4. If the consent screen is **Testing**, add your Gmail as a test user.

Credentials are stored at:

`%LocalAppData%\CalendarApp\google-oauth.json`

---

## Using the app

| Action | How |
|---|---|
| Change month | ◀ / ▶ or **Today** |
| New event | **New event**, or double-click a day |
| Edit / delete event | Double-click an event in the list |
| Connect Google | **Sign in with Google** |
| Force sync | **Sync with Google** |

When signed in:

- New/updated events are pushed to Google automatically
- Google deletions are removed locally on the next sync

---

## Project layout

```
DotNetApp/
├── CalendarApp.sln
├── CalendarDesktop/          # WPF app
│   ├── Assets/app.ico
│   ├── Services/             # Events + Google Calendar
│   ├── Data/                 # EF Core + SQLite
│   ├── Models/
│   └── MainWindow.xaml
├── scripts/
│   ├── publish.ps1           # Release package builder
│   └── CalendarApp.iss       # Optional Inno Setup installer
├── artifacts/                # Publish output (generated)
├── PRODUCTION.md             # Production checklist & details
└── archive/                  # Older web / WebView experiments
```

---

## Configuration

### Bundled settings

`CalendarDesktop/appsettings.json` ships empty in source:

```json
{
  "GoogleCalendar": {
    "ClientId": "",
    "ClientSecret": ""
  }
}
```

Load order at runtime:

1. Bundled `appsettings.json` next to the EXE (preferred for production builds)
2. `%LocalAppData%\CalendarApp\credentials.json` (Google Desktop download)
3. `%LocalAppData%\CalendarApp\google-oauth.json` (Advanced setup)

Do **not** commit real Client secrets to git.

### Local data

| Path | Purpose |
|---|---|
| `%LocalAppData%\CalendarApp\calendarapp.db` | Local events + connection state |
| `%LocalAppData%\CalendarApp\GoogleAuth\` | OAuth tokens |
| `%LocalAppData%\CalendarApp\logs\` | Rolling Serilog logs |
| `%LocalAppData%\CalendarApp\google-oauth.json` | Dev / override OAuth client |

---

## Production build

From the repo root:

```powershell
.\scripts\publish.ps1
```

This will:

1. Publish a **self-contained** `win-x64` single-file Release build
2. Embed OAuth from your local `google-oauth.json` into the package
3. Create:
   - `artifacts\CalendarApp-1.0.0-win-x64\`
   - `artifacts\CalendarApp-1.0.0-win-x64.zip`

End users unzip (or install) and click **Sign in with Google** — no setup dialog.

### Optional installer

1. Run `.\scripts\publish.ps1`
2. Compile `scripts\CalendarApp.iss` with [Inno Setup](https://jrsoftware.org/isinfo.php)

### Optional Authenticode signing

This is a **Windows code-signing certificate** thumbprint, not an OAuth value:

```powershell
$env:CALENDARAPP_SIGN_THUMBPRINT = "YOUR_CERT_SHA1_THUMBPRINT"
.\scripts\publish.ps1
```

Find the thumbprint in `certmgr.msc` → Personal → Certificates → Details → Thumbprint, or:

```powershell
Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.HasPrivateKey } |
  Format-List Subject, Thumbprint, NotAfter
```

Without signing, Windows may show an “unknown publisher” / SmartScreen warning.

### Google Cloud for other users

| Audience | Consent screen |
|---|---|
| Just you / small team | Keep **Testing** and add test users |
| Public / many users | Set status to **In production** (verification may be required for Calendar scopes) |

Keep the OAuth client type as **Desktop app**.

More detail: [PRODUCTION.md](PRODUCTION.md)

---

## Troubleshooting

| Symptom | What to check |
|---|---|
| “This app’s request is invalid” | OAuth client must be **Desktop app**, not Web |
| Sign-in works for you but not others | Consent screen still **Testing** — add them as test users or publish |
| Events don’t appear in Google | Confirm status shows your email (signed in); check footer / logs |
| Deleted Google events still show | Wait for auto-sync, click **Sync with Google**, or restart the app |
| Crash / sync errors | `%LocalAppData%\CalendarApp\logs\` |

---

## Tech stack

- .NET 9 / WPF
- MaterialDesignThemes
- Entity Framework Core + SQLite
- Google.Apis.Calendar.v3 + Google.Apis.Auth
- Serilog

---

## License

Private project — add a license here if you distribute it.
