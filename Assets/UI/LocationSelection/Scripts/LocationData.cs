/*
 * ARCHITECTURE PLAN: LocationData
 *
 * PURPOSE:
 *   - Static configuration of available Area Target locations
 *   - Bridge between MainMenu (no AR scene) and MainScene (has Area Targets)
 *   - Provide display-friendly names and descriptions
 *
 * DEPENDENCIES:
 *   - Unity APIs: ScriptableObject pattern (static data)
 *   - None (pure data structure)
 *
 * DATA FLOW:
 *   - MainMenu → LocationData.Locations → Display list
 *   - User selects → Store targetName
 *   - MainScene → Find Area Target by targetName → Activate
 *
 * PERFORMANCE CONSIDERATIONS:
 *   - Static readonly data (zero allocation)
 *   - No runtime instantiation
 */

using System;
using System.Collections.Generic;

namespace ARSAFE.UI
{
    /// <summary>
    /// Represents a single selectable Area Target location
    /// </summary>
    [Serializable]
    public class LocationInfo
    {
        /// <summary>
        /// GameObject name of the Area Target in MainScene (exact match required)
        /// </summary>
        public string targetName;

        /// <summary>
        /// User-friendly display name (shown in UI)
        /// </summary>
        public string displayName;

        /// <summary>
        /// Description to help users identify the location
        /// </summary>
        public string description;

        /// <summary>
        /// Location type for visual categorization
        /// </summary>
        public LocationType locationType;

        public LocationInfo(string targetName, string displayName, string description, LocationType locationType = LocationType.Room)
        {
            this.targetName = targetName;
            this.displayName = displayName;
            this.description = description;
            this.locationType = locationType;
        }
    }

    /// <summary>
    /// Location type categorization
    /// </summary>
    public enum LocationType
    {
        All,        // Special filter: Show all locations
        Room,
        Hallway,
        Stairs,
        Outside,
        Canteen,
        Evacuation
    }

    /// <summary>
    /// Static repository of all available Area Target locations in the building.
    /// Maintained manually to match Area Targets in MainScene.
    /// </summary>
    public static class LocationData
    {
        /// <summary>
        /// Complete list of available starting locations.
        /// IMPORTANT: targetName MUST match GameObject name in MainScene exactly!
        /// </summary>
        public static readonly List<LocationInfo> Locations = new List<LocationInfo>
        {
            // === 1ST FLOOR - ROOMS ===
            new LocationInfo("Room101", "Room 101", "First floor - Room 101", LocationType.Room),
            new LocationInfo("Room102", "Room 102", "First floor - Room 102", LocationType.Room),
            new LocationInfo("Room103", "Room 103", "First floor - Room 103", LocationType.Room),
            new LocationInfo("Room104Part1", "Room 104 (Part 1)", "First floor - Room 104 entrance area", LocationType.Room),
            new LocationInfo("Room104Part2", "Room 104 (Part 2)", "First floor - Room 104 inner area", LocationType.Room),
            new LocationInfo("Room105", "Room 105", "First floor - Room 105", LocationType.Room),
            new LocationInfo("Room106", "Room 106", "First floor - Room 106", LocationType.Room),
            new LocationInfo("Room110Part1", "Room 110 (Part 1)", "First floor - Room 110 entrance area", LocationType.Room),
            new LocationInfo("Room110Part2", "Room 110 (Part 2)", "First floor - Room 110 inner area", LocationType.Room),
            new LocationInfo("Room112Part1", "Room 112 (Part 1)", "First floor - Room 112 entrance area", LocationType.Room),
            new LocationInfo("Room112Part2", "Room 112 (Part 2)", "First floor - Room 112 inner area", LocationType.Room),
            new LocationInfo("Room114", "Room 114", "First floor - Room 114", LocationType.Room),
            new LocationInfo("Room116", "Room 116", "First floor - Room 116", LocationType.Room),
            new LocationInfo("Room117Part1", "Room 117 (Part 1)", "First floor - Room 117 entrance area", LocationType.Room),
            new LocationInfo("Room117Part2", "Room 117 (Part 2)", "First floor - Room 117 inner area", LocationType.Room),
            new LocationInfo("Room118Part1", "Room 118 (Part 1)", "First floor - Room 118 entrance area", LocationType.Room),
            new LocationInfo("Room118Part2", "Room 118 (Part 2)", "First floor - Room 118 inner area", LocationType.Room),
            new LocationInfo("CRGirls_First", "CR Girls (1st Floor)", "First floor - Girls' restroom", LocationType.Room),
            new LocationInfo("CRMen_First", "CR Men (1st Floor)", "First floor - Men's restroom", LocationType.Room),

            // === 1ST FLOOR - HALLWAYS RIGHT SIDE ===
            new LocationInfo("1stHallway_RightStairs", "1st Hallway (Right - Stairs)", "First floor - Right hallway near stairs", LocationType.Hallway),
            new LocationInfo("2ndHallway_Right", "2nd Hallway (Right)", "First floor - Right hallway 2nd section", LocationType.Hallway),
            new LocationInfo("3rdHallway_Right", "3rd Hallway (Right)", "First floor - Right hallway 3rd section", LocationType.Hallway),
            new LocationInfo("4thHallway_RightCanteen", "4th Hallway (Right - Canteen)", "First floor - Right hallway near canteen", LocationType.Canteen),
            new LocationInfo("6thHallway_Right", "6th Hallway (Right)", "First floor - Right hallway 6th section", LocationType.Hallway),
            new LocationInfo("7thHallway_Right", "7th Hallway (Right)", "First floor - Right hallway 7th section", LocationType.Hallway),
            new LocationInfo("8thHallway_RightOutside", "8th Hallway (Right - Outside)", "First floor - Right hallway near outside exit", LocationType.Outside),

            // === 1ST FLOOR - HALLWAYS LEFT SIDE ===
            new LocationInfo("12thHallway_LeftStairs", "12th Hallway (Left - Stairs)", "First floor - Left hallway near stairs", LocationType.Hallway),
            new LocationInfo("13thHallway_Left", "13th Hallway (Left)", "First floor - Left hallway 13th section", LocationType.Hallway),
            new LocationInfo("15thHallway_Left", "15th Hallway (Left)", "First floor - Left hallway 15th section", LocationType.Hallway),
            new LocationInfo("16thHallway_LeftCanteen", "16th Hallway (Left - Canteen)", "First floor - Left hallway near canteen", LocationType.Canteen),
            new LocationInfo("17thHallway_Left", "17th Hallway (Left)", "First floor - Left hallway 17th section", LocationType.Hallway),
            new LocationInfo("18thHallway_Left", "18th Hallway (Left)", "First floor - Left hallway 18th section", LocationType.Hallway),
            new LocationInfo("19thHallway_LeftStairs", "19th Hallway (Left - Stairs)", "First floor - Left hallway near stairs", LocationType.Stairs),
            new LocationInfo("20thHallway_LeftEvac", "20th Hallway (Left - Evacuation)", "First floor - Left hallway near evacuation route", LocationType.Evacuation),

            // === 1ST FLOOR - HALLWAYS CENTER/DULO ===
            new LocationInfo("9thHallway_Dulo", "9th Hallway (Center)", "First floor - Center hallway 9th section", LocationType.Hallway),
            new LocationInfo("10thHallway_Dulo", "10th Hallway (Center)", "First floor - Center hallway 10th section", LocationType.Hallway),
            new LocationInfo("11thHallway_Dulo", "11th Hallway (Center)", "First floor - Center hallway 11th section", LocationType.Hallway),

            // === 1ST FLOOR - SPECIAL AREAS ===
            new LocationInfo("EvacToOval", "Evacuation to Oval", "First floor - Evacuation route to oval area", LocationType.Evacuation),

            // === STAIRS (CONNECTING FLOORS) ===
            new LocationInfo("StairsTo2ndFloor_RIghtStairs", "Stairs to 2nd Floor (Right)", "Stairs connecting 1st and 2nd floors - Right side", LocationType.Stairs),

            // === 2ND FLOOR - ROOMS ===
            new LocationInfo("2_Room202B", "Room 202B (2nd Floor)", "Second floor - Room 202B", LocationType.Room),
            new LocationInfo("2_Room203", "Room 203 (2nd Floor)", "Second floor - Room 203", LocationType.Room),
            new LocationInfo("2_Room204", "Room 204 (2nd Floor)", "Second floor - Room 204", LocationType.Room),
            new LocationInfo("2_Room205Part1", "Room 205 Part 1 (2nd Floor)", "Second floor - Room 205 entrance area", LocationType.Room),
            new LocationInfo("2_Room205Part2", "Room 205 Part 2 (2nd Floor)", "Second floor - Room 205 inner area", LocationType.Room),
            new LocationInfo("2_Room206", "Room 206 (2nd Floor)", "Second floor - Room 206", LocationType.Room),
            new LocationInfo("2_CRMen", "CR Men (2nd Floor)", "Second floor - Men's restroom", LocationType.Room),
            new LocationInfo("2_CRGIRLS", "CR Girls (2nd Floor)", "Second floor - Girls' restroom", LocationType.Room),
            new LocationInfo("2_SGOMMR", "SG Office (2nd Floor)", "Second floor - SG Office / Multi-purpose Room", LocationType.Room),
            new LocationInfo("2_DemoRoom1", "Demo Room 1 (2nd Floor)", "Second floor - Demo Room 1", LocationType.Room),
            new LocationInfo("2_DemoRoom1Part2", "Demo Room 1 Part 2 (2nd Floor)", "Second floor - Demo Room 1 inner area", LocationType.Room),
            new LocationInfo("2_DemoRoom2", "Demo Room 2 (2nd Floor)", "Second floor - Demo Room 2", LocationType.Room),
            new LocationInfo("2_FacultyRoom", "Faculty Room (2nd Floor)", "Second floor - Faculty Room", LocationType.Room),
            new LocationInfo("2_FacultyRoom2", "Faculty Room 2 (2nd Floor)", "Second floor - Faculty Room 2", LocationType.Room),

            // === 2ND FLOOR - HALLWAYS LEFT SIDE ===
            new LocationInfo("2_1stHallway_LeftStairs", "1st Hallway Left - Stairs (2nd Floor)", "Second floor - Left hallway near stairs", LocationType.Stairs),
            new LocationInfo("2_2ndHallway_Left", "2nd Hallway Left (2nd Floor)", "Second floor - Left hallway 2nd section", LocationType.Hallway),
            new LocationInfo("2_3rdHallway_Left", "3rd Hallway Left (2nd Floor)", "Second floor - Left hallway 3rd section", LocationType.Hallway),
            new LocationInfo("2_4thHallway_LeftRescan", "4th Hallway Left (2nd Floor)", "Second floor - Left hallway 4th section", LocationType.Hallway),
            new LocationInfo("2_5thHallway_LeftStairs", "5th Hallway Left - Stairs (2nd Floor)", "Second floor - Left hallway near stairs", LocationType.Stairs),

            // === 2ND FLOOR - HALLWAYS CENTER/DULO ===
            new LocationInfo("2_6thHallway_Dulo", "6th Hallway Center (2nd Floor)", "Second floor - Center hallway 6th section", LocationType.Hallway),
            new LocationInfo("2_7thHallway_Dulo", "7th Hallway Center (2nd Floor)", "Second floor - Center hallway 7th section", LocationType.Hallway),

            // === 2ND FLOOR - HALLWAYS RIGHT SIDE ===
            new LocationInfo("2_8thHallway_Right", "8th Hallway Right (2nd Floor)", "Second floor - Right hallway 8th section", LocationType.Hallway),
            new LocationInfo("2_9thHallway_Right", "9th Hallway Right (2nd Floor)", "Second floor - Right hallway 9th section", LocationType.Hallway),
            new LocationInfo("2_10thHallway_Right", "10th Hallway Right (2nd Floor)", "Second floor - Right hallway 10th section", LocationType.Hallway),
            new LocationInfo("2_11thHallway_Right", "11th Hallway Right (2nd Floor)", "Second floor - Right hallway 11th section", LocationType.Hallway),
            new LocationInfo("2_12thHallway_Right", "12th Hallway Right (2nd Floor)", "Second floor - Right hallway 12th section", LocationType.Hallway),
            new LocationInfo("2_13thHallway_Right", "13th Hallway Right (2nd Floor)", "Second floor - Right hallway 13th section", LocationType.Hallway),
        };

        /// <summary>
        /// Find location info by target name
        /// </summary>
        /// <param name="targetName">GameObject name of the Area Target</param>
        /// <returns>LocationInfo if found, null otherwise</returns>
        public static LocationInfo FindByTargetName(string targetName)
        {
            return Locations.Find(loc => loc.targetName == targetName);
        }

        /// <summary>
        /// Get user-friendly display name for a target
        /// </summary>
        /// <param name="targetName">GameObject name of the Area Target</param>
        /// <returns>Display name, or targetName if not found</returns>
        public static string GetDisplayName(string targetName)
        {
            var location = FindByTargetName(targetName);
            return location != null ? location.displayName : targetName;
        }
    }
}
