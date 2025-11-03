# Welcome Screen Banner Setup Guide

## Overview
This guide explains how to configure banner images for the enhanced ARSAFE welcome screen.

## What Are Banner Images?
Banner images are full-width hero images displayed at the top of the welcome card. They provide immediate visual context for each disaster scenario, making the welcome screen more engaging and informative.

## Setup Instructions

### Step 1: Prepare Banner Images

#### Image Specifications
- **Recommended Size**: 1280×400px (3.2:1 aspect ratio)
- **Minimum Size**: 640×200px
- **Format**: PNG (for transparency) or JPG
- **File Size**: Keep under 500KB for performance
- **Content**: Disaster-relevant imagery (fire, flood, earthquake, etc.)

#### Design Tips
- Use high-contrast images that remain visible against dark backgrounds
- Avoid placing critical text in the banner (it's decorative)
- Consider the top corners will be rounded (28px radius)
- Center important visual elements vertically

### Step 2: Import Images to Unity

1. **Create Banner Directory** (optional but recommended):
   ```
   Assets/UI/Welcome/Resources/Banners/
   ```

2. **Import Your Images**:
   - Drag banner images into the directory
   - Select each image in the Project window

3. **Configure Import Settings**:
   - **Texture Type**: Sprite (2D and UI)
   - **Sprite Mode**: Single
   - **Pixels Per Unit**: 100
   - **Filter Mode**: Bilinear
   - **Max Size**: 2048 (or appropriate for your image)
   - **Compression**: Normal Quality or High Quality
   - Click **Apply**

### Step 3: Configure WelcomeScreenManager

#### Find the WelcomeScreenManager
- Look for the `WelcomeScreenManager` GameObject in your scene
- If it doesn't exist, it will be auto-created at runtime (check DontDestroyOnLoad objects)
- Or find the prefab/component in: `Assets/ARSafe_ModularSystem/Scripts/UI/WelcomeScreenManager.cs`

#### Set Default Banner (Optional)
1. Select the `WelcomeScreenManager` GameObject
2. In the Inspector, find the **Content** section
3. Locate the `Default Banner Image` field
4. Drag your generic/default banner sprite here

This banner will be used when:
- No disaster-specific banner is configured
- The disaster type is `None`
- As a fallback for any scenario

#### Configure Disaster-Specific Banners

1. In the Inspector, find `Disaster Content Overrides` list
2. Expand the list (or click + to add new entries)
3. For each disaster scenario:

##### Fire Scenario Example:
```
Element 0
├─ Disaster Type: Fire
├─ Banner Override: [Your Fire Banner Sprite]
├─ Icon Override: [Optional - existing fire icon]
├─ Title Override: "Fire Emergency Training"
├─ Description Override: [Optional custom text]
├─ Confirm Button Override: [Optional custom button text]
└─ Skip Button Override: [Optional custom button text]
```

##### Flood Scenario Example:
```
Element 1
├─ Disaster Type: Flood
├─ Banner Override: [Your Flood Banner Sprite]
├─ Icon Override: [Optional - existing flood icon]
├─ Title Override: "Flood Emergency Training"
└─ ... (other optional fields)
```

##### Earthquake Scenario Example:
```
Element 2
├─ Disaster Type: Earthquake
├─ Banner Override: [Your Earthquake Banner Sprite]
├─ Icon Override: [Optional - existing earthquake icon]
├─ Title Override: "Earthquake Safety Training"
└─ ... (other optional fields)
```

### Step 4: Using Existing Assets

Your project already has some disaster-related images:

#### Fire Banner
- **Existing Asset**: `Assets/firelogo.jpg`
- Import this and assign to Fire disaster override

#### Flood Banner
- **Existing Asset**: `Assets/floodlogo.png`
- Import this and assign to Flood disaster override

#### Earthquake Banner
- **Status**: You'll need to create or source this image
- Recommended: Look for earthquake crack, structural damage, or seismic wave imagery

### Step 5: Test Your Setup

1. **Play the scene** in Unity Editor
2. **Trigger the welcome screen** (should show on first load)
3. **Verify**:
   - Banner displays correctly
   - Image is not stretched or distorted
   - Top corners are rounded
   - Content below banner is properly spaced
   - Icon still displays below banner

4. **Test each disaster type**:
   - Switch disaster type in your menu
   - Verify correct banner shows for each scenario
   - Check fallback to default banner works

## Advanced Configuration

### Banner-Only Design (No Default)
If you want banners to be optional:
1. Leave `Default Banner Image` empty
2. Only set `Banner Override` for specific disasters
3. Scenarios without banners will show soft accent background

### Hiding Banner Completely
To disable the banner feature:
1. Leave all banner fields empty
2. The banner element will be hidden (DisplayStyle.None)
3. Layout will adjust automatically

### Custom Banner Sizes
While 200px height is default, you can modify in USS:
```css
.welcome-banner {
    height: 180px; /* Adjust as needed */
}
```

### Animated Banners (Future Enhancement)
Currently static images only. For animated banners, consider:
- Using video textures (advanced)
- Animated sprite sheets
- Runtime animation via C# scripts

## Troubleshooting

### Banner Not Showing
- ✓ Check sprite is assigned in Inspector
- ✓ Verify texture import settings (Sprite 2D/UI)
- ✓ Check Console for errors
- ✓ Ensure `WelcomeScreenManager` is active

### Banner Looks Stretched
- ✓ Use correct aspect ratio (3.2:1 recommended)
- ✓ Check import max size isn't too low
- ✓ Verify sprite import mode is "Single"

### Wrong Banner Displays
- ✓ Check `Disaster Type` enum matches your scenario
- ✓ Verify banner override is assigned to correct entry
- ✓ Check if default banner is overriding

### Banner Too Dark/Bright
- ✓ Adjust image brightness in image editor before import
- ✓ Consider adding subtle gradient overlay
- ✓ Check monitor/device brightness settings

## Best Practices

### Image Content
✓ **Do**: Use clear, recognizable disaster imagery
✓ **Do**: Maintain consistent visual style across banners
✓ **Do**: Test on multiple devices and screen sizes
✗ **Don't**: Use text in banner (it may not scale well)
✗ **Don't**: Use extremely dark or light images
✗ **Don't**: Include company logos (use icon for branding)

### Performance
✓ Compress images before import
✓ Use appropriate texture sizes (don't import 4K for mobile)
✓ Consider atlas packing for multiple banners
✓ Test loading times on target devices

### Accessibility
✓ Banner should enhance, not be critical to understanding
✓ All important information should be in text below
✓ Consider color-blind friendly imagery
✓ Maintain good contrast for visibility

## Example Configuration

Here's a complete example setup in the Inspector:

```
WelcomeScreenManager
├─ Content
│  ├─ Force Show Welcome: ☐
│  ├─ Default Banner Image: arsafe_generic_banner
│  ├─ Modal Icon: arsafe_icon_round
│  ├─ Modal Title: "Welcome to ARSAFE Training"
│  └─ ... (other content fields)
│
└─ Disaster Content Overrides (Size: 3)
   ├─ Element 0 (Fire)
   │  ├─ Disaster Type: Fire
   │  ├─ Banner Override: fire_banner_1280x400
   │  ├─ Icon Override: fire_icon_round
   │  └─ Title Override: "Fire Emergency Training"
   │
   ├─ Element 1 (Flood)
   │  ├─ Disaster Type: Flood
   │  ├─ Banner Override: flood_banner_1280x400
   │  ├─ Icon Override: flood_icon_round
   │  └─ Title Override: "Flood Safety Training"
   │
   └─ Element 2 (Earthquake)
      ├─ Disaster Type: Earthquake
      ├─ Banner Override: earthquake_banner_1280x400
      ├─ Icon Override: earthquake_icon_round
      └─ Title Override: "Earthquake Preparedness"
```

## Resources

### Free Image Sources (for reference)
- Unsplash.com - High-quality free images
- Pexels.com - Free stock photos
- Pixabay.com - Free images and vectors

### Image Editing Tools
- Photoshop/GIMP - Professional editing
- Canva - Quick online editing
- Figma - Design and prototyping

### Unity Documentation
- [Sprites Documentation](https://docs.unity3d.com/Manual/Sprites.html)
- [UI Toolkit Documentation](https://docs.unity3d.com/Manual/UIElements.html)
- [Texture Import Settings](https://docs.unity3d.com/Manual/class-TextureImporter.html)

---

**Need Help?**
- Check the console for error messages
- Review the main README.md in Assets/UI/Welcome/
- Verify all import settings match this guide
- Test with a simple solid color image first