# WGC production guide

## Feels-production checklist

| Item | Status |
|---|---|
| Embed Desktop OAuth (users just Sign in) | Done via `scripts/publish.ps1` |
| Hide Advanced Google setup when configured | Done |
| Version + app icon | `1.0.0` + `CalendarDesktop/Assets/app.ico` |
| Release self-contained publish | `scripts/publish.ps1` |
| Logging | `%LocalAppData%\WGC\logs\` |
| Auto-sync (startup, every 5 min, on focus) | Done |
| Tray + toast reminders | Done |
| Google consent screen Production | **You** do this in Google Cloud Console |
| Authenticode signing | Optional: set `WGC_SIGN_THUMBPRINT` |

## Publish a build

1. Make sure you have already signed in once (so `%LocalAppData%\WGC\google-oauth.json` exists), **or** place a Google Desktop `credentials.json` there.
2. From the repo root:

```powershell
.\scripts\publish.ps1
```

3. Output:
   - `artifacts\WGC-1.0.0-win-x64\` — runnable folder
   - `artifacts\WGC-1.0.0-win-x64.zip` — shareable zip

Optional installer (requires [Inno Setup](https://jrsoftware.org/isinfo.php)):

```powershell
.\scripts\publish.ps1
# then compile scripts\WGC.iss in Inno Setup
```

Optional signing:

```powershell
$env:WGC_SIGN_THUMBPRINT = "YOUR_CERT_SHA1_THUMBPRINT"
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
2. Run `WGC.exe`.
3. Click **Sign in with Google** once.
4. Create/edit/delete events — they sync to Google automatically.
5. Deletes on Google disappear here on auto-sync (startup / focus / every 5 minutes) or **Sync with Google**.
6. Closing the window keeps WGC in the tray for reminders; use tray **Exit** to quit.

## Logs & data

- Logs: `%LocalAppData%\WGC\logs\`
- Local DB: `%LocalAppData%\WGC\wgc.db`
- Google tokens: `%LocalAppData%\WGC\GoogleAuth\`

Legacy `%LocalAppData%\CalendarApp` is migrated automatically on first run after the rename.
