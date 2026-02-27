## Summary
Fixes BUG-2026-0007 by updating application branding/title text from `Softracker` to `YES I DID IT` in user-visible layout and login UI.

## Root cause
Branding values were hardcoded as `Softracker` in Razor views, so requested title/brand updates were not reflected.

## Changes made
- `Views/Shared/_Layout.cshtml`
  - Updated HTML `<title>` suffix to `YES I DID IT`
  - Updated sidebar brand label to `YES I DID IT`
  - Updated fallback page title text to `YES I DID IT`
- `Views/Auth/Login.cshtml`
  - Updated login page `<title>` to `Login - YES I DID IT`
  - Updated login header text to `YES I DID IT`

## Validation
- `dotnet build` (solution root; succeeded)
- `dotnet build WFHMonitor/WFHMonitor.csproj` (succeeded, 0 warnings, 0 errors)

## Bug
- BUG-2026-0007
