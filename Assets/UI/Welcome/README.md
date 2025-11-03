# Welcome Screen UI Toolkit

## Table of Contents
- [Overview](#overview)
- [Setup](#setup)
- [Runtime Behaviour](#runtime-behaviour)
- [Customization](#customization)
- [Disaster Overrides](#disaster-overrides)
- [Troubleshooting](#troubleshooting)

## Overview
The welcome sequence now ships as a Unity UI Toolkit overlay instead of the legacy Modern UI Pack prefab. `WelcomeScreenManager` clones the UXML template in `Resources/UI/Welcome/WelcomeScreen.uxml`, applies the companion stylesheet, and renders a disaster-specific briefing card with an optional full-width banner image, circular icon, and safety instructions.

## Setup
- Ensure `WelcomeScreenManager` is present in the project (autoloaded via `Instance`).
- The manager will automatically create a `UIDocument` at runtime, reusing an existing panel or generating a runtime panel if none are available.
- Keep `WelcomeScreen.uxml` and `WelcomeScreenStyles.uss` under `Resources/UI/Welcome/` so the manager can load them without inspector wiring.

## Runtime Behaviour
- `ShowWelcomeScreen(DisasterType)` displays the overlay, injects the selected scenario name, and populates the intro/instruction/footer blocks.
- `allowSkip` toggles the secondary button and also enables tapping the scrim / pressing `Esc` to dismiss.
- When the user confirms, `WelcomeScreenManager` records PlayerPrefs flags and optionally queues AR localization guidance through `MessageNotificationController`.
- `OnWelcomeCompleted` fires once per display. `ARSafeLoadingIntegration` waits on this event before proceeding.

## Customization
- Update colors, spacing, or typography in `WelcomeScreenStyles.uss` (avoid unsupported UI Toolkit CSS features such as pseudo selectors or gap).
- Change copy defaults (title, intro template, buttons) in `WelcomeScreenManager`'s inspector fields.
- The "Don't show this again" toggle is controlled by `allowDontShowToggle` and feeds into the PlayerPrefs gate.

## Banner Images (New Feature)
The welcome screen now supports full-width banner images that display at the top of the card:
- **Default Banner**: Set `defaultBannerImage` in the `WelcomeScreenManager` inspector for a generic banner shown when no disaster-specific banner is configured
- **Disaster-Specific Banners**: Add `bannerOverride` sprites in the `disasterContentOverrides` list for scenario-specific imagery
- **Fallback Behavior**: If no banner is assigned, the banner area shows a soft accent color or can be hidden entirely

### Banner Specifications
- **Recommended Size**: 1280×400px (3.2:1 aspect ratio) or larger
- **Format**: PNG (for transparency) or JPG
- **Height**: Banner displays at 200px height in the UI
- **Styling**: Automatically fits to card width with rounded top corners

## Disaster Overrides
Add entries to the `disasterContentOverrides` list on `WelcomeScreenManager` to provide per-scenario customization:
- **Banner Override**: `bannerOverride` - Full-width hero image for visual context
- **Icon Override**: `iconOverride` - Circular icon (maintained alongside banner)
- **Title Override**: `titleOverride` - Custom scenario title
- **Description Override**: `descriptionOverride` - Custom instructions and text
- **Button Text**: `confirmButtonOverride` and `skipButtonOverride` - Custom button labels

Each override maps to a `DisasterType` enum value and will replace the default content when that scenario is selected.

## Layout Structure
The enhanced welcome screen uses a layered design:
```
┌─────────────────────────────┐
│  Full-Width Banner Image    │ ← New: 200px hero section
├─────────────────────────────┤
│  [Icon] Title + Scenario    │ ← Existing: Circular icon + text
│                             │
│  Introduction Text          │
│  • Instruction 1            │
│  • Instruction 2            │
│  • Instruction 3            │
│                             │
│  Footer Text                │
│  ☐ Don't show this again    │
│                             │
│  [Skip] [Begin]             │
└─────────────────────────────┘
```

## Troubleshooting
- If the overlay does not appear, confirm the Resources assets exist and the manager logs do not report missing templates.
- If the UI renders beneath other Toolkit panels, raise `documentSortingOrder` on the manager.
- If banner images don't display, check that sprites are properly assigned in the inspector and are using the correct import settings.
- Mobile builds rely on the runtime `PanelSettings`; delete PlayerPrefs via `ResetWelcomeStatus()` during iteration to ensure the overlay displays.
