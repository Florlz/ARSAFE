# ARSAFE – Augmented Reality Smart Assistance

An offline augmented reality app for Android that teaches disaster preparedness. Pointing the phone at markers placed around a building shows fire, earthquake and flood scenarios in place, and guides the user toward the exits.

Capstone project (2025–2026), where I was lead developer. The capstone earned a final grade of 1.1.

## Features

- **Three disaster scenarios**: fire, earthquake (camera shake, floor cracks, falling debris) and flood (rising water per area).
- **Multi-area tracking**: Vuforia image targets across several areas, with anchor switching as the user moves between them.
- **Proximity-based content**: scenario content appears only near the relevant target, with boundaries between adjacent areas.
- **Navigation guidance**: virtual exit markers and route validation.
- **Mobile-first UI** built with Unity UI Toolkit, plus a loading flow, a welcome screen and an on-device debug overlay.
- **Works offline**; no network connection is needed during use.

## Tech

Unity 6, C#, Vuforia Engine 11, Universal Render Pipeline, UI Toolkit, Android.

## Repository contents

This repository holds the project's scripts and UI:

- `Assets/ARSafe_ModularSystem/Scripts/`: tracking, activation, proximity, scenarios, navigation
- `Assets/Editor/`: Unity editor tools used during development
- `Assets/UI/`: UI Toolkit screens and styles
- `Assets/Scripts/`: shared utilities such as the debug logger

Unity project settings, 3D models and third-party assets are not included.
