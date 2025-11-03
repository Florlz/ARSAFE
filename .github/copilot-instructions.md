# ARSAFE_URP – AI Agent Instructions

**Unity 6000.2.7f2 | Vuforia 11.4.4 | Android AR | URP**

_Last updated: October 2025 (Optimized for AI agent workflow)_

> **READ `.agents/memory.md` FIRST** - Live system state, pending tasks, and session history

> **Project Type:** Unity 6 AR Mobile Application (Android)  
> **Unity Version:** 6000.2.7f2  
> **Key Technologies:** Vuforia Engine 11.4.4, Unity UI Toolkit, URP  
> **Target Platform:** Mobile Phone Screens (AR)

## ⚠️ CRITICAL: Keep These Instructions Current

**MANDATORY:** When you discover new patterns, limitations, or structural changes:
1. **Update this file immediately** - Don't let instructions become stale
2. **Document new limitations** - If you find Unity API constraints, add them to relevant sections
3. **Update workflow patterns** - If you establish new best practices, codify them here
4. **Sync with memory.md** - Major architectural changes belong in both files
5. **Version Unity updates** - When Unity version changes, audit all API references

**Examples of required updates:**
- New UI Toolkit CSS limitations discovered → Update UI Toolkit Guidelines section
- New modular system added → Update Core Modular Systems section
- Changed file organization → Update Documentation Workflow section
- New debug patterns → Update Debug & Logging Toolkit section
- Unity 6 API changes → Update affected sections with version-specific notes

## 🚨 FOLLOW THESE INSTRUCTIONS - NOT OPTIONAL

**These instructions exist to prevent mistakes and maintain code quality. EVERY instruction here is based on real issues that occurred in this project.**

**When you see an instruction:**
1. ✅ **READ IT COMPLETELY** - Don't skim, don't assume you know what it says
2. ✅ **FOLLOW IT EXACTLY** - These aren't suggestions, they're requirements
3. ✅ **CHECK YOUR WORK** - Verify you followed the instruction before proceeding
4. ✅ **UPDATE memory.md** - Document ALL significant changes immediately
5. ✅ **USE THE TOOLS** - grep_search, semantic_search, list_code_usages, get_errors

**If you find yourself about to:**
- Skip the research phase → STOP. Use Context7 first.
- Write code without a plan → STOP. Write the architecture plan in comments.
- Change code without checking dependencies → STOP. Use grep_search/semantic_search.
- Forget to update memory.md → STOP. Update it NOW.
- Create a new markdown file → STOP. Consolidate into existing docs.
- Assume you know the API behavior → STOP. Verify with official documentation.

## Table of Contents
- [Mission Priorities](#mission-priorities)
- [Documentation Workflow](#documentation-workflow)
- [UI Toolkit Guidelines](#ui-toolkit-guidelines)
- [Research & External References](#research-external-references)
- [Debug & Logging Toolkit](#debug--logging-toolkit)
- [Core Modular Systems](#core-modular-systems)
- [Cross-Component Update Protocol](#cross-component-update-protocol)
- [Boundary & Anchor Management](#boundary--anchor-management)
- [Project Configuration & Workflows](#project-configuration--workflows)

---

<a id="mission-priorities" name="mission-priorities"></a>
## Mission Priorities

### Core Principles
1. **📝 Documentation First**
   - `.agents/memory.md` is the **LIVE SYSTEM STATE** - single source of truth
   - Update SESSION HISTORY after every significant change with timestamp
   - Document CURRENT STATUS, PENDING TASKS, KNOWN ISSUES
   - One README per system with clear sections and table of contents
   - Delete redundant markdown files immediately
   
   **Current Documentation Structure:**
   ```
   .github/
   ├─ copilot-instructions.md                   (This file - AI agent instructions)
   └─ SESSION_2025_10_22_MULTI_AREA_DRIFT_FIXES.md (Multi-area drift & relocalization fixes)

   .agents/
   └─ memory.md                                 (LIVE SYSTEM STATE - update always!)

   Assets/UI/[ComponentName]/
   └─ README.md                                 (Per-component setup guides)
   ```

2. **🔍 Research Before Code**
   - Research Unity APIs, Vuforia, UI Toolkit, URP, and third-party packages before writing ANY code
   - **Never** assume API behavior - verify with official documentation
   - Apply to ALL Unity features: Physics, Rendering, Animation, Input, XR, Networking, etc.
   - See Research & External References section for workflow
   - Document findings in comments or `.agents/memory.md`

3. **🏗️ Architecture & Planning Protocol**
   - **BEFORE writing code:** Design the architecture and create implementation plan
   - Use Context7 to research relevant Unity patterns and best practices
   - Document planned approach: components needed, data flow, dependencies
   - Consider scalability, performance, and maintainability from start
   - For complex features: write plan in comments before implementation

4. **🔗 Cross-Check Dependencies**
   - Changes rarely live alone
   - Follow the Cross-Component Update Protocol section
   - Keep activation, debug, UI, and content systems aligned

5. **🐛 Debug & Documentation Updates**
   - **ALWAYS update after making changes:**
     - Add new log patterns to `ARDebugLogger.filterKeywords[]`
     - Update `.agents/memory.md` with new entries
     - Add debug checkpoints to critical paths
     - Document new features in relevant READMEs
   - Color-code debug logs: cyan (★★★ major events), green (✓ success), yellow (warnings), red (errors)

6. **🏗️ Prefer Modular Architecture**
   - New functionality → `Assets/ARSafe_ModularSystem/`
   - Avoid extending legacy controllers
   - Maintain separation of concerns

---

<a id="documentation-workflow" name="documentation-workflow"></a>
## Documentation Workflow

### Consolidated Documentation Structure (October 2025)

**Core Documentation Files:**
```
.github/
├─ copilot-instructions.md                   (This file - AI agent instructions)
└─ SESSION_2025_10_22_MULTI_AREA_DRIFT_FIXES.md (Multi-area drift & relocalization fixes)

.agents/
└─ memory.md                                 (LIVE SYSTEM STATE - UPDATE ALWAYS)

Assets/UI/[ComponentName]/
└─ README.md                                 (Per-component setup guides)
```

**When to Update Which File:**
- **memory.md** → ALL significant code changes, new features, bug fixes (timestamp entries in SESSION HISTORY)
- **copilot-instructions.md** → New patterns discovered, Unity API limitations, workflow changes
- **Component READMEs** → UI component setup, usage examples, configuration

### Rules for Creating & Maintaining Docs
1. **Single README per system** – consolidate everything in one markdown file
2. **No extra markdown fragments** – avoid `SETUP_GUIDE.md`, `QUICK_REFERENCE.md`, etc. Use headings within the README instead
3. **Use structure** – include headings and a TOC for larger documents
4. **Update, don't duplicate** – extend the existing README when features evolve
5. **Delete redundancies** – remove stale or duplicate docs immediately
6. **Consolidate related docs** – if you create multiple docs for one system, merge them into comprehensive guides

**Good pattern**
```
Assets/UI/MessageNotification/
├── README.md                         # sole documentation file
├── Resources/UI/MessageNotification/
│   ├── MessageNotification.uxml
│   └── MessageNotification.uss
└── Scripts/
    └── MessageNotificationController.cs
```

**Bad pattern (do NOT copy)**
```
Assets/UI/MessageNotification/
├── README.md
├── SETUP_GUIDE.md          ❌
├── QUICK_REFERENCE.md      ❌
├── INTEGRATION_GUIDE.md    ❌
```

### UI Resources Organization

**Current pattern (nested Resources folders):**
```
Assets/UI/ComponentName/
├── Resources/UI/ComponentName/
│   ├── ComponentTemplate.uxml      (embedded <Style> reference)
│   └── ComponentStyles.uss
├── Scripts/
│   └── ComponentController.cs
└── README.md
```

**Loading resources in code:**
```csharp
Resources.Load<VisualTreeAsset>("UI/ComponentName/TemplateName");
Resources.Load<StyleSheet>("UI/ComponentName/StyleName");
```

**Key rules:**
1. **Keep existing nested structure** - `Assets/UI/[ComponentName]/Resources/UI/[ComponentName]/` for UXML/USS
2. **Scripts at component level** - `Assets/UI/[ComponentName]/Scripts/`
3. **README at component level** - `Assets/UI/[ComponentName]/README.md`
4. **Embed stylesheets in UXML** - All templates have `<Style src="project://database/...">` references
5. **Don't consolidate to single Resources folder** - Unity UXML project:// URIs break when files move

**Note:** While nested Resources folders aren't ideal, they work reliably. Consolidation attempts cause Unity asset reference errors due to UXML's hardcoded GUIDs.

### Documentation Priority Order
1. `.agents/memory.md` (Live system state - update after EVERY significant change)
2. `.github/copilot-instructions.md` (This file - AI agent instructions)
3. `.github/SESSION_2025_10_22_MULTI_AREA_DRIFT_FIXES.md` (Session documentation)
4. Component READMEs (UI components, specific systems)
5. In-code comments
6. External documentation

---

<a id="ui-toolkit-guidelines" name="ui-toolkit-guidelines"></a>
## Unity UI Toolkit Guidelines

**Official Documentation:** Unity 6 UI Toolkit uses UXML for structure, USS for styling (subset of CSS), and C# for logic. Optimized for both Editor extensions and runtime UI.

### Mobile-First Design Principle
**CRITICAL: This is an Android AR mobile application. ALL UI MUST be designed EXCLUSIVELY for phone screens - NOT for PC/desktop.**

- **Target Platform:** Android phones ONLY
- **Build Target:** Android mobile devices (NOT Windows/Mac/Linux)
- **Testing:** All UI must be tested on actual phone screen sizes or mobile emulators
- **Design Philosophy:** Mobile-first, mobile-only - if it works on desktop, it's WRONG

**Mobile UI Requirements:**
- **Minimum font sizes:** Body text ≥22px, headings ≥26px, titles ≥32px
- **Touch targets:** Buttons and interactive elements ≥60px × 60px (thumb-friendly)
- **Spacing:** Generous padding (24px+) and margins (16px+) for readability
- **Tap areas:** Large, well-spaced to prevent mis-taps
- **Orientation:** Support portrait and landscape
- **Screen sizes:** Design for 5" to 7" phone screens (1080x1920 to 1440x2960)

**❌ NEVER:**
- Design for desktop/PC screens
- Use small fonts or buttons that work on desktop
- Assume mouse/keyboard interaction
- Create UI optimized for large monitors

**✅ ALWAYS:**
- Design for touch input (fingers, not mouse cursors)
- Test on mobile resolutions (not 1920x1080 desktop)
- Use large, thumb-friendly tap targets
- Consider one-handed phone use
- Optimize for mobile performance (lightweight UI)

**Rule of Thumb:** If text/buttons look good on desktop, they're TOO SMALL for mobile phones!

### Stylesheet Embedding in UXML
**ALL UI components MUST embed their stylesheets using `<Style>` element:**
```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="project://database/Assets/UI/ComponentName/Resources/UI/ComponentName/ComponentStyles.uss?fileID=7433441132597879392&amp;guid=YOUR_GUID_HERE&amp;type=3" />
    <!-- Your UI elements -->
</ui:UXML>
```

**How to get the GUID:**
1. Open the `.uss.meta` file in a text editor
2. Copy the `guid:` value (e.g., `884dff4fa9e29d743babd89a8d1c1c04`)
3. Use fileID `7433441132597879392` (standard for StyleSheet assets)
4. Use type `3` (StyleSheet asset type)

### USS Supported Properties (Official Unity 6 Specification)

**Layout & Positioning:**
- `display` (flex | none)
- `position` (absolute | relative)
- `left`, `right`, `top`, `bottom` (length | percentage | auto)
- `width`, `height` (length | percentage | auto)
- `min-width`, `max-width`, `min-height`, `max-height`
- `margin`, `padding` (accepts 1-4 values)
- `border-width`, `border-color`, `border-radius` (length | percentage)

**Flexbox (Full Support):**
- `flex-direction` (row | row-reverse | column | column-reverse)
- `flex-wrap` (nowrap | wrap | wrap-reverse)
- `flex-grow`, `flex-shrink`, `flex-basis`
- `flex` (shorthand)
- `justify-content` (flex-start | flex-end | center | space-between | space-around)
- `align-items` (flex-start | flex-end | center | stretch | **NOT baseline**)
- `align-content` (flex-start | flex-end | center | stretch | auto)
- `align-self` (flex-start | flex-end | center | stretch | auto)

**Visual Styling:**
- `color`, `background-color` (hex, rgb, rgba, keyword)
- `background-image` (resource | url | none)
- `opacity` (0.0 - 1.0)
- `visibility` (visible | hidden)
- `overflow` (visible | hidden | **NOT scroll**)
- `cursor` (arrow | text | resize-* | etc.)

**Typography (Unity-Specific):**
- `-unity-font` (resource | url)
- `-unity-font-definition` (resource | url)
- `-unity-font-style` (normal | italic | bold | bold-and-italic)
- `-unity-text-align` (upper-left | middle-center | lower-right | etc.)
- `-unity-text-outline-width`, `-unity-text-outline-color`
- `-unity-paragraph-spacing`
- `word-spacing` (length)
- `white-space` (normal | nowrap)
- **NOT SUPPORTED:** `line-height`, `text-transform`, `letter-spacing`, `font-size` (use `font-size` instead)

**Background & Images:**
- `-unity-background-scale-mode` (stretch-to-fill | scale-and-crop | scale-to-fit)
- `-unity-background-image-tint-color`
- `-unity-slice-left`, `-unity-slice-right`, `-unity-slice-top`, `-unity-slice-bottom` (9-slice)

**Other:**
- `transition-*` properties (limited support)
- CSS variables with `var(--variable-name)`

### Unsupported CSS Features (Critical - Will Cause Errors)
**❌ NEVER USE THESE - OFFICIAL UNITY LIMITATIONS:**

**Pseudo-Selectors (ALL unsupported):**
- `:hover`, `:active`, `:focus`, `:checked`, `:disabled`
- `:first-child`, `:last-child`, `:nth-child()`, `:nth-of-type()`
- `:not()`, `:before`, `:after`
- **Solution:** Use C# event callbacks or class manipulation

**Layout Properties:**
- `gap`, `row-gap`, `column-gap` → Use margin/padding
- CSS Grid (`grid-template-*`) → Use flexbox only
- `float`, `clear` → Not implemented

**Typography:**
- `line-height` → Not supported
- `text-transform` → Not supported
- `letter-spacing` → Not supported
- `text-decoration` → Limited support

**Visual Effects:**
- `backdrop-filter`, `filter` → Not supported
- `box-shadow`, `text-shadow` → Not supported
- `transform` → Limited support

**Other:**
- `overflow: scroll` → Use `overflow: hidden` or `overflow: visible` only
- `border-radius: 50%` → Use exact pixel values (e.g., `36px` for 72px circle)
- `calc()` → Not supported
- `@keyframes` → Use C# animations
- `align-items: baseline` → Only `flex-start`, `flex-end`, `center`, `stretch` supported

### Background Images in UI Toolkit (Runtime Best Practice)
- Prefer `-unity-background-scale-mode` over web CSS `background-size`/`background-position` for VisualElement backgrounds. Use `scale-and-crop` for hero banners.
- Apply sprites via C#: `element.style.backgroundImage = new StyleBackground(sprite);`
- Example (Welcome banner):
   - USS: `.welcome-banner { -unity-background-scale-mode: scale-and-crop; height: 200px; }`
   - C#: `bannerElement.style.backgroundImage = new StyleBackground(sprite);`

**For interactive states:** Use C# callbacks:
```csharp
// Hover simulation
element.RegisterCallback<PointerEnterEvent>(evt => element.AddToClassList("hover"));
element.RegisterCallback<PointerLeaveEvent>(evt => element.RemoveFromClassList("hover"));

// Focus simulation
element.RegisterCallback<FocusInEvent>(evt => element.AddToClassList("focused"));
element.RegisterCallback<FocusOutEvent>(evt => element.RemoveFromClassList("focused"));
```

### USS Selectors (Supported)
| Selector Type | Example | Description |
|---------------|---------|-------------|
| Type | `Button { }` | All Button elements |
| Class | `.primary-btn { }` | Elements with class="primary-btn" |
| ID | `#submit-btn { }` | Element with name="submit-btn" |
| Universal | `* { }` | All elements |
| Descendant | `.container Button { }` | All Buttons inside .container |
| Child | `.container > Button { }` | Direct Button children of .container |
| Multiple | `.btn, .link { }` | Multiple selectors |
| Unity State | `.unity-disabled { }` | Built-in state classes |

**NOT Supported:** Pseudo-selectors, attribute selectors `[attr=value]`, sibling combinators (`+`, `~`)

### Best Practices (Unity 6 Runtime UI)
1. **Always design for mobile first** - AR apps require large, touch-friendly UI
2. **Embed stylesheets in UXML** - Ensures styles load with templates
3. **Use USS variables** - Define reusable values with custom properties (e.g., `--main-color: rgb(255, 255, 255);`)
4. **Load UXML/USS via Resources** - `Resources.Load<VisualTreeAsset>("UI/...")`
5. **Access elements via Q/UQuery** - `rootElement.Q<Button>("button-name")`
6. **Test immediately in Unity** - Valid CSS may behave differently in USS
7. **Use class manipulation for states** - `element.AddToClassList("active")`
8. **Leverage PanelSettings** - Configure scale mode, theme stylesheet, DPI
9. **Avoid excessive nesting** - Flat hierarchies perform better
10. **Hide overlays in USS** - Use `display: none` in stylesheet, not complex C# logic (UIDocument Source Asset renders immediately)

### Common Pitfalls to Avoid
- ❌ Using `:hover` or any pseudo-selectors (causes "Unknown pseudo class" errors)
- ❌ Using `gap` property (not implemented - use margin/padding)
- ❌ Using `line-height`, `letter-spacing`, `text-transform` (not supported)
- ❌ Invalid `align-items` values (only flex-start, flex-end, center, stretch)
- ❌ `overflow: scroll` (only visible/hidden work)
- ❌ Stray braces or invalid syntax (crashes USS parser)
- ❌ Forgetting `&amp;` for `&` in UXML attributes
- ❌ Small desktop-sized fonts/buttons (remember: mobile AR app!)
- ❌ Loading styles only in C# (no fallback if code fails)
- ❌ Switching only `display` when showing overlays while `opacity=0` and `pickingMode=Ignore` remain from initialization—UI will still be invisible/non-interactive. Always restore `opacity=1f` and `pickingMode=Position` too.

### Runtime UI Setup Pattern
```csharp
using UnityEngine;
using UnityEngine.UIElements;

public class RuntimeUIExample : MonoBehaviour
{
    void Start()
    {
        var uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;
        
        // Load additional styles if needed
        var styleSheet = Resources.Load<StyleSheet>("UI/AdditionalStyles");
        root.styleSheets.Add(styleSheet);
        
        // Query elements
        var button = root.Q<Button>("my-button");
        button.clicked += () => Debug.Log("Clicked!");
        
        // Dynamic styling
        var label = root.Q<Label>("status");
        label.AddToClassList("active");
        label.style.color = Color.green;
    }
}
```

### Overlay Show/Hide Pattern (CRITICAL for runtime UI)
- Overlays start hidden in USS: `display: none`.
- If you additionally disable interactivity in `OnEnable` (e.g., `opacity = 0`, `pickingMode = Ignore`), you MUST restore all three when showing:
   - ShowOverlay(): `display = Flex`, `opacity = 1f`, `pickingMode = Position`, `Focus()`.
   - HideOverlay(): `display = None`, `opacity = 0f`, `pickingMode = Ignore`.
- Reason: Setting only `display` may still leave the element invisible/non-interactive.

### Performance Optimization (Official Unity 6 Guidelines)

**UsageHints Property:**
- Set `usageHints` on VisualElements **before** adding to Panel (becomes read-only after)
- Hints don't alter visuals/behavior, only internal performance optimizations
- Use for frequently updated elements (animations, dynamic content)

**Layout Optimization:**
- Prefer `position: absolute` for moving elements to avoid layout recalculation
- Use `transform: translate()` instead of changing `top`/`left` when possible
- Batch layout-triggering property changes when updating multiple elements
- Avoid excessive nesting - flat hierarchies perform better

**Event System Optimization:**
- Register callbacks on parent elements and use event bubbling when possible
- Unregister callbacks in cleanup to prevent memory leaks
- Use `evt.StopPropagation()` judiciously to prevent unnecessary event propagation
- For runtime panels: call `EventSystem.SetUITookitEventSystemOverride(null, true, false)` to disable default handling if manually sending events

**Touch/Mobile Input:**
- UI Toolkit automatically handles touch events via pointer events
- `PointerDownEvent`, `PointerUpEvent`, `PointerMoveEvent` work for both mouse and touch
- No special configuration needed for touch on mobile AR
- Test directional navigation if supporting gamepad input

**Memory Management:**
- Use UQuery (`.Q<T>()`) efficiently - cache results instead of repeated queries
- ListView/ScrollView automatically virtualize content for large datasets
- Access `VisualElement.layout` property only when necessary (computed on demand)
- Reuse VisualElements when possible instead of creating/destroying frequently

**Visual Tree Best Practices:**
- Runtime root access: `UIDocument.rootVisualElement`
- Traverse tree efficiently: `element.Children()` for immediate children only
- Use `hierarchy.Children()` for advanced manipulation
- Query by name (fastest): `root.Q<Button>("button-name")`
- Query by class (flexible): `root.Q<Label>(className: "highlighted")`

### Event Handling Patterns (Unity 6 Specification)

**Event Types:**
- **Pointer Events:** `PointerDownEvent`, `PointerUpEvent`, `PointerMoveEvent`, `PointerEnterEvent`, `PointerLeaveEvent`
- **Focus Events:** `FocusInEvent`, `FocusOutEvent`, `FocusEvent`, `BlurEvent`
- **Layout Events:** `GeometryChangedEvent` (only layout event - fires on position/dimension changes)
- **Input Events:** `InputEvent` (fires on text input, one event per keystroke)
- **Drag Events:** `DragEnterEvent`, `DragLeaveEvent`, `DragUpdatedEvent`, `DragPerformEvent`, `DragExitedEvent`
- **Tooltip Events:** `TooltipEvent` (intercept to set custom tooltips dynamically)

**Event Registration:**
```csharp
// Standard callback
element.RegisterCallback<PointerDownEvent>(OnPointerDown);

// With TrickleDown phase (intercept before reaching children)
element.RegisterCallback<TooltipEvent>(OnTooltip, TrickleDown.TrickleDown);

// Unregister in cleanup
element.UnregisterCallback<PointerDownEvent>(OnPointerDown);
```

**Event Propagation:**
- Events bubble up by default (child → parent)
- Use `TrickleDown.TrickleDown` parameter to intercept during trickle phase (parent → child)
- Call `evt.StopPropagation()` to stop event from continuing
- Access event target: `evt.target`, current target: `evt.currentTarget`

**Drag and Drop:**
```csharp
// Make element draggable
element.RegisterCallback<PointerDownEvent>(evt => {
    DragAndDrop.StartDrag(new DragAndDropData { visualMode = DragAndDropVisualMode.Copy });
});

// Handle drop
dropTarget.RegisterCallback<DragPerformEvent>(evt => {
    // Process DragAndDrop data
    evt.StopPropagation();
});
```

### Runtime UI FAQ (Official Unity Documentation)

**Q: How do I know if the mouse is over a visual element?**
- Check `evt.target` in `PointerEnterEvent`/`PointerLeaveEvent`
- Or use `element.worldBound.Contains(mousePosition)`

**Q: How can I remap basic UI actions?**
- Disable default handling: `EventSystem.SetUITookitEventSystemOverride(null, true, false)`
- Manually send events to panels using `panel.SendEvent()`

**Q: How can I change directional navigation focus order?**
- Set `focusable = true` on elements
- Control order via visual tree hierarchy (siblings traversed in order)
- Override navigation with custom logic in `NavigationMoveEvent` handlers

**Q: How can I start entering keyboard input without clicking?**
- Programmatically focus element: `element.Focus()`
- Set initial focus in `Start()` or after UI builds

---

<a id="research-external-references" name="research-external-references"></a>
## Research & External References

### Research Workflow (MANDATORY for ALL Unity APIs)
**Before touching ANY Unity feature, API, or third-party package:**

1. **Identify the Knowledge Gap**
   - Unity UI Toolkit: "Does UI Toolkit support the `gap` property?"
   - Unity Physics: "What's the proper Rigidbody constraint setup for AR?"
   - Unity Animation: "How does Animator state machine transitions work?"
   - Unity Rendering: "What are URP DecalProjector limitations?"
   - Unity Input: "How to handle touch input on mobile AR?"
   - Unity XR/AR: "What's the correct Vuforia observer lifecycle?"
   - Unity Networking: "How to implement multiplayer state synchronization?"
   - Unity Audio: "What's the AudioSource 3D spatialization API?"

2. **Fetch Documentation** using the appropriate tool:
   | Tool | Use Case |
   |------|----------|
   | `mcp_context7_resolve-library-id` → `mcp_context7_get-library-docs` | **PRIMARY:** Unity Manual, Unity UI Toolkit, URP, AR Foundation, Vuforia |
   | `fetch_webpage` | Unity Manual/Vuforia docs when Context7 lacks coverage |
   | `mcp_deepwiki_*` | GitHub repository documentation |
   | `github_repo` | Code snippets from public repos |
   | `get_vscode_api` | VS Code extension APIs |

3. **Research ALL Unity Features Before Use:**
   - **Physics:** Rigidbody, Colliders, Joints, Raycasting, Physics Materials, Character Controllers
   - **Rendering:** URP features, Cameras, Lighting, Shadows, Post-Processing, Shaders, Materials
   - **Animation:** Animator, Animation Clips, IK, Blend Trees, State Machines
   - **Input:** New Input System, Touch, Gestures, Gamepad, Keyboard/Mouse
   - **XR/AR:** AR Foundation, Vuforia, Anchors, Tracking, Plane Detection
   - **UI:** UI Toolkit (runtime/editor), Canvas, EventSystem, Input Modules
   - **Audio:** AudioSource, AudioListener, AudioMixer, 3D Sound, Effects
   - **Scripting:** MonoBehaviour lifecycle, Coroutines, Events, ScriptableObjects
   - **Networking:** Multiplayer, State Sync, RPCs, Network Transforms
   - **Particles:** Particle Systems, VFX Graph, Emission, Modules
   - **Navigation:** NavMesh, NavMeshAgent, Pathfinding, Off-Mesh Links

4. **Verify Constraints** before writing code:
   - API limitations (e.g., USS unsupported properties)
   - Platform-specific behavior (Android vs iOS differences)
   - Performance implications (mobile AR constraints)
   - Version compatibility (Unity 6 vs earlier versions)

5. **Capture Findings** in code comments or `.agents/memory.md`

6. **Update These Instructions** if you discover new limitations

### C# Script Development Protocol

**MANDATORY steps before writing ANY C# script:**

1. **Research Phase** (Use Context7)
   - Look up relevant Unity APIs and features
   - Check official Unity Manual for best practices
   - Verify API availability in Unity 6
   - Document any limitations or gotchas

2. **Architecture & Design Phase**
   - **Define Purpose:** What problem does this script solve?
   - **List Dependencies:** Which Unity APIs, components, and other scripts are needed?
   - **Plan Data Flow:** How does data move through the system?
   - **Identify Integration Points:** Where does this connect to existing systems?
   - **Consider Performance:** Mobile AR constraints, frame budget, memory

3. **Implementation Plan** (Write in comments BEFORE coding)
   ```csharp
   /*
    * ARCHITECTURE PLAN: [ScriptName]
    * 
    * PURPOSE:
    *   - [What this script does]
    * 
    * DEPENDENCIES:
    *   - Unity APIs: [List all Unity namespaces/classes used]
    *   - Project Scripts: [References to other custom scripts]
    *   - External Packages: [Vuforia, etc.]
    * 
    * DATA FLOW:
    *   - Input: [Where data comes from]
    *   - Processing: [How data is transformed]
    *   - Output: [Where results go]
    * 
    * INTEGRATION POINTS:
    *   - [How this connects to existing systems]
    * 
    * PERFORMANCE CONSIDERATIONS:
    *   - [Mobile AR constraints, optimization notes]
    * 
    * DEBUG LOGGING:
    *   - [Key events to log, keywords to add to ARDebugLogger]
    */
   ```

4. **Implementation Phase**
   - Write code following the documented plan
   - Add inline comments for complex logic
   - Include debug logs with color-coding (cyan/green/yellow/red)

5. **Post-Implementation Updates (MANDATORY - DO NOT SKIP)**
   - ✅ **Update `.agents/memory.md`** with timestamped entry documenting the change
   - ✅ **Add log keywords** to `ARDebugLogger.filterKeywords[]` for any new debug strings
   - ✅ **Update relevant README** if new feature/system or significant behavioral change
   - ✅ **Test compilation** with `get_errors` to verify zero errors
   - ✅ **Update these instructions** if you discovered new patterns or limitations
   - ✅ **Verify cross-component impacts** using grep_search/semantic_search/list_code_usages

<a id="when-not-to-research" name="when-not-to-research"></a>
### When NOT to Research
- Project-specific code patterns already documented in `.agents/memory.md`
- Well-established Unity C# fundamentals (basic Transform operations, GameObject.Find, etc.)
- Changes limited to custom project scripts that do not touch Unity APIs or external packages
- Pure C# logic with no Unity-specific features

<a id="debug-logging-toolkit" name="debug-logging-toolkit"></a>
## Debug and Logging Toolkit
- **`ARSafeActivationController`**
  - Toggle `enableDebugLogs = true` to trace anchor switches, dwell timers, and priority decisions.
  - Color legend: cyan ★★★ = switch, green ✓ = localization, yellow = dwell/deactivation, red = blocked.
  - Anchor priority: (1) User INSIDE boundary, (2) Tracking quality (TRACKED > EXTENDED_TRACKED > LIMITED), (3) Center distance, (4) Target type (Room over Hallway on overlaps).
  - Overlap handling: 2 m hysteresis plus a 2 s grace period after each switch to prevent thrashing.
- **`ARDebugLogger`** (`Assets/Scripts/`)
  - Captures all “ARSafe*” logs to `Documents/ARSAFE_Logs/ARDebug_[timestamp].txt`.
  - Add keywords for every new log pattern (include uppercase/lowercase variants).
  - Enable with `captureLogsToFile = true`.
- **Reference** `.agents/memory.md` for up-to-date keyword lists, priority boosts (+200 connected rooms, +50 approaching), and debugging tips.

<a id="core-modular-systems" name="core-modular-systems"></a>
## Core Modular Systems
### Loading & Welcome Flow
- `ARLoadingScreenManager` governs the glassmorphism loading UI. Disable `sequentiallyActivateAreaTargets` whenever `ARSafeLoadingIntegration` is active.
- `ARSafeLoadingIntegration` waits for Vuforia initialization and first tracking, then optionally shows the Modern UI Pack welcome modal. It also pushes localization success messages into the notification stack.
- Runtime telemetry feeds through `ARSafeDebugOverlayIntegration` → `DebugOverlay.Instance.UpdateDisplay`. Attach `ARSafeDebugHelper` to problem Area Targets to trace visibility and activation changes.

-### Earthquake Scenario System
- `EarthquakeScenarioManager` generates random magnitude (5.2-7.4 Richter), intensity tiers, and duration (18-28s) once `BeginScenarioIfReady()` is called after the welcome flow; broadcasts `OnParametersUpdated` and `OnProgressUpdated` events and auto-creates `EarthquakeAlertOverlayController` if the scene is missing one.
- `EarthquakeAlertOverlayController` shows dual-section UI: start alert (magnitude/intensity/response) and completion overlay ("Shaking Has Stopped" guidance).
- `EarthquakeCameraShake` applies Perlin noise-based shake scaled by scenario multipliers and progress curves (ramp in first 15%, fade last 20%).
- `EarthquakeDebrisController` scales particle emission with progress; `autoStopSystems` halts emission on completion.
- `EarthquakeCrackProjectorController` fades URP DecalProjector opacity in/out synchronized with scenario timeline.
- **Timing:** Overlay waits for welcome screen dismissal via `WelcomeScreenManager.OnWelcomeCompleted` to avoid conflicts; parameters cached if UI not built yet.

#### Welcome Banners (Unity UI Toolkit)
- Banner images are applied at runtime via `StyleBackground(Sprite)`.
- In USS, use `-unity-background-scale-mode: scale-and-crop` for hero images (e.g., `.welcome-banner`).
- Integration safeguard: `ARSafeLoadingIntegration.ShowWelcomeScreen()` falls back to `MenuButtonHandler.LastSelectedDisasterType` when `DisasterTypeManager.SelectedDisasterType` is `None`, ensuring disaster-specific banners appear even when entering `MainScene` directly in Editor.

#### Localization Instructions UI (Compact Dropdown)
- The `LocalizationInstructions` overlay is now a compact, tap-to-expand dropdown to minimize camera obstruction on phones.
- Structure: a compact header bar (mini spinner + label + chevron) and an expandable body with full steps and a large spinner.
- Default: collapsed. While waiting for tracking, the mini spinner animates in the header; the large spinner appears only when expanded.
- Controller API: `LocalizationInstructionsController.ShowInitialLocalization()` and `ShowRelocalization()` show the compact bar; tap the bar to expand/collapse. Overlay auto-hides after stable tracking or timeout.
- Implementation notes: toggle section visibility with `display`/`opacity`/`pickingMode` and use `element.style.rotate = new Rotate(new Angle(degrees))` for spinner animation (Unity 6 UI Toolkit).

#### Editor Tools Alignment (Debris)
- Menu: ARSafe → Earthquake Debris → Complete Setup / Create Particle System Only.
- When creating debris via editor tools:
   - Add `ARSafeDisasterContent` to the debris GameObject with `disasterType = Earthquake` and `ignoreAnchorRestrictions = false` (anchor-only scope).
   - Initialize the ParticleSystem with `main.playOnAwake = false` and `emission.enabled = false`; runtime `EarthquakeDebrisController` owns enabling emission and calling `Play()` when the scenario is active and after any re-enable.
   - Use `main.simulationSpace = World`, disable `collision`, and set `ParticleSystemRenderer.alignment = Facing` for AR-friendly visuals.
- Rationale: Prevents “enabled-but-no-particles” traps and ensures filters recognize debris as Earthquake content tied to the current anchor.

### Message Notification System
- **Location:** `Assets/UI/MessageNotification/`
- **Namespace:** `ARSafe.UI`
- **Singleton:** `MessageNotificationController.Instance`
- **Features:** queued stack (max 4 visible), programmatic creation, timing guard (min 2 s, default 5 s), five message types (Info, Success, Warning, Error, ARHint).
- **Setup:**
  ```text
  GameObject: MessageNotification
  ├─ UIDocument (Source Asset = NONE)
  └─ MessageNotificationController
  ```
- **Usage examples:**
  ```csharp
  MessageNotificationController.Instance.ShowMessage("Hello!");
  MessageNotificationController.Instance.ShowMessage("Success!", MessageType.Success, 4f);
  MessageNotificationController.Instance.ShowARTargetPrompt();     // indefinite
  MessageNotificationController.Instance.ShowTrackingSuccess();    // 4 s
  MessageNotificationController.Instance.ShowTrackingLost();       // indefinite
  MessageNotificationController.Instance.HideAllMessages();        // animated hide
  MessageNotificationController.Instance.ClearAllMessages();       // instant clear
  ```
- Already integrated with `ARSafeLoadingIntegration` (localization success) and `WelcomeScreenManager` (“Begin Simulation” guidance).
- Keep UIDocument Source Asset empty; legacy APIs (`HideMessage()`, `HideImmediate()`) were removed in v2.0. See `Assets/UI/MessageNotification/README.md` for full details.

<a id="cross-component-update-protocol" name="cross-component-update-protocol"></a>
## Cross-Component Update Protocol
Follow this process **every time** you modify behavior:

1. **Identify dependencies** using `grep_search`, `semantic_search`, and `list_code_usages`.
2. **Check related systems**:
   - Activation: `ARSafeActivationController`, `ARSafeTrackingManager`, `ARSafeTargetInfo`.
   - Debug: `ARSafeDebugHelper`, `ARSafeDebugOverlayIntegration`, `DebugOverlay`, `ARDebugLogger`.
   - UI: `ARLoadingScreenManager`, `ARSafeLoadingIntegration`, relevant UXML/USS files.
   - Content: `ARSafeProximityDisplay`, `ARSafeDisasterFilter`, `ARSafeDisasterContent`.
   - Vuforia integration: observer lifecycle & tracking events.
3. **Verify no breaking changes**: update all call sites for modified signatures, enums, serialized fields, or data structures.
4. **Test compilation** with `get_errors` after edits.
5. **Update documentation**: add notes to `.agents/memory.md`, refresh public API comments, and extend `ARDebugLogger.filterKeywords[]` for new log phrases.

<a id="component-specific-requirements" name="component-specific-requirements"></a>
### Component-Specific Requirements
- **Activation changes** – patch controller + tracking manager + target info; adjust debug helper/overlay; add keywords.
- **Debug output changes** – refresh `filterKeywords[]`, recolor overlay as needed, verify logs write to disk.
- **Data model changes** – audit all usages, check Inspector references, guard against nulls.
- **UI changes** – keep UXML/USS synchronized, update integrators, test rendering post-import.
- **New log strings** – add lowercase/uppercase variants to `filterKeywords[]`, confirm capture.
// New
- **Overlays (UI Toolkit)** – If `OnEnable` hides overlays by also setting `opacity = 0` and `pickingMode = Ignore`, ensure show/hide APIs restore all three properties (display/opacity/pickingMode). Call `Focus()` on show to prime input.

<a id="common-pitfalls-to-avoid" name="common-pitfalls-to-avoid"></a>
### Common Pitfalls to Avoid
- Changing method signatures without updating every call site.
- Renaming serialized fields or properties without checking Inspector references.
- Adding debug logs but forgetting to extend `ARDebugLogger.filterKeywords[]`.
- Assuming a change is isolated to one script without verifying dependencies.
- Skipping compilation checks after edits.
- Modifying enums without auditing all switch statements.
- Adjusting event signatures without updating subscribers.

<a id="verification-checklist" name="verification-checklist"></a>
### Verification Checklist
1. `get_errors` reports zero issues.
2. All references updated (search tools used).
3. Related scripts patched and coordinated.
4. `ARDebugLogger.filterKeywords[]` includes new patterns.
5. Documentation updated (context memory + comments).
6. Play Mode smoke test when feasible.

<a id="key-principles" name="key-principles"></a>
### Key Principles
- 💡 **"When in doubt: Fetch documentation first, implement second!"**
- 🔗 **"Never make isolated changes. Always verify impact on related scripts."**
- 🔍 **"Use grep_search and semantic_search liberally. Never guess scope of changes."**
- 📱 **"Design for mobile first. If it looks good on desktop, it's too small."**

<a id="boundary-anchor-management" name="boundary-anchor-management"></a>
## Boundary and Anchor Management
- **Boundary priority order**
  1. Child `VisualCenter` BoxCollider (supports rotation).
  2. Child `Boundary` BoxCollider.
  3. BoxCollider on Area Target root.
  4. Auto-calculated geometry (filtered & clamped).
  5. Manual default (`defaultBoundsSize = 20 × 5 × 20 m`).
- **Setup tools**
  - Batch: `ARSafe → Setup Box Colliders for Area Targets` (adds colliders to VisualCenters).
  - Per target: context menu `Setup: Add BoxCollider to VisualCenter`.
  - Diagnostics: `ARSafe → Diagnostics → Check Boundary Setup`.
- **Signed distance convention**
  - Negative = inside boundary.
  - Positive = outside boundary.
  - Zero = boundary edge.
  - Distances rely on oriented-box calculations (8-corner transforms) for rotated colliders.
- **Anchor switching overview**
  - Two switching paths (pre-tracking neighbor checks and tracking-based selection) each with a 0.5 s dwell time.
  - Global grace period: 2 s after every switch.
  - Current anchor preference: remains on the most recent anchor if the user stays inside or within 3 m of its boundary, even when tracking drops.
  - Priority boosts: +200 for connected rooms, +50 for approaching targets.
- **Diagnostics**
  - Enable debug logs to trace “★★★” switch events, dwell timers, inside/outside distances, and tracking states.
  - Logs flow into both the overlay and file capture pipeline.

<a id="project-configuration-workflows" name="project-configuration-workflows"></a>
## Project Configuration and Workflows
### Project Snapshot
- Unity **6000.2.6f1** targeting Android AR with Vuforia Engine **11.4.4** (local tarball referenced in `Packages/manifest.json`).
- Entry flow: `Assets/Scenes/MainMenu.unity` → `Lovatto.SceneLoader` → `Assets/Scenes/MainScene.unity`.
- Area Target datasets: `Assets/StreamingAssets/Vuforia/` (update configs if renamed).

### Modular Stack Guidelines
- New features: `Assets/ARSafe_ModularSystem/Scripts/`.
- `ARSafeActivationController` + `ARSafeTrackingManager` enforce simultaneous tracking ≤ 2.
- `ARSafeTargetInfo` defines metadata, adjacency, bounds, start flags.
- `ARSafeProximityDisplay` handles content visibility with pose gating (`roomsRequireInside = true` for rooms; hallways list connected rooms).
- Disaster content uses `ARSafeDisasterFilter` and child `ARSafeDisasterContent` tags reacting to `DisasterTypeManager.SetDisasterType`.

### Loading, Menu, and Debug Conventions
- Menu buttons (`Assets/Scripts/MenuButtonHandler.cs`) set `DisasterTypeManager.SelectedDisasterType` prior to scene loading. Update button lists when adding scenarios and confirm matching content exists in `MainScene`.
- Always open the project with Unity 6000.2.6f1; earlier versions break the Vuforia package reference.
- Smoke test: start in `MainMenu`, choose a disaster, confirm the loading overlay completes, and verify `ARSafeTrackingManager` keeps active observers ≤ 2.

#### Main Menu Location Selection
- Component: `Assets/UI/LocationSelection/Scripts/MainMenuLocationController.cs` (UIDocument-based)
- Menu buttons call `MainMenuLocationController.ShowOverlay()` when `showLocationSelection = true`.
- Overlay behavior:
   - Default USS: `.location-overlay { display: none; }`
   - On show: set `display = Flex`, `opacity = 1f`, `pickingMode = Position`, then `Focus()`.
   - On hide: set `display = None`, `opacity = 0f`, `pickingMode = Ignore`.
- If the picker doesn’t appear, confirm the controller is present in the scene and the UIDocument uses `LocationSelection.uxml`.

### Build, Test, Troubleshoot
- “Failed to activate observer” → ensure only modular controller + tracking manager are active (disable legacy `AreaTargetActivationManager`).
- Room content invisible → check `ARSafeTargetInfo` distances, `roomsRequireInside`, and `ARSafeProximityDisplay` minimum tracking time.
- Disaster filtering missing → run `ARSafeDisasterFilter.RefreshContentList()` and validate tag naming.
- Diagnostics menu (`ARSafe → Diagnostics`) helps inspect boundary setup, adjacency links, and player position metrics.

---

## Quick Reference

### Essential Commands
- `grep_search` / `semantic_search` - Find code dependencies
- `list_code_usages` - Track all references to a symbol
- `get_errors` - Verify compilation after changes
- `read_file` - Get context before editing

### Critical Files
- `.agents/memory.md` - Live system state and session history
- `Assets/ARSafe_ModularSystem/` - Core modular functionality
- `Assets/Scripts/ARDebugLogger.cs` - Log capture configuration
- `Assets/UI/MessageNotification/` - Notification system

### Color Palette
| Color | Hex Code | Usage |
|-------|----------|-------|
| Navy | 1e2a47 | Primary background |
| Orange | d97628 | Accent, buttons |
| Brown | 8b4513 | Borders, secondary |
| Gold | f3b23a | Highlights |
| Cream | fff4db | Text |

### Common Workflows
1. **Adding New Feature** → Check modular system → Research APIs → Update docs → Test compilation
2. **Fixing Bug** → Enable debug logs → Trace dependencies → Fix all call sites → Update keywords → Test
3. **UI Changes** → Design for mobile → Check USS support → Update UXML/USS together → Test in Unity
4. **System Changes** → Map dependencies → Update all related scripts → Add debug keywords → Update memory.md

---

## 🚨 CRITICAL REMINDERS - READ BEFORE EVERY CODE CHANGE

### Before Writing ANY Code:
1. ✅ **Research Unity APIs** - Use Context7 to look up official documentation
2. ✅ **Plan Architecture** - Write implementation plan in comments first
3. ✅ **Check Dependencies** - Use grep_search/semantic_search to find related code
4. ✅ **Review memory.md** - Check for existing patterns and recent changes

### While Writing Code:
1. ✅ **Add Debug Logs** - Include color-coded logs for key events
2. ✅ **Comment Complex Logic** - Explain non-obvious implementations
3. ✅ **Follow Mobile-First** - Large fonts (≥22px), big touch targets (≥60px)
4. ✅ **Test Incrementally** - Run `get_errors` frequently

### After Writing Code:
1. ✅ **Update ARDebugLogger** - Add new keywords to `filterKeywords[]`
2. ✅ **Update memory.md** - Add new entry with timestamp in SESSION HISTORY
3. ✅ **Update README** - Document new features in relevant docs
4. ✅ **Verify Compilation** - Run `get_errors` to confirm no issues
5. ✅ **Check Cross-References** - Use `list_code_usages` to verify all call sites updated

### For Unity Feature Changes:
1. ✅ **Research First** - Context7 lookup for Physics/Rendering/Animation/Input/XR/etc.
2. ✅ **Verify Constraints** - Platform differences, mobile limitations, API availability
3. ✅ **Test on Target** - Mobile AR behavior may differ from editor

### For C# Script Creation:
1. ✅ **Research Phase** - Context7 lookup for Unity APIs
2. ✅ **Architecture Plan** - Write full plan in comments (PURPOSE/DEPENDENCIES/DATA FLOW/etc.)
3. ✅ **Implementation** - Code with inline comments and debug logs
4. ✅ **Documentation** - Update memory.md and README
5. ✅ **Debug Setup** - Add keywords to ARDebugLogger

---
**Remember:** 💡 "Research → Plan → Code → Document → Debug" - Never skip steps!  
**Golden Rule:** If it looks good on desktop, it's too small for mobile AR! 📱

---

## 📊 Codebase Analysis (October 22, 2025)

**Comprehensive analysis available in:** `.agents/memory.md`

### Overall Assessment
**Code Quality:** 🟢 **A- (Excellent)**  
**Architecture:** 🟢 **A (Excellent)**  
**Performance:** 🟢 **A (Excellent)**  
**Documentation:** 🟢 **B+ (Very Good)**  
**Maintainability:** 🟡 **B (Good, needs refactoring)**

### Key Findings

#### ✅ **Strengths**
1. **Event-Driven Architecture** - Clean event system with proper cleanup
2. **Performance Optimizations** - Throttling (15 FPS multi-area, 5 FPS visibility), caching (5-frame boundary cache), GC reduction (~500KB-2MB/min saved)
3. **Memory Management** - Proper material cleanup, event unsubscription, dictionary/list cleanup
4. **Documentation** - Comprehensive XML comments, inline explanations, context menus
5. **Error Handling** - Null checks, graceful fallbacks, color-coded debug logs

#### ⚠️ **Areas for Improvement**

**1. Code Organization**
- **Issue:** Very long files (ARSafeActivationController: 4,374 lines)
- **Recommendation:** Refactor into partial classes (Core, AnchorSwitching, MultiAreaPose, AugmentationManagement)

**2. Magic Numbers**
- **Issue:** Repeated literals (15f, 3.5f, 200, 50, 0.2f, 0.5f)
- **Recommendation:** Extract to named constants
  ```csharp
  private const float MULTI_AREA_UPDATE_FPS = 15f;
  private const float ANCHOR_GRACE_PERIOD = 3.5f;
  private const int PRIORITY_INSIDE_BONUS = 200;
  private const int PRIORITY_CONNECTED_ROOM_BONUS = 200;
  private const int PRIORITY_APPROACHING_BONUS = 50;
  private const float VISIBILITY_UPDATE_INTERVAL = 0.2f; // 5 FPS
  private const int BOUNDARY_CACHE_FRAMES = 5;
  ```

**3. Long Methods**
- **Issue:** Methods >200 lines (UpdateActivation, ShouldContentBeVisible, etc.)
- **Recommendation:** Extract into smaller, focused methods with clear single responsibilities

**4. Missing Null Checks**
- **Issue:** Potential NullReferenceException in critical paths
- **Examples:**
  ```csharp
  // EarthquakeDebrisController
  targetInfo.ComputeDistanceToBoundary(...); // targetInfo could be null
  
  // ARSafeProximityDisplay
  activationController.CurrentAnchor.GetComponent<ARSafeTargetInfo>(); // CurrentAnchor could be null
  ```
- **Recommendation:** Add null checks with early returns or null-conditional operators

**5. Unused Using Directives**
- **Issue:** Some files have unused imports
- **Recommendation:** Clean up with IDE tools or add Roslyn analyzers to project

### Codebase Statistics
- **Total C# Scripts:** 100+ MonoBehaviour classes
- **Core Modular Scripts:** 20 files
- **UI Components:** 15 components
- **Disaster Systems:** 8 systems (Earthquake, Flood)
- **Critical Files:**
  - ARSafeActivationController: 4,374 lines
  - ARSafeProximityDisplay: 1,200+ lines
  - ARSafeTargetInfo: 900+ lines
  - ARSafeTrackingManager: 250 lines

### Performance Metrics
| Optimization | Before | After | Improvement |
|--------------|--------|-------|-------------|
| Multi-area pose updates | 60 FPS | 15 FPS | 75% reduction |
| Boundary calculations | Every frame | 5-frame cache | 60-70% faster |
| GC allocations | 500KB-2MB/min | ~0 | 100% reduction |
| Visibility checks | 60 FPS | 5 FPS | 92% reduction |

### Critical Patterns

**Anchor Switching Algorithm:**
```
1. Pre-Localization: Enable starting targets → Wait for first tracking → Set anchor
2. Post-Localization: Scan enabled targets → 4-layer validation → Priority scoring → Switch if better
3. Safeguards: Grace Period (3.5s), Minimum Stability (2s), Hysteresis (3m), Speed Check (<3 m/s)
```

**Content Visibility System:**
```
Visibility Rules (AND logic):
1. System localized (hasLocalized = true)
2. Current anchor OR neighbor of current anchor
3. Tracking validation: Rooms (tracking + inside), Hallways (tracking OR adjacent tracking)
4. Distance check (min/max range)
5. Pose validation (minimum tracking time)
```

**Multi-Area Pose Update:**
```
Flow:
1. Anchor tracked → Update controller transform (shared root)
2. Transform neighbors to shared root space
3. Drift correction: Continuous updates at 15 FPS
4. On anchor switch: Immediate pose update (zero delay)
5. Augmentation reparenting: Dynamic attachment
```

### System Integration Map
```
MainMenu (DisasterTypeManager) → SceneLoader → MainScene:
    1. ARSafeLoadingIntegration (loading overlay)
    2. Vuforia initialization & first tracking
    3. WelcomeScreenManager (modal after localization)
    4. EarthquakeScenarioManager (begins disaster timeline)
    5. ARSafeActivationController (anchor switching)
    6. ARSafeProximityDisplay (content visibility)

Event Flow:
DisasterTypeManager.OnDisasterTypeChanged
    → EarthquakeScenarioManager.OnParametersUpdated
        → EarthquakeAlertOverlayController
        → EarthquakeCameraShake
        → EarthquakeDebrisController
        → EarthquakeCrackProjectorController
    → EarthquakeScenarioManager.OnProgressUpdated (timeline sync)

Tracking Flow:
Vuforia → ARSafeTrackingManager → ARSafeActivationController → ARSafeProximityDisplay
```

### Action Items

**High Priority (Do First):**
- [ ] Extract magic numbers to named constants (all files)
- [ ] Add null checks for `targetInfo`, `activationController`, `CurrentAnchor`
- [ ] Refactor ARSafeActivationController into partial classes
- [ ] Clean up unused using directives
- [ ] Add missing XML documentation for public methods

**Medium Priority:**
- [ ] Extract long methods into smaller functions
- [ ] Improve naming consistency (private field conventions)
- [ ] Add unit tests for core algorithms (boundary calculations, priority scoring)
- [ ] Create architecture decision records (ADRs)

**Low Priority:**
- [ ] Optimize ARDebugLogger keyword array to HashSet
- [ ] Consider Job System for boundary calculations
- [ ] Implement object pooling for UI notification instances
- [ ] Add Burst compilation for vector math operations

### Key Learnings

1. **Vuforia Observer Lifecycle** - Area Target observers MUST stay alive; only deinitialize on scene exit, not relocalization
2. **Transform Hierarchies in Multi-Area AR** - Static relative poses don't work; must transform through world space using current transforms
3. **Timing Issues in Unity** - Update() cycles have delays (up to 67ms at 15 FPS); critical operations need immediate execution
4. **Unity UI Toolkit Limitations** - No pseudo-selectors, no gap/line-height, requires C# for interactive states

### Recommended Next Steps
1. Review `.agents/memory.md` for complete analysis details
2. Implement high-priority action items (magic numbers, null checks)
3. Create refactoring plan for ARSafeActivationController
4. Add unit tests for boundary distance calculations
5. Document architecture decisions in ADR format

---

**Analysis completed:** October 22, 2025  
**Full details:** See `.agents/memory.md` (comprehensive 30+ page analysis)

