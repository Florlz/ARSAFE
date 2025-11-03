# Location Selection Component

**Last Updated:** October 20, 2025  
**Status:** ✅ Fully Integrated with ARSafe System  
**Unity Version:** 6000.2.7f2 | UI Toolkit | Mobile AR

---

## 📋 Table of Contents
- [Overview](#overview)
- [Integration Status](#integration-status)
- [Setup Instructions](#setup-instructions)
- [Usage](#usage)
- [API Reference](#api-reference)
- [Integration with Main Menu](#integration-with-main-menu)
- [Troubleshooting](#troubleshooting)

---

## Overview

The **LocationSelectionController** provides a pre-simulation UI overlay that allows users to manually select their starting Area Target location before AR tracking begins. **NEW (October 20, 2025):** The system now waits for tracking confirmation before starting the simulation, ensuring content appears at the correct position from the start.

### Features
- ✅ Lists all available starting locations from `ARSafeActivationController.startingTargets`
- ✅ Real-time search/filter functionality
- ✅ Visual selection feedback with checkmarks
- ✅ **NEW:** Tracking confirmation - waits for selected location to be tracked before proceeding
- ✅ **NEW:** User guidance messages - prompts to point camera at selected location
- ✅ Auto-detection fallback if tracking timeout occurs
- ✅ Skip button for traditional auto-detection
- ✅ Mobile-first design (large touch targets, readable fonts)
- ✅ Integrates with ARSafe anchor switching system

### User Flow
```
Main Menu (Disaster Selection)
      ↓
Location Selection Overlay ← This component
      ↓
[User picks "Room118Part1" and clicks Continue]
      ↓
Overlay hides, guidance message appears
      ↓
"Point your camera at Room118Part1"
      ↓
[User points camera at Room118Part1]
      ↓
Vuforia detects and tracks Room118Part1 ✓
      ↓
"Location confirmed! Starting simulation..."
      ↓
Simulation begins with content at CORRECT position
```

---

## 🎯 **Tracking Confirmation Behavior** (NEW)

### **Default Mode: Require Tracking Confirmation** ✅

**What happens when user clicks Continue:**

1. **Overlay Hides** - Location selection UI closes
2. **Guidance Appears** - Debug log shows "Point your camera at [Location]"
3. **System Waits** - Up to 30 seconds (configurable) for tracking to start
4. **Tracking Confirmed** - Once Vuforia detects location, simulation proceeds
5. **Success Message** - "Location confirmed! Starting simulation..."
6. **Simulation Starts** - Content appears at CORRECT position

**If tracking times out (30s default):**
- ⚠️ Warning shown: "Location not found. Using auto-detection..."
- System falls back to normal auto-detection mode
- User can continue walking to find any tracking target

### **Configuration Options** (Inspector)

```
LocationSelectionController:
├─ Require Tracking Confirmation: ☑ (default: ON)
├─ Tracking Confirmation Timeout: 30s (range: 10-60s)
└─ Enable Debug Logs: ☑
```

**Disable tracking confirmation** to use immediate-start mode:
- Simulation starts instantly when Continue clicked
- Content may appear incorrectly if user not at selected location
- Use only if users are already at the selected location

---

## Integration Status

### ✅ **FULLY INTEGRATED** (October 20, 2025)

| Component | Status | Notes |
|-----------|--------|-------|
| **ARSafeActivationController.startingTargets** | ✅ Working | Public property accessible |
| **ARSafeActivationController.SwitchAnchor()** | ✅ Fixed | Made public for external access |
| **UXML/USS Setup** | ✅ Complete | GUID fixed, styles embedded |
| **Namespace** | ✅ Fixed | Added `ARSafe.Modular` using directive |
| **API Compatibility** | ✅ Updated | Uses Unity 6 `FindFirstObjectByType<>()` |

### Recent Fixes (October 20, 2025)
1. **Made `SwitchAnchor()` public** - Enables LocationSelection to programmatically set anchor
2. **Fixed UXML stylesheet GUID** - Replaced placeholder with actual GUID (`8a8f47a19bd690040b47d828771253be`)
3. **Added namespace import** - `using ARSafe.Modular;` for `ARSafeActivationController` access
4. **Updated deprecated API** - Changed `FindObjectOfType<>()` → `FindFirstObjectByType<>()`

---

## Setup Instructions

### 1. GameObject Setup (MainScene or Persistent UI Scene)

```
GameObject: LocationSelection
├─ UIDocument (Source Asset = NONE - builds dynamically)
└─ LocationSelectionController (Script)
    ├─ UI Document: [Auto-assigned from same GameObject]
    └─ Activation Controller: [Auto-found via FindFirstObjectByType]
    └─ Enable Debug Logs: ☑ (optional)
```

**Inspector Setup:**
- **UIDocument:** Leave "Source Asset" empty (component loads from Resources)
- **Activation Controller:** Can be left unassigned (auto-finds `ARSafeActivationController` in scene)
- **Enable Debug Logs:** Check for cyan-colored console logs during development

### 2. Scene Hierarchy

**Option A: MainScene Integration** (Recommended)
```
MainScene
├─ ARSafeActivationController (existing)
├─ UI Systems
│   ├─ LocationSelection ← Add here
│   ├─ MessageNotification
│   ├─ WelcomeScreen
│   └─ SimulationControls
└─ Area Targets (existing)
```

**Option B: Persistent UI Scene** (Advanced)
- Create separate scene for UI that persists across scenes
- Use `DontDestroyOnLoad()` pattern
- Ensure `ARSafeActivationController` reference persists

### 3. Resource Files

**Already configured (no action needed):**
```
Assets/UI/LocationSelection/
├─ Resources/UI/LocationSelection/
│   ├─ LocationSelection.uxml ✅ (GUID fixed)
│   └─ LocationSelectionStyles.uss ✅ (Complete styles)
├─ Scripts/
│   └─ LocationSelectionController.cs ✅ (Integrated)
└─ README.md (This file)
```

---

## Usage

### Basic Usage (Show/Hide Overlay)

```csharp
using ARSAFE.UI;

// Get reference (auto-finds in scene)
LocationSelectionController locationSelector = FindFirstObjectByType<LocationSelectionController>();

// Show overlay
locationSelector.ShowOverlay();

// Hide overlay
locationSelector.HideOverlay();

// Check if visible
bool isVisible = locationSelector.IsOverlayVisible();
```

---

## 🚀 Integration Guide

### Quick Setup (Recommended - Already Integrated!)

The **LocationSelection** component is **already fully integrated** into the ARSafe loading flow via `ARSafeLoadingIntegration`. **No manual setup needed!**

**Current Flow:**
```
1. AR Loading Screen (Vuforia init + tracking)
   ↓
2. LocationSelection Overlay ← YOU ARE HERE (select starting location)
   ↓
3. Welcome Screen (disaster briefing)
   ↓
4. Simulation starts
```

**To Enable/Disable:**
1. Open MainScene in Unity
2. Find GameObject with `ARSafeLoadingIntegration` component
3. Inspector → **Show Location Selection** checkbox
   - ✅ Checked (default) = Shows location chooser
   - ⬜ Unchecked = Skips directly to welcome screen

**How It Works:**
- `ARSafeLoadingIntegration` automatically calls `ShowOverlay()` at the correct time
- User selects location → Click Continue → Overlay hides
- Tracking confirmation waits for Vuforia to track selected location
- Welcome screen appears after location confirmed
- **No additional code required!**

---

## 📋 Main Menu Integration (Standard Flow)

### Menu Button Handler Example

**Scenario:** User clicks "Earthquake Simulation" button in main menu

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButtonHandler : MonoBehaviour
{
    public void OnEarthquakeButtonClicked()
    {
        // Set disaster type (existing logic)
        DisasterTypeManager.SelectedDisasterType = DisasterType.Earthquake;
        
        // Load MainScene - LocationSelection shows AUTOMATICALLY
        SceneManager.LoadScene("MainScene");
        
        // NOTE: No need to call ShowOverlay() manually!
        // ARSafeLoadingIntegration handles this in the loading sequence:
        // Loading Screen → LocationSelection → Welcome → Simulation
    }
}
```

**That's it!** The location selection will appear at the correct time in the flow.

---

## 📋 Advanced Integration (Custom Flows Only)

### Manual Control (If Not Using ARSafeLoadingIntegration)

If you need to show LocationSelection **outside** the normal loading flow:

```csharp
using UnityEngine;
using ARSAFE.UI;

public class CustomFlowExample : MonoBehaviour
{
    void Start()
    {
        // Find location selector
        var locationSelector = FindFirstObjectByType<LocationSelectionController>();
        
        // Show overlay manually
        locationSelector.ShowOverlay();
        
        // Wait for user to select location
        // (overlay will hide when user clicks Continue or Skip)
    }
}
```

### Integration with Main Menu

**Scenario:** User clicks "Earthquake Simulation" button in main menu

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using ARSAFE.UI;

public class MenuButtonHandler : MonoBehaviour
{
    public void OnEarthquakeButtonClicked()
    {
        // Set disaster type (existing logic)
        DisasterTypeManager.SelectedDisasterType = "Earthquake";
        
        // NEW: Show location selection before loading AR scene
        StartCoroutine(ShowLocationSelectionThenLoadScene());
    }
    
    private IEnumerator ShowLocationSelectionThenLoadScene()
    {
        // Option 1: Load MainScene first, THEN show location selection
        AsyncOperation sceneLoad = SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Single);
        sceneLoad.allowSceneActivation = false; // Wait before showing scene
        
        while (sceneLoad.progress < 0.9f)
        {
            yield return null; // Wait for scene to load
        }
        
        sceneLoad.allowSceneActivation = true; // Activate scene
        yield return new WaitForSeconds(0.5f); // Let scene initialize
        
        // Find and show location selector
        LocationSelectionController locationSelector = FindFirstObjectByType<LocationSelectionController>();
        if (locationSelector != null)
        {
            locationSelector.ShowOverlay();
        }
        else
        {
            Debug.LogWarning("LocationSelectionController not found in MainScene!");
        }
    }
    
    public void OnFloodButtonClicked()
    {
        DisasterTypeManager.SelectedDisasterType = "Flood";
        StartCoroutine(ShowLocationSelectionThenLoadScene());
    }
}
```

### Advanced: Pre-Select Location

```csharp
using Vuforia;
using ARSafe.Modular;
using ARSAFE.UI;

// Directly set anchor without showing UI
ARSafeActivationController activationController = FindFirstObjectByType<ARSafeActivationController>();
ObserverBehaviour room118 = activationController.startingTargets
    .Find(target => target.name.Contains("Room118Part1"));

if (room118 != null)
{
    activationController.SwitchAnchor(room118);
    Debug.Log($"Pre-selected starting location: {room118.name}");
}
```

---

## API Reference

### Public Methods

#### `void ShowOverlay()`
Shows the location selection UI overlay.
- Reloads available locations from `ARSafeActivationController.startingTargets`
- Clears search field and selection
- Sets overlay visibility to visible

**Example:**
```csharp
locationSelector.ShowOverlay();
```

---

#### `void HideOverlay()`
Hides the location selection UI overlay.
- Sets overlay visibility to hidden
- Does not affect anchor selection (persists)

**Example:**
```csharp
locationSelector.HideOverlay();
```

---

#### `bool IsOverlayVisible()`
Checks if the location selection overlay is currently visible.

**Returns:** `true` if visible, `false` if hidden

**Example:**
```csharp
if (locationSelector.IsOverlayVisible())
{
    Debug.Log("Location selection is showing");
}
```

---

### Serialized Fields (Inspector)

| Field | Type | Description | Auto-Assigned |
|-------|------|-------------|---------------|
| `uiDocument` | `UIDocument` | Reference to UIDocument component | ✅ Yes (Awake) |
| `activationController` | `ARSafeActivationController` | Reference to activation controller | ✅ Yes (Awake) |
| `enableDebugLogs` | `bool` | Enable cyan-colored console logs | ❌ Manual |

---

### UI Elements (UXML)

| Element Name | Type | Purpose |
|--------------|------|---------|
| `location-selection-overlay` | `VisualElement` | Root overlay container (backdrop) |
| `location-selection-card` | `VisualElement` | Card container (glassmorphic) |
| `location-header__title` | `Label` | Header title ("Select Your Location") |
| `location-header-subtitle` | `Label` | Subtitle text (instructions) |
| `location-search-input` | `TextField` | Search/filter input field |
| `location-list-scroll` | `ScrollView` | Scrollable list container |
| `location-list` | `VisualElement` | List of location items |
| `location-skip-button` | `Button` | Skip button (auto-detection) |
| `location-confirm-button` | `Button` | Confirm button (apply selection) |

---

### USS Classes (Styling)

**Key Classes:**
- `.location-overlay` - Full-screen backdrop (rgba(0,0,0,0.85))
- `.location-card` - Main card with glassmorphism
- `.location-item` - Individual location card
- `.location-item--selected` - Selected location (orange border, checkmark)
- `.location-footer__button--primary` - Orange confirm button
- `.location-footer__button--secondary` - Gray skip button

**Mobile-First Sizes:**
- Header title: 38px
- Location name: 24px
- Search input: 56px height, 20px font
- Buttons: 60px height, 20px font
- Touch targets: Minimum 80px height

---

## Integration with Main Menu

### Option 1: Scene Transition Pattern (Recommended)

**Flow:** Main Menu → Load MainScene → Show Location Selection → Begin Tracking

```csharp
// In MenuButtonHandler.cs
public void OnDisasterButtonClicked(string disasterType)
{
    DisasterTypeManager.SelectedDisasterType = disasterType;
    
    // Load scene asynchronously
    StartCoroutine(LoadSceneAndShowLocationSelection("MainScene"));
}

private IEnumerator LoadSceneAndShowLocationSelection(string sceneName)
{
    AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);
    yield return loadOp; // Wait for scene to load
    
    yield return new WaitForSeconds(0.3f); // Let systems initialize
    
    LocationSelectionController selector = FindFirstObjectByType<LocationSelectionController>();
    if (selector != null)
    {
        selector.ShowOverlay();
    }
}
```

### Option 2: Event-Based Pattern (Advanced)

**Flow:** Use custom events to decouple menu from location selection

```csharp
// Create SceneLoadingManager.cs
public class SceneLoadingManager : MonoBehaviour
{
    public static event Action OnMainSceneReady;
    
    private void Start()
    {
        // MainScene calls this when ready
        OnMainSceneReady?.Invoke();
    }
}

// In LocationSelectionController.cs (add to OnEnable)
private void OnEnable()
{
    SceneLoadingManager.OnMainSceneReady += OnSceneReady;
}

private void OnDisable()
{
    SceneLoadingManager.OnMainSceneReady -= OnSceneReady;
}

private void OnSceneReady()
{
    ShowOverlay();
}
```

### Option 3: Direct Call (Simplest)

**Flow:** Show location selection BEFORE loading MainScene

```csharp
// User clicks disaster button
public void OnDisasterButtonClicked(string disasterType)
{
    DisasterTypeManager.SelectedDisasterType = disasterType;
    
    // Show location selection in current scene
    LocationSelectionController selector = FindFirstObjectByType<LocationSelectionController>();
    selector.ShowOverlay();
}

// In LocationSelectionController.cs - modify OnConfirmButtonClicked:
private void OnConfirmButtonClicked()
{
    if (selectedLocation == null) return;
    
    // Store selected location persistently
    PlayerPrefs.SetString("SelectedStartingLocation", selectedLocation.name);
    
    // Load MainScene
    SceneManager.LoadScene("MainScene");
}

// In ARSafeActivationController.cs - read in Start():
private void Start()
{
    string selectedLocationName = PlayerPrefs.GetString("SelectedStartingLocation", "");
    if (!string.IsNullOrEmpty(selectedLocationName))
    {
        ObserverBehaviour selected = allAreaTargets.Find(t => t.name == selectedLocationName);
        if (selected != null)
        {
            SwitchAnchor(selected);
        }
        PlayerPrefs.DeleteKey("SelectedStartingLocation"); // Clean up
    }
}
```

---

## Troubleshooting

### Issue: "ARSafeActivationController not found in scene!"

**Symptoms:** Error message in console, overlay shows empty list

**Causes:**
1. `ARSafeActivationController` not present in scene
2. Script disabled or inactive
3. Scene not fully initialized

**Solutions:**
```csharp
// Verify controller exists
ARSafeActivationController controller = FindFirstObjectByType<ARSafeActivationController>();
if (controller == null)
{
    Debug.LogError("Controller missing! Add to scene hierarchy.");
}

// Check if disabled
if (!controller.enabled)
{
    Debug.LogError("Controller is disabled!");
}
```

---

### Issue: Location list is empty

**Symptoms:** Placeholder text "No locations available"

**Causes:**
1. `startingTargets` list is empty in `ARSafeActivationController`
2. Area Targets not assigned
3. Scene initialized before Vuforia

**Solutions:**
1. **Check Inspector:** ARSafeActivationController → Starting Targets list should have Area Targets
2. **Auto-assign:** Controller auto-discovers targets in `Awake()`
3. **Manual fix:**
```csharp
// In ARSafeActivationController Inspector
Starting Targets:
  Size: 5
  Element 0: Room118Part1
  Element 1: 2ndHallway_Right
  Element 2: 3rdHallway_Right
  Element 3: 3rdHallway_Left
  Element 4: Lobby
```

---

### Issue: Selected location doesn't switch anchor

**Symptoms:** Click confirm, but anchor doesn't change

**Causes:**
1. Spatial validation rejecting switch (distance limits)
2. Target not tracking
3. Grace period active

**Solutions:**
```csharp
// Enable debug logs in both components
LocationSelectionController:
  Enable Debug Logs: ☑

ARSafeActivationController:
  Enable Debug Logs: ☑

// Check console for:
// - "[LocationSelection] Confirm clicked - switching to anchor: Room118Part1"
// - "[SPATIAL VALIDATION] Rejected anchor switch..." ← Main issue
// - Check distance limits: maxAnchorSwitchDistance (35m default)
```

**Fix distance limits if needed:**
```csharp
// In ARSafeActivationController Inspector
Spatial Validation:
  Max Anchor Switch Distance: 35 → 50 (increase if large building)
  Max Movement Speed: 3 → 5 (allow faster initial positioning)
```

---

### Issue: Overlay doesn't appear

**Symptoms:** `ShowOverlay()` called but nothing visible

**Causes:**
1. UIDocument not assigned
2. UXML/USS not loading
3. Display style not set to flex

**Solutions:**
```csharp
// Check UIDocument
UIDocument doc = GetComponent<UIDocument>();
if (doc == null)
{
    Debug.LogError("UIDocument component missing!");
}

// Check root element
VisualElement root = doc.rootVisualElement;
if (root == null)
{
    Debug.LogError("Root element is null - UXML not loaded!");
}

// Check overlay element
VisualElement overlay = root.Q<VisualElement>("location-selection-overlay");
if (overlay == null)
{
    Debug.LogError("Overlay element not found in UXML!");
}

// Verify display style
Debug.Log($"Overlay display: {overlay.style.display}"); // Should be "Flex"
```

---

### Issue: Search/filter not working

**Symptoms:** Typing in search field doesn't filter list

**Causes:**
1. Search input callback not registered
2. Case-sensitive matching
3. PopulateLocationList() not called

**Solutions:**
```csharp
// Verify callback registration (in LocationSelectionController.cs)
private void RegisterCallbacks()
{
    if (searchInput != null)
    {
        searchInput.RegisterValueChangedCallback(OnSearchInputChanged);
        Debug.Log("Search callback registered"); // Add this
    }
}

// Check filter logic (case-insensitive)
var filteredLocations = availableLocations
    .Where(loc => loc.name.ToLower().Contains(currentSearchQuery.ToLower()))
    .ToList();
```

---

### Issue: Confirm button stays disabled

**Symptoms:** Can't click confirm even after selecting location

**Causes:**
1. Selection not registered
2. Button not enabled in code

**Solutions:**
```csharp
// Verify selection callback
private void OnLocationItemClicked(ClickEvent evt)
{
    var clickedItem = evt.currentTarget as VisualElement;
    selectedLocation = clickedItem.userData as ObserverBehaviour;
    
    Debug.Log($"Selected: {selectedLocation?.name}"); // Add this
    
    if (confirmButton != null)
    {
        confirmButton.SetEnabled(true);
        Debug.Log("Confirm button enabled"); // Add this
    }
}
```

---

## Performance Notes

### Optimization Features
- ✅ **Cached UI queries** - Elements queried once in `OnEnable()`
- ✅ **Minimal LINQ** - Only used for search filtering (acceptable)
- ✅ **No per-frame updates** - Event-driven architecture
- ✅ **Lazy initialization** - Only loads when shown

### Memory Profile
- **UI Elements:** ~10-50 location items (depending on building)
- **GC Allocation:** Minimal (reuses VisualElements)
- **UXML/USS:** Loaded from Resources once

---

## Future Enhancements

### Potential Features
- [ ] Add location icons (room vs hallway)
- [ ] Show distance to each location
- [ ] Add location descriptions from `ARSafeTargetInfo`
- [ ] Remember last selected location (PlayerPrefs)
- [ ] Add "Nearby Locations" smart sorting
- [ ] Integrate with GPS (if available)
- [ ] Add location preview images
- [ ] Multi-language support

---

## Related Documentation

- **ARSafeActivationController:** `Assets/ARSafe_ModularSystem/Scripts/ARSafeActivationController.cs`
- **UI Toolkit Guidelines:** `.github/copilot-instructions.md` (Section: UI Toolkit Guidelines)
- **CONTEXT_MEMORY:** `.github/CONTEXT_MEMORY.md` (Recent Changes: October 20, 2025)
- **Message Notification:** `Assets/UI/MessageNotification/README.md` (Similar UI component)

---

## Changelog

### October 20, 2025 - Tracking Confirmation Feature ✅
- **NEW:** Added tracking confirmation system - waits for selected location to be tracked before starting simulation
- **NEW:** Inspector options: `requireTrackingConfirmation` (default: true), `trackingConfirmationTimeout` (30s)
- **NEW:** User guidance messages via Debug.Log (purple/green/yellow/orange color-coded)
- **NEW:** Auto-fallback to auto-detection if tracking timeout occurs
- Ensures content appears at correct position from simulation start
- Prevents confusion from content appearing at wrong location
- `WaitForTrackingConfirmation()` coroutine monitors Vuforia tracking status
- Default timeout: 30 seconds (configurable 10-60s range)

### October 20, 2025 - Integration Complete ✅
- Made `ARSafeActivationController.SwitchAnchor()` public
- Fixed UXML stylesheet GUID (`8a8f47a19bd690040b47d828771253be`)
- Added `using ARSafe.Modular;` namespace import
- Updated `FindObjectOfType<>()` → `FindFirstObjectByType<>()` (Unity 6 API)
- Created comprehensive README documentation

---

**Status:** ✅ Production Ready with Tracking Confirmation  
**Next Step:** Add GameObject to MainScene hierarchy and integrate with main menu flow
