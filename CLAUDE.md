# ARSAFE_URP - AI Assistant Instructions

_Last updated: November 3, 2025_

> **Project:** Unity 6 AR Mobile (Android) | Vuforia 11.4.4 | UI Toolkit | URP

---

## 🎯 Mission-Critical Context

**READ FIRST:** `.github/CONTEXT_MEMORY.md` - Full system state, recent fixes, architecture

### Core Tech Stack
- **Unity:** 6000.2.7f2 (Unity 6)
- **AR:** Vuforia Engine 11.4.4 (multi-area tracking)
- **Rendering:** URP (Universal Render Pipeline)
- **UI:** UI Toolkit (NOT uGUI/Canvas)
- **Target:** Android mobile (AR Foundation NOT used)

### Critical Files
```
Assets/ARSafe_ModularSystem/Scripts/
├─ ARSafeActivationController.cs  (anchor switching, multi-area pose)
├─ ARSafeTrackingManager.cs       (Vuforia events)
├─ ARSafeTargetInfo.cs             (boundaries, adjacency)
├─ ARSafeProximityDisplay.cs       (content visibility)
└─ ARSafeLoadingIntegration.cs     (loading flow)

Assets/UI/                          (UI Toolkit components)
Assets/Scripts/ARDebugLogger.cs     (debug logging system)
```

---

## 🚨 CRITICAL CONSTRAINTS

### Unity UI Toolkit - Hard Limits
❌ **NOT SUPPORTED:** `vh`, `vw`, `gap`, `:hover`, `:first-child`, CSS Grid, `box-shadow`, `line-height`
✅ **SUPPORTED:** `px`, `%`, `auto`, Flexbox, `rgba()`, CSS Variables, `-unity-` properties

### Mobile-First Design (NON-NEGOTIABLE)
- **Fonts:** Body ≥22px, Headers ≥28px, Titles ≥38px
- **Touch Targets:** Buttons ≥60px, Interactive ≥48px
- **Golden Rule:** If it looks good on desktop, it's TOO SMALL for mobile

### Performance Budgets
- **Frame Time:** <2ms/frame for all ARSafe scripts
- **Memory:** <300MB total app memory
- **Update() Calls:** MUST throttle (10-15 FPS, NOT 60 FPS)
- **GC Allocations:** Minimize in hot paths (reuse collections)

---

## 📝 Mandatory Workflow

### Before Every Code Change
1. **Research:** Verify Unity 6 APIs (NEVER assume from older versions)
2. **Search:** Check if code already exists elsewhere in project
3. **Plan:** Write architecture comment block (see template below)

### During Implementation
4. **Code:** Add inline comments for complex logic
5. **Throttle:** If in Update(), use time-based throttling
6. **Cache:** Store expensive calculations (Transform, GetComponent, etc.)

### After Implementation
7. **Document:** Update `.github/CONTEXT_MEMORY.md` Section 3 (Recent Changes)
8. **Debug:** Add log keywords to `ARDebugLogger.filterKeywords[]`
9. **Test:** Verify in Unity Editor AND consider mobile performance

### Architecture Plan Template
```csharp
/*
 * PURPOSE: [What this does in 1 sentence]
 * DEPENDENCIES: [Unity APIs, Project Scripts]
 * DATA FLOW: Input → Processing → Output
 * PERFORMANCE: [Mobile constraints, throttling, caching]
 * EDGE CASES: [Null checks, race conditions, etc.]
 */
```

---

## 🐛 Debug System

### Enable Debugging
```csharp
// In Inspector or code:
ARSafeActivationController.enableDebugLogs = true;
ARDebugLogger.captureLogsToFile = true; // Desktop only (auto-disabled on mobile)
```

### Log File Location
```
%USERPROFILE%\Documents\ARSAFE_Logs\ARDebug_[timestamp].txt
```

### Color-Coded Logging
- **Cyan (★★★):** Major events (anchor switches, reparenting)
- **Green (✓):** Success confirmations
- **Yellow (⚠):** Warnings, fallbacks
- **Orange:** Timeouts, errors
- **Gray:** Hysteresis/throttling info

### Key Debug Tags
```
[AUGMENTATION ROOT]    - Reparenting operations
[HYSTERESIS]          - Ping-pong prevention
[GRACE PERIOD]        - Anchor switch cooldowns
[ARSafeDisasterFilter] - Content visibility
[EARTHQUAKE REFRESH]  - Disaster component reactivation
EDITOR MODE           - Editor-specific bypasses
```

---

## ⚡ Performance Patterns

### ✅ CORRECT: Throttled Update
```csharp
private float updateInterval = 0.1f; // 10 FPS
private float lastUpdateTime;

void Update() {
    if (Time.time - lastUpdateTime < updateInterval) return;
    lastUpdateTime = Time.time;

    // Your logic here
}
```

### ✅ CORRECT: Cached Components
```csharp
private Transform cachedTransform;
void Start() {
    cachedTransform = transform; // Cache once
}

void Update() {
    Vector3 pos = cachedTransform.position; // Use cached
}
```

### ✅ CORRECT: Collection Reuse
```csharp
private List<GameObject> reusableList = new List<GameObject>(10);

void ProcessItems() {
    reusableList.Clear(); // Reuse, don't create new
    GetComponentsInChildren(reusableList);
}
```

### ❌ WRONG: Every-Frame Allocations
```csharp
void Update() {
    var items = new List<GameObject>(); // GC ALLOCATION!
    GetComponentsInChildren(items);
}
```

---

## 🚫 Common Pitfalls

### UI Toolkit
- ❌ Using `gap` → ✅ Use `margin-top` on subsequent elements
- ❌ Using `:hover` → ✅ Use C# `RegisterCallback<PointerEnterEvent>`
- ❌ Using `vh`/`vw` → ✅ Use `%` relative to parent
- ❌ Forgetting USS in UXML → ✅ Add `<Style src="YourFile.uss" />`

### Performance
- ❌ `Update()` without throttling → ✅ Time-based intervals
- ❌ `GetComponent<>()` in loops → ✅ Cache in Start/Awake
- ❌ New allocations in hot paths → ✅ Reuse collections
- ❌ String concatenation in Update → ✅ Use StringBuilder or cache

### AR System
- ❌ Searching from shared root → ✅ Use `this.transform`
- ❌ Not refreshing after reparenting → ✅ Call `RefreshComponentsAfterReparenting()`
- ❌ Assuming Vuforia in editor → ✅ Use `#if UNITY_EDITOR` guards
- ❌ Setting `component.enabled` on inactive GameObject → ✅ Activate temporarily first

### Documentation
- ❌ Forgetting CONTEXT_MEMORY.md → ✅ Update Section 3 after changes
- ❌ Not adding debug keywords → ✅ Update `ARDebugLogger.filterKeywords[]`
- ❌ No inline comments → ✅ Explain "why", not "what"

---

## 🎯 System Behaviors Reference

### Augmentation Reparenting
- Only **anchor + enabled neighbors** reparent to shared root
- Disabled targets → stay with original parent (auto-hidden)
- Triggers `RefreshComponentsAfterReparenting()` automatically
- Refreshes disaster filters/proximity displays

### Anchor Switching Safeguards
1. **Hysteresis:** 3m depth advantage required when inside both targets
2. **Grace Period:** 2.5s cooldown after each switch (blocks ALL switches)
3. **Stability Time:** 1.5s minimum on current anchor before switch
4. **Dwell Time:** 1.5s inside neighbor boundary before switch
5. **4-Layer Spatial Validation:** Adjacency → Distance → Speed → Quality

### Editor vs Device
- **Editor:** Skips Vuforia tracking/localization waits (`#if UNITY_EDITOR`)
- **Device:** Full Vuforia tracking and localization flow
- Welcome screen shows immediately in editor for UI testing

### Drift Correction
- Runs at **15 FPS** (throttled from 60 FPS) via `UpdateMultiAreaPose()`
- Only reparents on **anchor/neighbor changes** (not every frame)
- <1% CPU on mobile when optimized

---

## 🔍 Quick Troubleshooting

### Content Not Visible After Anchor Switch
1. Check if `RefreshComponentsAfterReparenting()` called
2. Verify disaster filters updated (`filter.RefreshVisibility()`)
3. For earthquake effects: Check components re-subscribed to events
4. Enable debug logs: `ARSafeActivationController.enableDebugLogs = true`

### Anchor Ping-Pong Between Overlapping Targets
1. Increase `anchorSwitchHysteresis` (default: 3m)
2. Increase `anchorSwitchGracePeriod` (default: 2.5s)
3. Check debug logs for `[HYSTERESIS]` messages

### USS Styles Not Applied
1. Check UXML has `<Style src="YourFile.uss" />`
2. Verify UIDocument Source Asset = NONE (for runtime loading)
3. Check Unity console for USS parsing errors
4. Avoid unsupported properties (`:hover`, `gap`, `vh`, etc.)

### Performance Issues
1. Profile with Unity Profiler (CPU + Memory)
2. Check all `Update()` methods have throttling
3. Verify `multiAreaPoseUpdateFPS` = 10-15 FPS
4. Look for GC allocations in hot paths

---

## 🚪 Virtual Exit System

### Overview
Virtual exits allow you to create exit zones without requiring fully scanned Vuforia Area Targets. Perfect for areas you haven't scanned yet but need exit detection.

### Core Components

**VirtualExitMarker.cs** - Main component
- Attach to any GameObject under an Area Target
- Simple BoxCollider bounds detection (checks if camera position is inside collider, throttled to 10 FPS)
- Three exit types: GroundExit, StairwayCheckpoint, FloodSafeZone
- Auto-registers with ARSafeNavigationValidator
- Requires only BoxCollider (auto-added if missing) - NO physics, NO triggers, NO Rigidbody needed
- Uses `BoxCollider.bounds.Contains(cameraPosition)` for reliable world-space detection

**ARSafeNavigationValidator.cs** - Pathfinding integration
- Compares distances to both real and virtual exits
- Navigation arrows point to nearest exit (real or virtual)
- Wrong-way detection works with virtual exits

**FloodSafeZoneController.cs** - Flood-specific UI
- Shows "You're Safe!" when user reaches 2nd floor+
- Auto-disables wrong-way warnings
- Displays floor level reached

### Setup Instructions

**1. Fire/Earthquake Virtual Exit (Ground Level):**
```
AreaTarget_Hallway
└─ VirtualExit_Main (Empty GameObject)
   └─ VirtualExitMarker component
      ├─ Exit Type: GroundExit
      ├─ Disaster Types: [Fire, Earthquake, GeneralSafety]
      ├─ BoxCollider Size: 2m × 3m × 1m (auto-configured)
      └─ Position: Place where real exit/doorway is located
```

**Quick Setup:**
1. Create empty GameObject as child of Area Target
2. Add `VirtualExitMarker` component (BoxCollider auto-added)
3. Right-click component → **"Auto-Configure BoxCollider"** for preset sizes
4. Set Exit Type and Disaster Types
5. Position GameObject at exit location
6. Enable "Enable Debug Logs" to see detection messages in console

**2. Flood Stairway Checkpoint:**
```
AreaTarget_Stairway
├─ ARSafeTargetInfo
│  ├─ Target Type: Stairway
│  └─ Floor Level: 1
└─ VirtualExit_StairCheckpoint (Empty GameObject)
   └─ VirtualExitMarker component
      ├─ Exit Type: StairwayCheckpoint
      ├─ Disaster Types: [Flood]
      └─ BoxCollider Size: 3m × 3m × 3m (larger for stairway landing)
```

**3. Flood Safe Zone (2nd Floor+):**
```
AreaTarget_SecondFloor
└─ ARSafeTargetInfo
   ├─ Target Type: Room (or any type)
   └─ Floor Level: 2 ← IMPORTANT! Auto-triggers safe zone

NOTE: No VirtualExitMarker needed! Floor level >= 2 auto-detected.
```

### BoxCollider Configuration

**Auto-Configure Presets:**
- **GroundExit**: 2m × 3m × 1m (standard doorway)
- **StairwayCheckpoint**: 3m × 3m × 3m (stairway landing)
- **FloodSafeZone**: 5m × 3m × 5m (large safe area)

**Manual Configuration:**
1. Select VirtualExitMarker GameObject
2. Adjust BoxCollider size/center in Inspector
3. Use Scene view gizmos to visualize detection zone
4. Zone must be at human height (center.y ≈ 1.5m)

### Behavior by Disaster Type

**Fire / Earthquake:**
- Virtual GroundExit shows standard exit overlay
- User sees disaster-specific evacuation guidance
- Wrong-way warnings disable when exit reached

**Flood - Stairway (Checkpoint):**
- Shows notification: "Checkpoint Reached - Keep moving upward"
- Navigation arrows continue pointing to higher floors
- Wrong-way warnings stay active (user must continue)

**Flood - 2nd Floor+ (Safe Zone):**
- Shows FloodSafeZoneController: "You're Safe on High Ground"
- Displays floor level reached
- Wrong-way warnings disable
- Simulation marked as complete

### Performance Notes
- Virtual exit bounds detection: **10 FPS** (100ms intervals, throttled in Update)
- Integrates with existing navigation system (12 FPS)
- Uses `BoxCollider.bounds.Contains()` - NO physics engine overhead
- Mobile-optimized: <0.2ms per virtual exit per frame
- No Rigidbody, no triggers, no colliders on camera - just simple position checking

### Gizmo Visualization
Virtual exits are visible in Unity Scene view:
- **Green box**: GroundExit
- **Yellow box**: StairwayCheckpoint
- **Cyan box**: FloodSafeZone
- **Wireframe + filled**: BoxCollider bounds visualization
- **Label**: Shows exit type and size when selected

### Troubleshooting

**Virtual exit not detected:**
- **MOST COMMON:** BoxCollider too small - use Scene view gizmo to verify size covers exit area
- Check Main Camera has "MainCamera" tag and Camera.main is working
- Verify disaster type filter matches current simulation
- Ensure BoxCollider center is at human height (≈1.5m) so camera position falls inside bounds
- Enable debug logs on VirtualExitMarker component - logs show camera position and bounds
- Check console for "Inside exit zone" messages when you walk into the area
- If seeing "Left exit zone" immediately, BoxCollider might not include your position

**Navigation arrows not pointing to virtual exit:**
- Ensure VirtualExitMarker called Start() (check registration)
- Verify virtual exit is closer than real exits
- Check ARSafeNavigationValidator.enableDebugLogs

**BoxCollider too small/large:**
- Right-click VirtualExitMarker → "Auto-Configure BoxCollider"
- Or manually adjust BoxCollider size in Inspector
- Use Scene view gizmos to visualize zone coverage

**Flood safe zone not showing:**
- Verify ARSafeTargetInfo.floorLevel >= 2
- Check FloodSafeZoneController exists in scene
- Ensure DisasterTypeManager.SelectedDisasterType == Flood

**Stairway checkpoint not appearing:**
- Check ARSafeTargetInfo.targetType == Stairway
- Verify MessageNotificationController exists
- Check flood disaster is active

### Integration Points

**Files Modified:**
- VirtualExitMarker.cs (NEW) - Exit marker component
- ARSafeNavigationValidator.cs - Virtual exit pathfinding
- ARSafeActivationController.cs - Flood floor detection logic
- FloodSafeZoneController.cs (NEW) - Flood safe zone UI
- ExitOverlayController.cs - Virtual exit type handling
- ARSafeWrongWayWarning.cs - Virtual exit event subscription

**Events:**
- `ARSafeNavigationValidator.OnVirtualExitReached` - Fired when virtual exit reached
- `VirtualExitMarker.onVirtualExitReached` - Unity event per marker

---

## 🎛️ Unity Editor Menu Reference

### ARSafe Menu Structure (Workflow-Based)
All ARSafe tools are organized under a single **ARSafe** menu in Unity Editor, grouped by workflow stage:

```
ARSafe/
├── Setup/                              [Create new disaster systems & configure targets]
│   ├── Earthquake Debris/
│   │   ├── Complete Setup (Recommended)
│   │   ├── Generate Meshes Only
│   │   ├── Create Material Only
│   │   ├── Create Particle System Only
│   │   └── Create Dust Particle Prefab
│   ├── Flood/
│   │   ├── Create Flood Water Prefab
│   │   └── Setup Bitgem Water Transparency
│   └── Area Targets/
│       ├── Setup Bounds (Auto from Renderers)
│       └── Setup Box Colliders (Manual Presets)
│
├── Tools/                              [Upgrade & fix existing systems]
│   ├── Earthquake Debris/
│   │   ├── Upgrade Existing Debris
│   │   ├── Upgrade Selected Only
│   │   ├── Fix AR Simulation Settings
│   │   └── Reset to Defaults
│   ├── Fix Script Execution Order
│   └── Disable Old Handlers
│
├── Diagnostics/                        [Test & validate setup]
│   ├── Check Boundary Setup
│   ├── Check Adjacency Setup
│   └── Test Current Position
│
├── Project/                            [Build & project utilities]
│   ├── Refresh Assets (Ctrl+R)
│   ├── Force Recompile
│   ├── Reimport All Assets
│   ├── Clear Console
│   ├── Build/
│   │   ├── Quick Build Android
│   │   ├── Development Build
│   │   ├── Switch to Android
│   │   ├── Open Build Settings
│   │   └── Include Particle Shaders in Build
│   └── Cleanup/
│       ├── Delete Library Folder
│       └── Clear PlayerPrefs
│
├── Help/                               [Documentation & what's new]
│   └── Earthquake Debris: What's New?
│
└── Legacy/                             [Deprecated tools]
    └── Unified ARSAFE Tools (Old)
```

### Common Workflows

**🔥 Setting Up Earthquake Debris (First Time):**
1. `ARSafe > Setup > Earthquake Debris > Complete Setup (Recommended)`
2. Position `EarthquakeDebris` GameObject above target area
3. Test in Play Mode

**💧 Setting Up Flood Simulation:**
1. `ARSafe > Setup > Flood > Create Flood Water Prefab`
2. Place under Area Target's augmentation root
3. Configure floor/knee heights in Inspector

**🎯 Configuring Area Target Boundaries:**
1. `ARSafe > Setup > Area Targets > Setup Bounds (Auto from Renderers)` — OR —
2. `ARSafe > Setup > Area Targets > Setup Box Colliders (Manual Presets)`
3. Verify with `ARSafe > Diagnostics > Check Boundary Setup`

**🔧 Upgrading Existing Debris to Enhanced Visuals:**
1. `ARSafe > Tools > Earthquake Debris > Upgrade Existing Debris`
2. Review changes with `ARSafe > Help > Earthquake Debris: What's New?`

**🐛 Troubleshooting Tracking Issues:**
1. `ARSafe > Diagnostics > Check Boundary Setup` (verify colliders)
2. `ARSafe > Diagnostics > Check Adjacency Setup` (verify neighbors)
3. `ARSafe > Diagnostics > Test Current Position` (runtime test in Play Mode)

---

## 📚 Documentation Priority

1. **Quick Reference:** `.github/CONTEXT_MEMORY.md` Section 1
2. **Recent Changes:** `.github/CONTEXT_MEMORY.md` Section 3 ← **UPDATE THIS**
3. **Full Architecture:** `.github/CONTEXT_MEMORY.md` Section 2
4. **Disaster Systems:** `Assets/ARSafe_ModularSystem/Documentation/DISASTER_SYSTEMS_COMPLETE_GUIDE.md`
5. **Code Review:** `Assets/ARSafe_ModularSystem/Documentation/COMPREHENSIVE_CODE_REVIEW_AND_FIXES.md`

---

## 🎓 Unity 6 API Changes

### Use Modern APIs
```csharp
// ✅ Unity 6
FindFirstObjectByType<ARSafeActivationController>();
FindAnyObjectByType<ARSafeActivationController>();

// ❌ Deprecated (Unity 5)
FindObjectOfType<ARSafeActivationController>();
```

### Use `#if UNITY_EDITOR` for Editor-Only Code
```csharp
#if UNITY_EDITOR
    Debug.Log("Editor mode - skipping Vuforia wait");
    yield break;
#endif
```

---

## ✅ Pre-Flight Checklist

Before submitting code:
- [ ] Unity APIs verified for Unity 6
- [ ] Architecture comment block added
- [ ] Update() throttled (if used)
- [ ] Expensive calls cached
- [ ] CONTEXT_MEMORY.md Section 3 updated
- [ ] Debug keywords added to ARDebugLogger
- [ ] Mobile-first design verified (fonts ≥22px, buttons ≥60px)
- [ ] No unsupported USS properties (vh, vw, gap, :hover)
- [ ] No GC allocations in hot paths
- [ ] Tested in Unity Editor

---

**Remember:** Research → Plan → Code → Document → Debug 📱

**Golden Rule:** When in doubt, CHECK CONTEXT_MEMORY.md FIRST!
