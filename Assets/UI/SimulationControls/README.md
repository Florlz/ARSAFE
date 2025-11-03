# Simulation Controls – Hamburger Command Drawer

UI Toolkit overlay that anchors a hamburger icon in simulation scenes and reveals a slide-in command drawer. The drawer replaces the old single back button, keeps navigation actions organized, and provides room for future utilities (Help, Diagnostics, etc.) while retaining the existing exit overlay flow.

## ✨ Features

- Compact hamburger toggle styled with ARSafe × USANT branding
- Slide-in panel from the left edge with configurable animation duration
- Data-driven menu entry list (default: Back to Menu, Help & Tips)
- Scrim overlay to capture outside taps and close the drawer
- System back input closes the drawer first, then triggers exit
- Exit overlay retained for the back-to-menu action with configurable copy and delay
- Drawer header Help button that opens an in-place tips overlay
- `UnityEvent<string>` hook (`onMenuEntryInvoked`) for custom actions or analytics

## 🚀 Setup

1. **Scene Object**: Place a GameObject (e.g., `SimulationMenuDrawer`) in the simulation scene.
2. **Components**:
   - Add `UIDocument` → assign shared panel settings.
   - Set `Source Asset` to `SimulationBackButton.uxml` (now contains the hamburger layout).
   - Add `SimulationBackButtonController` (auto injects the default USS).
3. **Inspector Settings**:
   - `Menu Scene Name`: destination scene, defaults to `MainMenu`.
   - `Use Scene Loader Manager`: keep enabled to reuse the animated loader.
   - `Allow System Back Input`: Escape / Android back / controller B closes the drawer or exits.
   - `Exit Overlay Fade Duration` & `Exit Delay Seconds`: tune exit pacing.
   - `Menu Animation Duration`: controls how quickly the drawer hides after closing.
   - `Menu Entries`: edit labels, icons, and action types; add more rows for future features.
   - `On Menu Entry Invoked`: optional event with the action `id` (e.g., `help`) for custom handlers.
   - `Document Sorting Order`: set a higher value if other UI layers (e.g., welcome screen) overlap and block taps; this ensures the hamburger sits above them.

> Leave `Back Button Styles` blank unless you supply a custom USS. The controller loads `Resources/UI/SimulationControls/SimulationBackButtonStyles.uss` automatically.

## 🎛️ Menu Entry Actions

| Action Type | Behavior |
|-------------|----------|
| **BackToMenu** | Closes drawer, shows exit overlay, waits for delay, returns to menu scene. |
| **Help** | Shows the built-in help overlay and invokes `onMenuEntryInvoked` with the entry id. |
| **Custom** | Only triggers `onMenuEntryInvoked` (script the behavior externally). |

Each entry exposes `id`, `label`, and `icon`. Icons can be glyphs (e.g., `←`, `❓`, `⚙`) or plain text.

## 🧠 Drawer Behavior

- **Open**: Tapping the hamburger button reveals the drawer and fades in the scrim.
- **Close**: Tap the close button, the scrim, or press system back. Closing toggles classes and uses the animation duration before hiding the drawer.
- **Input Guard**: While the drawer is open, system back only closes the drawer—no immediate exit.
- **Accessibility**: Buttons use bold typography and high contrast; add additional tooling (sounds, focus management) as needed for gamepad navigation.

### Layering Guidance
- If you can't click the hamburger, another canvas/`UIDocument` may be above it. Increase the `Document Sorting Order` on this component.
- Keep the `simulation-controls-root` compact (top-left). Avoid full-screen roots unless you set `picking-mode: Ignore` on the root so it doesn't intercept taps intended for other UI.

## 💡 Help Overlay

- Access the overlay via the header `?` button or the Help menu action.
- Displays curated AR control reminders plus troubleshooting tips.
- Captures input while visible so the drawer stays put until the close button is pressed.
- Automatically hides if the drawer closes or the controller disables.
- The `help` action id still fires through `onMenuEntryInvoked` for custom instrumentation.

## 🪄 Exit Overlay

- Markup and styling remain (`exit-overlay`, `exit-overlay__card`, etc.).
- Triggered only by the `BackToMenu` entry or hardware/system back when the drawer is closed.
- Customize title/subtitle via inspector fields.

## 🛠️ Troubleshooting

| Symptom | Fix |
|---------|-----|
| Drawer never appears | Ensure `menu-toggle-button`, `menu-drawer`, and `menu-scrim` remain named as in the shipped UXML. |
| Scrim stays visible | Verify `Menu Animation Duration` > 0.05 (schedule uses this value). |
| Help button does nothing | Ensure the header `menu-help-button` element is intact and the controller initialized; `onMenuEntryInvoked` still fires with `help` for external listeners. |
| Exit overlay missing | Confirm initialization logs and that overlay element names are unchanged. |

## 📦 File Locations

- UXML: `Assets/UI/SimulationControls/SimulationBackButton.uxml`
- USS: `Assets/UI/SimulationControls/Resources/UI/SimulationControls/SimulationBackButtonStyles.uss`
- Controller: `Assets/UI/SimulationControls/Scripts/SimulationBackButtonController.cs`
- Design notes: `Design/SimulationControlHamburgerPlan.md`

Keep documentation and context memory updated if you introduce new menu entries or change default behavior.
