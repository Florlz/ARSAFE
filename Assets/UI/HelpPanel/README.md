# Help Panel - User Guide & FAQ System

**Location:** `Assets/UI/HelpPanel/`  
**Namespace:** `ARSafe.UI`  
**Component:** `HelpPanelController`  
**Mobile-Optimized:** ✅ Phone screens only (large fonts, touch targets)

---

## Overview

The Help Panel provides users with:
- **FAQ** - Common questions about getting started, scenarios, and troubleshooting
- **Safety Information** - Important warnings about real emergencies
- **App Controls** - Guide to buttons and navigation
- **Contact Support** - Email support information

**Design Philosophy:** Mobile-first with large fonts (≥22px), generous spacing, and thumb-friendly tap targets.

---

## Features

### ✅ Comprehensive FAQ Sections
1. **Getting Started** - How to use ARSAFE, supported buildings
2. **Emergency Scenarios** - Fire, Earthquake, Flood guidance
3. **Troubleshooting** - AR tracking, arrows, performance issues
4. **Safety Information** - Real emergency warnings and tips
5. **App Controls** - Button descriptions and usage
6. **Contact Support** - Email and response time

### ✅ Mobile-Friendly UI
- Large fonts: Body 22px, Headings 28px, Title 36px
- Touch-friendly close button: 52×52px
- Scrollable content for all phone sizes
- Color-coded info cards (Warning: orange, Tip: green)
- Responsive layout (92% width, max 600px)

### ✅ Multiple Close Methods
- **× Button** - Top-right close button
- **Overlay Click** - Tap background to dismiss
- **Back Button** - Android back button support
- **API Method** - `HelpPanelController.Hide()`

### ✅ Auto-Integration with Main Menu
- `MenuButtonHandler` automatically creates HelpPanel if missing
- Help button click shows/hides overlay
- Singleton pattern ensures only one instance

---

## Setup (Automatic via MenuButtonHandler)

### Option 1: Auto-Create (Recommended)
1. Open MainMenu scene
2. Select `MenuButtonHandler` GameObject
3. Assign your Help button to `Help Button` field
4. Enable `Auto Create Help Panel` (default: true)
5. Set `Help Panel Sorting Order` (default: 55)
6. **Done!** Panel will be created at runtime when Help button is clicked

### Option 2: Manual Setup
1. Create GameObject: `HelpPanel`
2. Add component: `HelpPanelController`
3. In `MenuButtonHandler`, assign `HelpPanelController` reference
4. Disable `Auto Create Help Panel`
5. Assign Help button in inspector
6. **Done!** Panel will show/hide on button click

---

## Structure

```
HelpPanel/
├── Resources/
│   └── UI/
│       └── HelpPanel/
│           ├── HelpPanel.uxml           (UI structure with FAQ content)
│           └── HelpPanelStyles.uss      (Mobile-first styles)
└── Scripts/
    └── HelpPanelController.cs          (Controller logic)
```

---

## Usage

### Via MenuButtonHandler (Main Menu)
```csharp
// Automatic - just assign the help button in Inspector
// MenuButtonHandler will:
// 1. Auto-create HelpPanelController if missing
// 2. Hook up button click → Toggle()
// 3. Handle cleanup on scene exit
```

### Programmatic API
```csharp
using ARSafe.UI;

// Show help panel
HelpPanelController.Instance.Show();

// Hide help panel
HelpPanelController.Instance.Hide();

// Toggle visibility
HelpPanelController.Instance.Toggle();

// Check visibility
bool isOpen = HelpPanelController.Instance.IsVisible;
```

### Context Menu (Editor Testing)
Right-click `HelpPanelController` in Inspector:
- **Show Help Panel** - Open overlay
- **Hide Help Panel** - Close overlay
- **Toggle Help Panel** - Switch state

---

## FAQ Content

### Current FAQ Entries (Editable in UXML)

**Getting Started:**
- Q: How do I start using ARSAFE?
- Q: What buildings are supported?

**Emergency Scenarios:**
- Q: What should I do during a Fire simulation?
- Q: What should I do during an Earthquake simulation?
- Q: What should I do during a Flood simulation?

**Troubleshooting:**
- Q: The AR tracking is not working. What should I do?
- Q: The arrows are not showing. Why?
- Q: The app is running slowly. How can I fix this?
- Q: How do I switch between disasters?

**App Controls:**
- Back Button - Return to main menu or exit simulation
- Settings Icon - Adjust app preferences and language
- Location Menu - Switch between different campus buildings
- Disaster Cards - Tap to start Fire, Earthquake, or Flood training

**Safety Information:**
- **WARNING:** ARSAFE is a training tool, not for real emergencies
- **TIP:** Practice regularly to build muscle memory

**Contact Support:**
- Email: usantarsafe@gmail.com
- Response time: 24-48 hours

---

## Customization

### Adding New FAQ Items
Edit `HelpPanel.uxml`:
```xml
<ui:VisualElement class="help-faq-item">
    <ui:Label text="Q: Your question here?" class="help-faq-question" />
    <ui:Label text="A: Your answer here." class="help-faq-answer" />
</ui:VisualElement>
```

### Adding New Sections
```xml
<ui:VisualElement class="help-section">
    <ui:Label text="New Section Title" class="help-section__title" />
    <!-- Your FAQ items or content here -->
</ui:VisualElement>
```

### Adding Info Cards
**Warning Card:**
```xml
<ui:VisualElement class="help-info-card help-info-card--warning">
    <ui:Label text="IMPORTANT" class="help-info-card__badge" />
    <ui:Label text="Your warning message" class="help-info-card__text" />
</ui:VisualElement>
```

**Tip Card:**
```xml
<ui:VisualElement class="help-info-card help-info-card--tip">
    <ui:Label text="TIP" class="help-info-card__badge" />
    <ui:Label text="Your helpful tip" class="help-info-card__text" />
</ui:VisualElement>
```

### Styling Customization
Edit `HelpPanelStyles.uss`:
```css
/* Change font sizes */
.help-faq-question {
    font-size: 26px; /* Increase question size */
}

/* Change colors */
.help-card {
    background-color: rgb(255, 255, 255); /* White background */
}

/* Adjust spacing */
.help-faq-item {
    margin-bottom: 30px; /* More space between items */
}
```

---

## Color Palette

| Element | Color | Hex Code | Usage |
|---------|-------|----------|-------|
| Navy | RGB(30, 42, 71) | #1e2a47 | Headers, footer, text |
| Orange | RGB(217, 118, 40) | #d97628 | Borders, accents |
| Brown | RGB(139, 69, 19) | #8b4513 | Card border |
| Cream | RGB(255, 244, 219) | #fff4db | Background |
| Blue | RGB(68, 138, 255) | #448aff | Hero section, controls |

---

## Mobile Design Specifications

### Font Sizes (Phone-Optimized)
- **Title:** 36px (Hero section)
- **Mobile Header:** 28px (Top bar)
- **Section Titles:** 28px
- **FAQ Questions:** 24px
- **Body Text:** 22px
- **Small Text:** 20px
- **Footer Date:** 18px

### Touch Targets
- **Close Button:** 52×52px (thumb-friendly)
- **Minimum Tap Area:** 60×60px recommended

### Layout
- **Card Width:** 92% of screen (max 600px)
- **Card Height:** 88% of screen
- **Padding:** 18-28px (generous spacing)
- **Margins:** 16-32px between sections
- **Border Radius:** 10-16px (modern rounded corners)

---

## Integration with Other Systems

### MenuButtonHandler
- Auto-creates HelpPanel if `autoCreateHelpPanel = true`
- Links help button → `HelpPanelController.Toggle()`
- Cleans up listeners on scene exit

### Android Back Button
- Enabled by default (`enableBackButtonClose = true`)
- Closes panel when visible
- Falls through to system/menu when panel hidden

### Sorting Order
- Default: 55 (above most UI, below critical overlays)
- Configurable in `MenuButtonHandler` inspector
- Can be changed at runtime via `SetSortingOrder(int)`

---

## Troubleshooting

### Help button does nothing
1. Check `MenuButtonHandler` has `helpButton` assigned
2. Verify `HelpPanelController` exists or `autoCreateHelpPanel = true`
3. Check Console for `[MenuButtons]` warnings
4. Ensure `HelpPanel.uxml` exists in `Resources/UI/HelpPanel/`

### Panel shows but content is blank
1. Verify `HelpPanel.uxml` has all content elements
2. Check `HelpPanelStyles.uss` is referenced in UXML `<Style>` tag
3. Look for Console errors about missing elements

### Close button not working
1. Verify button name in UXML is `help-close-button`
2. Check callback registration in `HelpPanelController.RegisterCallbacks()`
3. Test with context menu: Right-click controller → Hide Help Panel

### Android back button doesn't close panel
1. Ensure `enableBackButtonClose = true` in inspector
2. Verify panel `isVisible = true` when open
3. Check for other systems consuming `Input.GetKeyDown(KeyCode.Escape)`

### Fonts too small on device
1. Remember: This is a **mobile AR app**, not desktop
2. Fonts are already large (22-36px) for phone screens
3. If still too small, increase font sizes in USS
4. Test on actual device, not Unity Game View

---

## Performance Notes

- **Lightweight:** Static content, no runtime generation
- **Lazy Loading:** UXML loaded on first show
- **Single Instance:** Reused for multiple opens
- **No GC Allocations:** UI Toolkit event system is efficient
- **Mobile-Optimized:** Scrolling handled by native ScrollView

---

## Future Enhancements (Optional)

- **Search/Filter FAQ** - Text search box to find specific topics
- **Localization** - Multi-language support for FAQ content
- **Video Tutorials** - Embedded video links for visual guides
- **Expandable Sections** - Collapsible FAQ categories
- **Dark Mode** - Toggle between light/dark themes
- **Feedback Form** - In-app support ticket submission

---

## Testing Checklist

### Main Menu Integration
- [ ] Help button assigned in MenuButtonHandler
- [ ] Click help button → Panel shows
- [ ] Click × button → Panel hides
- [ ] Click overlay background → Panel hides
- [ ] Press Android back button → Panel hides

### Content Verification
- [ ] All FAQ sections visible
- [ ] Scrolling works smoothly
- [ ] Text is readable on phone screen
- [ ] Info cards show correct colors
- [ ] Contact email is correct

### Edge Cases
- [ ] Rapid open/close doesn't break state
- [ ] Panel works after scene reload
- [ ] Back button doesn't interfere with menu navigation
- [ ] Panel stays on top of menu elements (sorting order)

---

## Summary

**What It Does:**
- Provides comprehensive help, FAQ, and support information
- Mobile-first design with large fonts and touch targets
- Multiple close methods (button, overlay, back key)
- Auto-integrates with Main Menu via MenuButtonHandler

**How to Use:**
1. Assign help button in MenuButtonHandler
2. Enable auto-create (default)
3. Click help button → Panel shows with full FAQ
4. Users can scroll, read, and close

**Key Files:**
- `HelpPanel.uxml` - Content structure (edit FAQ here)
- `HelpPanelStyles.uss` - Mobile-first styles
- `HelpPanelController.cs` - Show/hide logic
- `MenuButtonHandler.cs` - Main menu integration

**Status:** ✅ Complete and ready to use!

---

_Last Updated: October 24, 2025_
