## Summary
- Update app branding text from `Softracker` to `YES I DID IT` in shared layout and login page.
- Ensure fallback page title also reflects the requested branding.

## Root cause
Brand name was hardcoded as `Softracker` across Razor views and had not been updated to match the requested title.

## Validation
- `dotnet build` (run in `WFHMonitor/WFHMonitor`) ✅
