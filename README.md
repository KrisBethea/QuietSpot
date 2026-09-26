# QuietSpot

Crowdsourced "how busy is it right now" for introverts. Find quiet cafés,
libraries, and gyms near you — powered by one-tap reports from people who are
already there.

This scaffold implements the product we sketched: the home feed ranked by
quietness, venue detail with one-tap reporting, the confidence-weighted scoring
blend, staged permission asks, and the geofence-dwell report prompt.

## Solution layout

```
src/
  QuietSpot.Shared/      DTOs + the scoring engine (runs on client AND server)
    Scoring/QuietnessScoring.cs   <- the blend formula lives here, once
  QuietSpot.Api/         ASP.NET Core minimal API
    Program.cs                    <- all endpoints
    Data/                         <- EF Core entities (6-table schema) + seed
    Services/ScoringService.cs    <- recompute + cache, baselines, credibility
    Services/ScoreSweepService.cs <- 10-min decay sweep + quiet-now alert hook
  QuietSpot.App/         .NET MAUI app (net10.0-android / net10.0-ios)
    ViewModels/                   <- CommunityToolkit.Mvvm
    Views/                        <- Feed, VenueDetail, Onboarding
    Services/                     <- ApiClient, offline ReportQueue, Session,
                                     GeofenceSyncService (fence shuffling)
    Delegates/                    <- Shiny geofence + notification delegates
```

## Prerequisites

- .NET 10 SDK with MAUI workloads: `dotnet workload install maui`
- Android: Android SDK / emulator (API 26+). iOS: Xcode + simulator (macOS).

## Run it

**1. Start the API** (uses a local SQLite file when no Postgres connection
string is set, and seeds ~10 Minneapolis venues automatically):

```bash
cd src/QuietSpot.Api
dotnet run
# -> http://0.0.0.0:5210 ; try http://localhost:5210/venues/nearby?lat=44.95&lng=-93.29
```

**2. Run the app:**

```bash
cd src/QuietSpot.App
dotnet build -t:Run -f net10.0-android        # Android emulator
# or open the solution in VS / VS Code with the MAUI extension and F5
```

The app targets `http://10.0.2.2:5210` on Android emulators and
`http://localhost:5210` on iOS simulators (see `Services/Session.cs`). On a
physical device, set your machine's LAN IP:
`Preferences.Set("api_base_url", "http://192.168.x.x:5210")` or edit Session.

For Postgres instead of SQLite, set `ConnectionStrings:Postgres` in
`appsettings.json`.

## How the pieces map to the design

| Design decision | Where it lives |
|---|---|
| Score = confidence × live + (1−confidence) × baseline | `Shared/Scoring/QuietnessScoring.cs` |
| 25-min report half-life, "Usually quiet" below 0.3 confidence | same file (constants at top) |
| Hour-of-week baselines, slow EWMA updates | `Api/Services/ScoringService.cs` |
| Credibility moves with consensus agreement | `ScoringService.UpdateCredibilityAsync` |
| One report / device / venue / 30 min | `POST /reports` in `Api/Program.cs` |
| Precomputed scores, feed never calculates | `Venue.Cached*` columns + `ScoreSweepService` |
| Offline-safe reporting (café basements!) | `App/Services/ReportQueue.cs` |
| Fence shuffling under the iOS 20-region cap | `App/Services/GeofenceSyncService.cs` |
| Dwell = schedule notification on enter, cancel on exit | `App/Delegates/VenueGeofenceDelegate.cs` |
| Report from the lock screen, app never opens | `App/Delegates/ReportNotificationDelegate.cs` |
| Soft ask before the OS location dialog | `Views/OnboardingPage` + `OnboardingViewModel` |
| Notification ask only after first favorite | `VenueDetailViewModel.ToggleFavoriteAsync` |

## Honest caveats (read before first build)

This scaffold was written without the ability to compile (no network in the
authoring environment), so treat it as a 90% starting point:

1. **Package versions.** Pinned to known-good-era versions (Shiny 3.3.4,
   CommunityToolkit.Maui 11.2.0, EF Core 10.0.0). Run `dotnet restore` and bump
   whatever NuGet complains about. If CommunityToolkit.Maui demands a different
   MAUI version, take its latest.
2. **Shiny API surface.** The delegate/manager shapes
   (`IGeofenceDelegate.OnStatusChanged`, `INotificationManager.Send`,
   `Channel.Actions`, `Notification.ScheduleDate`) match Shiny v3 docs, but
   verify against https://shinylib.net if signatures drifted. Shiny is the
   right dependency either way — it's the only maintained geofence provider
   for .NET mobile.
3. **Notification action buttons on iOS** appear on long-press/pull-down, not
   inline. Expected behavior, not a bug.
4. **Android maps key.** `AndroidManifest.xml` has a placeholder Google Maps
   key — only needed when you add the map view (the feed list works without it).
5. **Background location** is opt-in and OFF by default
   (`Session.BackgroundReportingEnabled`). Wire the day-3 soft ask before
   enabling, and budget time for the Play Console background-location
   declaration.
6. **`EnsureCreated`** is used for speed; switch to EF migrations
   (`dotnet ef migrations add Init`) before the schema matters.

## Build roadmap (matches our plan)

- [x] Step 1 — API + schema + seeded Minneapolis venues
- [x] Step 2 — Feed → venue detail → in-app one-tap report (shippable core)
- [x] Step 3 — Scoring service, baselines, decay sweep
- [~] Step 4 — Favorites done; FCM push for quiet-now alerts is a TODO hook in
      `ScoreSweepService.MaybeQueueQuietAlertsAsync`
- [~] Step 5 — Geofence + dwell prompt scaffolded; flip
      `Session.BackgroundReportingEnabled` and request background permission
- [ ] Step 6 — Map view, day-3 background soft ask, "this place is missing" flow

## AI-Assisted Development
AI-assisted development: The initial QuietSpot codebase was generated collaboratively with Anthropic's Claude while exploring AI-assisted software development. The concept, requirements, product decisions, and ongoing development are by Kris Bethea.
