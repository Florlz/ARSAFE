# ARSAFE Settings Panel

## Overview
Comprehensive settings system using UI Toolkit for user preferences and app configuration.

## Features
- ✅ **Persistent Settings** - PlayerPrefs-based storage
- ✅ **Mobile-First Design** - Large touch targets (≥65px)
- ✅ **Real-Time Updates** - Changes apply immediately
- ✅ **Audio Controls** - Master, SFX, and UI volume
- ✅ **Graphics Options** - Quality presets (Low/Medium/High)
- ✅ **AR Optimization** - Tracking quality and drift correction FPS
- ✅ **Developer Tools** - Debug overlay and logging toggles

## Setup Instructions

### 1. In MainMenu Scene:

#### Create Settings Panel GameObject:
1. Create empty GameObject: `SettingsPanel`
2. Add Component: `SettingsPanelController`
3. Add Component: `UIDocument`
4. Configure UIDocument:
   - **Source Asset**: `SettingsPanel.uxml`
   - **Panel Settings**: Default Runtime Panel Settings
   - **Sort Order**: `100` (appears on top of other UI)

#### Create Settings Manager:
1. Create empty GameObject: `ARSafeSettings`
2. Add Component: `ARSafeSettings`
3. ✅ This persists across scenes (DontDestroyOnLoad)

### 2. Connect Settings Button:

#### For UGUI Button:
1. Select your Settings button in MainMenu
2. In Inspector → Button component → OnClick()
3. Click `+` to add event
4. Drag `SettingsPanel` GameObject to object field
5. Select Function: `SettingsPanelController > ShowSettings()`

#### For Code:
```csharp
using ARSafe.UI;

// Show settings panel
var settingsPanel = FindFirstObjectByType<SettingsPanelController>();
if (settingsPanel != null)
{
    settingsPanel.ShowSettings();
}

// Access settings
float volume = ARSafeSettings.Instance.MasterVolume;
ARSafeSettings.Instance.SetGraphicsQuality(2); // High
```

## Settings Reference

### Audio Settings
- **Master Volume**: Controls AudioListener.volume (0-100%)
- **SFX Volume**: Sound effects volume (0-100%)
- **UI Volume**: UI sounds volume (0-100%)

### Graphics Settings
- **Quality Level**: Unity QualitySettings (0=Low, 1=Medium, 2=High)
- **AR Tracking**: Vuforia tracking performance (0=Low, 1=Medium, 2=High)

### AR Settings
- **Drift Correction FPS**: MultiArea pose update rate (5-60 FPS, default: 15)
  - Lower = Better battery life
  - Higher = Smoother drift correction
- **Haptic Feedback**: Enable/disable vibration feedback

### Developer Settings
- **Debug Overlay**: Show/hide debug information overlay
- **Debug Logs**: Enable/disable ARDebugLogger file capture

## File Structure
```
Assets/UI/Settings/
├── README.md (this file)
├── Resources/UI/Settings/
│   ├── SettingsPanel.uxml (UI structure)
│   └── SettingsPanel.uss (styles)
└── Scripts/
    └── SettingsPanelController.cs (controller)

Assets/Scripts/
└── ARSafeSettings.cs (settings manager)
```

## Events
Subscribe to setting change events in your code:

```csharp
ARSafeSettings.Instance.OnMasterVolumeChanged += (volume) => {
    Debug.Log($"Master volume changed to: {volume}");
};

ARSafeSettings.Instance.OnGraphicsQualityChanged += (level) => {
    Debug.Log($"Graphics quality changed to: {level}");
};
```

## Troubleshooting

### Panel doesn't show:
- Check UIDocument has correct UXML assigned
- Check Sort Order is high enough (≥100)
- Check SettingsPanelController is enabled

### Settings don't persist:
- Check ARSafeSettings GameObject exists
- Check PlayerPrefs permissions on device
- Check `SaveSettings()` is called on Apply

### Visual issues:
- Verify SettingsPanel.uss is loaded (auto-loaded via `<Style src="SettingsPanel.uss" />` in UXML)
- Check Panel Settings is assigned (Default Runtime)
- Font sizes should be ≥26px for mobile

### Console warnings:
- ⚠️ `Unknown property 'gap'` - This is expected; UI Toolkit doesn't support CSS `gap` property. We use margins instead.
- ⚠️ `Unknown property 'line-height'` - UI Toolkit limitation, text spacing controlled via padding/margins

## Mobile Optimization
- **Touch Targets**: All buttons ≥65px height
- **Fonts**: Body text ≥26px, headers ≥38px
- **Scrolling**: Enabled for small screens
- **Contrast**: High contrast for outdoor AR use

## Integration with AR Scene
Settings automatically apply to AR components:
- Debug logs → ARDebugLogger
- Drift FPS → ARSafeActivationController.multiAreaPoseUpdateFPS
- Graphics → Unity QualitySettings

## Notes
- Settings panel uses UI Toolkit (not UGUI)
- Panel overlays on top of UGUI main menu
- Settings persist across app sessions
- Default values are mobile-optimized
