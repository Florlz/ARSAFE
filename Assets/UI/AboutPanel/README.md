# About Panel Overlay

Provides a UI Toolkit-driven "About" overlay for the Main Menu. The panel highlights the ARSAFE mission, lists team members, and surfaces contact/version details in a glassmorphism card that matches the simulation HUD palette.

## Overview
- **Location:** `Assets/UI/AboutPanel/`
- **Namespace:** `ARSafe.UI`
- **Components:**
  - `AboutPanelController` – Loads the UXML/USS, injects copy, and exposes `Show()`, `Hide()`, and `Toggle()` helpers.
  - `AboutPanelTrigger` – Optional bridge for uGUI buttons that should toggle the panel.
  - `MenuButtonHandler` now knows how to auto-spawn the panel and wire the Main Menu "About" button.
- **Assets:**
  - `Resources/UI/AboutPanel/AboutPanel.uxml`
  - `Resources/UI/AboutPanel/AboutPanelStyles.uss`

## Setup
1. Ensure the Main Menu contains a `MenuButtonHandler`.
2. Assign the "About" button (uGUI) to the new **About Button** field.
3. Optionally drop a preconfigured `AboutPanelController` in the scene and assign it; otherwise, enable `autoCreateAboutPanel` to spawn one at runtime.
4. Set **About Panel Parent** if the auto-created panel should live under a specific hierarchy node, and adjust **About Panel Sorting Order** to keep the overlay above/below other UI Toolkit documents.
5. Update text in `AboutPanelController` (subtitle, description, version, contact, team list) via the inspector.

## Runtime Behaviour
- When the About button is pressed the overlay slides in, captures input, and can be dismissed via the Close button or tapping outside the card.
- General safety copy and team roster are populated from the controller's serialized fields. Add/remove team members directly in the inspector.
- Styles avoid unsupported UI Toolkit features (`gap`, `backdrop-filter`, `transition`).
- While hidden the controller disables its `UIDocument`, ensuring the overlay never blocks the uGUI main menu buttons.

## Adding Developer Images

### Image Requirements
- **Format:** PNG, JPG, or TGA
- **Recommended Size:** 512x512px (square)
- **Aspect Ratio:** 1:1 (images will be cropped to circle)
- **File Size:** < 500KB for mobile performance

### Step-by-Step Guide

1. **Prepare Your Images**
   - Export developer photos as square images (512x512px recommended)
   - Name them descriptively (e.g., `john_doe.png`, `jane_smith.jpg`)

2. **Import to Unity**
   - Place images in: `Assets/UI/AboutPanel/Resources/Images/Developers/`
   - Unity will auto-detect and import them

3. **Configure Import Settings**
   - Select the image in Unity Project window
   - In Inspector, set:
     - **Texture Type:** `Sprite (2D and UI)` or `Default`
     - **Max Size:** 512 or 1024
     - **Compression:** `High Quality` for best results
     - **Android:** Enable `Override for Android` → Format: `ASTC 6x6`
   - Click **Apply**

4. **Assign to Team Members**
   - Find the GameObject with `AboutPanelController` (usually under Main Menu)
   - In Inspector, expand **Team Roster** list
   - For each team member:
     - Set **Name** (e.g., "John Doe")
     - Set **Role** (e.g., "Lead Developer")
     - Drag the developer image from Project window to **Avatar Image** field
   - If no image is assigned, a default user icon (👤) will appear

### Example Configuration
```
Team Members:
├─ [0]
│  ├─ Name: "Florian Monte"
│  ├─ Role: "Project Lead"
│  └─ Avatar Image: florian_monte.png
├─ [1]
│  ├─ Name: "Jane Smith"
│  ├─ Role: "AR Developer"
│  └─ Avatar Image: jane_smith.png
└─ [2]
   ├─ Name: "Bob Johnson"
   ├─ Role: "UI/UX Designer"
   └─ Avatar Image: (None) → Shows 👤 icon
```

### Styling Notes
- Avatars are displayed as **90x90px circles** on mobile
- Images with **border:** 3px orange glow (`rgba(255, 130, 0, 0.5)`)
- **Background scaling:** `scale-and-crop` (centers and fills circle)
- **Fallback:** If no image, shows 👤 emoji on orange background

## Customisation Tips
- Swap the badge text or icon by editing the UXML or by overriding fields in the controller.
- Add additional footer content by cloning `about-footer__text` elements.
- To localise strings, hook a localisation system in `AboutPanelController.PopulateContent()`.
- Adjust avatar size by modifying `.about-team-item__avatar` width/height in USS (line 235-249)

## Verification Checklist
- Pressing the About button shows the panel and blocks other menu actions until dismissed.
- Close button and scrim tap both hide the panel.
- Text matches current ARSAFE messaging and contact details.
- No warnings in the Console about unsupported UI Toolkit properties.
- Developer avatars display as circles with proper scaling
- Fallback icon (👤) appears for team members without images
