# CalendarApp production guide

## Feels-production checklist

| Item | Status |
|---|---|
| Embed Desktop OAuth (users just Sign in) | Done via `scripts/publish.ps1` |
| Hide Advanced Google setup when configured | Done |
| Version + app icon | `1.0.0` + `CalendarDesktop/Assets/app.ico` |
| Release self-contained publish | `scripts/publish.ps1` |
| Logging | `%LocalAppData%\CalendarApp\logs\` |
| Auto-sync (startup, every 5 min, on focus) | Done |
| Google consent screen Production | **You** do this in Google Cloud Console |
| Authenticode signing | Optional: set `CALENDARAPP_SIGN_THUMBPRINT` |

## Publish a build

1. Make sure you have already signed in once (so `%LocalAppData%\CalendarApp\google-oauth.json` exists), **or** place a Google Desktop `credentials.json` there.
2. From the repo root:

```powershell
.\scripts\publish.ps1
```

3. Output:
   - `artifacts\CalendarApp-1.0.0-win-x64\` — runnable folder
   - `artifacts\CalendarApp-1.0.0-win-x64.zip` — shareable zip

Optional installer (requires [Inno Setup](https://jrsoftware.org/isinfo.php)):

```powershell
.\scripts\publish.ps1
# then compile scripts\CalendarApp.iss in Inno Setup
```

Optional signing:

```powershell
$env:CALENDARAPP_SIGN_THUMBPRINT = "YOUR_CERT_SHA1_THUMBPRINT"
.\scripts\publish.ps1
```

## Google Cloud (required for other users)

1. OAuth client type must stay **Desktop app**.
2. Enable **Google Calendar API**.
3. Consent screen:
   - **Private / just you:** leave Publishing status as Testing and add test users.
   - **Other people:** set Publishing status to **In production**. Google may require verification for Calendar scopes.
4. Ship the Client ID/Secret only through the publish script (embedded into `appsettings.json` in the package). Do not commit secrets to git.

## End-user experience

1. Install or unzip the Release package.
2. Run `CalendarApp.exe`.
3. Click **Sign in with Google** once.
4. Create/edit/delete events — they sync to Google automatically.
5. Deletes on Google disappear here on auto-sync (startup / focus / every 5 minutes) or **Sync with Google**.

## Logs & data

- Logs: `%LocalAppData%\CalendarApp\logs\`
- Local DB: `%LocalAppData%\CalendarApp\calendarapp.db`
- Google tokens: `%LocalAppData%\CalendarApp\GoogleAuth\`
