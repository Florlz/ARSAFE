/*
 * ARCHITECTURE PLAN: MainMenuLocationController
 *
 * PURPOSE:
 *   - Present location selection UI in MainMenu scene (before AR scene loads)
 *   - Use static LocationData instead of ARSafeActivationController (not available in MainMenu)
 *   - Store selected location in SelectedLocationManager for MainScene to use
 *   - Trigger scene load after user selects location
 *
 * DEPENDENCIES:
 *   - Unity APIs: UIToolkit (UIDocument, VisualElement, Button, TextField, ScrollView)
 *   - Project Scripts: LocationData (static location list), SelectedLocationManager
 *   - bl_SceneLoaderManager (for scene transitions)
 *
 * DATA FLOW:
 *   - OnEnable → Load locations from LocationData.Locations
 *   - Populate location list → User selects location
 *   - Confirm button → SelectedLocationManager.SetSelectedLocation() → Load MainScene
 *   - Skip button → Clear selection → Load MainScene (auto-detection)
 *
 * PERFORMANCE CONSIDERATIONS:
 *   - Cache UI element queries in Awake/OnEnable
 *   - Use string filtering on search instead of rebuilding entire list
 *   - Minimal allocations in search/filter logic
 */

using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Lovatto.SceneLoader;

namespace ARSAFE.UI
{
    /// <summary>
    /// Controls the Location Selection UI in the MainMenu scene.
    /// Allows users to choose their starting Area Target before the AR scene loads.
    /// </summary>
    public class MainMenuLocationController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Scene Settings")]
        [SerializeField] private string mainSceneName = "MainScene";

        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = true;

        // Root elements
        private VisualElement root;
        private VisualElement overlay;
        private VisualElement card;

        // Header elements
        private Label headerTitle;
        private Label headerSubtitle;
        private Button backButton;

        // Search elements
        private TextField searchInput;

        // Location list elements
        private ScrollView locationListScroll;
        private VisualElement locationList;

        // Filter chip buttons
        private Button filterAll;
        private Button filterRooms;
        private Button filterHallways;
        private Button filterStairs;
        private Button filterCanteen;
        private Button filterEvacuation;

        // Footer button elements
        private Button skipButton;
        private Button confirmButton;

        // Data
        private List<LocationInfo> availableLocations = new List<LocationInfo>();
        private List<VisualElement> locationItemElements = new List<VisualElement>();
        private LocationInfo selectedLocation = null;
        private string currentSearchQuery = "";
        private LocationType currentFilter = LocationType.All;

        #region Unity Lifecycle

        private void Awake()
        {
            // Auto-find UIDocument if not assigned
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
                if (uiDocument == null)
                {
                    LogError("UIDocument component not found on MainMenuLocationController GameObject!");
                    return;
                }
            }
        }

        private void OnEnable()
        {
            if (uiDocument == null) return;

            root = uiDocument.rootVisualElement;
            if (root == null)
            {
                LogError("Root visual element is null!");
                return;
            }

            // If Source Asset is assigned in UIDocument, visual tree is already loaded
            CacheUIElements();
            
            // Force hide overlay immediately (whether loaded from Source Asset or built in code)
            if (overlay != null)
            {
                overlay.style.display = DisplayStyle.None;
                overlay.style.opacity = 0f;
                overlay.pickingMode = PickingMode.Ignore;
            }
            
            RegisterCallbacks();
            LoadAvailableLocations();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
        }

        #endregion

        #region UI Setup

        /// <summary>
        /// Cache all UI element references from the UXML document.
        /// </summary>
        private void CacheUIElements()
        {
            // Root elements
            overlay = root.Q<VisualElement>("location-selection-overlay");
            card = root.Q<VisualElement>("location-selection-card");

            // Header
            headerTitle = root.Q<Label>("location-header__title");
            headerSubtitle = root.Q<Label>("location-header-subtitle");
            backButton = root.Q<Button>("location-back-button");

            // Search
            searchInput = root.Q<TextField>("location-search-input");

            // Location list
            locationListScroll = root.Q<ScrollView>("location-list-scroll");
            locationList = root.Q<VisualElement>("location-list");

            // Filter chips
            filterAll = root.Q<Button>("filter-all");
            filterRooms = root.Q<Button>("filter-rooms");
            filterHallways = root.Q<Button>("filter-hallways");
            filterStairs = root.Q<Button>("filter-stairs");
            filterCanteen = root.Q<Button>("filter-canteen");
            filterEvacuation = root.Q<Button>("filter-evacuation");

            // Footer buttons
            skipButton = root.Q<Button>("location-skip-button");
            confirmButton = root.Q<Button>("location-confirm-button");

            // Validate critical elements
            if (overlay == null) LogError("location-selection-overlay not found in UXML!");
            if (locationList == null) LogError("location-list not found in UXML!");
            if (skipButton == null) LogError("location-skip-button not found in UXML!");
            if (confirmButton == null) LogError("location-confirm-button not found in UXML!");
            if (backButton == null) LogWarning("location-back-button not found in UXML!");
        }

        /// <summary>
        /// Register event callbacks for interactive elements.
        /// </summary>
        private void RegisterCallbacks()
        {
            if (backButton != null)
            {
                backButton.clicked += OnBackButtonClicked;
                Log($"Back button callback registered successfully.");
            }
            else
            {
                LogWarning("Cannot register back button callback - button is null!");
            }

            if (searchInput != null)
            {
                searchInput.RegisterValueChangedCallback(OnSearchInputChanged);
            }

            // Filter chip callbacks
            if (filterAll != null)
                filterAll.clicked += () => OnFilterChanged(LocationType.All);
            if (filterRooms != null)
                filterRooms.clicked += () => OnFilterChanged(LocationType.Room);
            if (filterHallways != null)
                filterHallways.clicked += () => OnFilterChanged(LocationType.Hallway);
            if (filterStairs != null)
                filterStairs.clicked += () => OnFilterChanged(LocationType.Stairs);
            if (filterCanteen != null)
                filterCanteen.clicked += () => OnFilterChanged(LocationType.Canteen);
            if (filterEvacuation != null)
                filterEvacuation.clicked += () => OnFilterChanged(LocationType.Evacuation);

            if (skipButton != null)
            {
                skipButton.clicked += OnSkipButtonClicked;
            }

            if (confirmButton != null)
            {
                confirmButton.clicked += OnConfirmButtonClicked;
            }
        }

        /// <summary>
        /// Unregister event callbacks.
        /// </summary>
        private void UnregisterCallbacks()
        {
            if (backButton != null)
            {
                backButton.clicked -= OnBackButtonClicked;
            }

            if (searchInput != null)
            {
                searchInput.UnregisterValueChangedCallback(OnSearchInputChanged);
            }

            // Unregister filter chip callbacks
            if (filterAll != null)
                filterAll.clicked -= () => OnFilterChanged(LocationType.All);
            if (filterRooms != null)
                filterRooms.clicked -= () => OnFilterChanged(LocationType.Room);
            if (filterHallways != null)
                filterHallways.clicked -= () => OnFilterChanged(LocationType.Hallway);
            if (filterStairs != null)
                filterStairs.clicked -= () => OnFilterChanged(LocationType.Stairs);
            if (filterCanteen != null)
                filterCanteen.clicked -= () => OnFilterChanged(LocationType.Canteen);
            if (filterEvacuation != null)
                filterEvacuation.clicked -= () => OnFilterChanged(LocationType.Evacuation);

            if (skipButton != null)
            {
                skipButton.clicked -= OnSkipButtonClicked;
            }

            if (confirmButton != null)
            {
                confirmButton.clicked -= OnConfirmButtonClicked;
            }

            // Unregister location item click callbacks
            foreach (var item in locationItemElements)
            {
                item.UnregisterCallback<ClickEvent>(OnLocationItemClicked);
            }
        }

        #endregion

        #region Data Loading

        /// <summary>
        /// Load available locations from static LocationData.
        /// </summary>
        private void LoadAvailableLocations()
        {
            availableLocations.Clear();

            if (LocationData.Locations == null || LocationData.Locations.Count == 0)
            {
                LogWarning("No locations found in LocationData.");
                ShowEmptyState("No locations available.");
                return;
            }

            availableLocations = new List<LocationInfo>(LocationData.Locations);
            Log($"Loaded {availableLocations.Count} available locations.");

            PopulateLocationList();
        }

        #endregion

        #region UI Population

        /// <summary>
        /// Populate the location list with items based on available locations.
        /// </summary>
        private void PopulateLocationList()
        {
            if (locationList == null)
            {
                LogError("locationList is null, cannot populate!");
                return;
            }

            // Clear existing items
            locationList.Clear();
            locationItemElements.Clear();

            if (availableLocations.Count == 0)
            {
                ShowEmptyState("No locations available.");
                return;
            }

            // Filter by location type
            var filteredLocations = availableLocations;
            if (currentFilter != LocationType.All)
            {
                filteredLocations = filteredLocations
                    .Where(loc => loc.locationType == currentFilter)
                    .ToList();
            }

            // Filter by search query
            if (!string.IsNullOrEmpty(currentSearchQuery))
            {
                filteredLocations = filteredLocations
                    .Where(loc =>
                        loc.displayName.ToLower().Contains(currentSearchQuery.ToLower()) ||
                        loc.description.ToLower().Contains(currentSearchQuery.ToLower()) ||
                        loc.targetName.ToLower().Contains(currentSearchQuery.ToLower())
                    )
                    .ToList();
            }

            if (filteredLocations.Count == 0)
            {
                string message = !string.IsNullOrEmpty(currentSearchQuery)
                    ? $"No locations match \"{currentSearchQuery}\""
                    : $"No {currentFilter} locations available";
                ShowEmptyState(message);
                return;
            }

            // Create location item for each location
            foreach (var location in filteredLocations)
            {
                var locationItem = CreateLocationItem(location);
                locationList.Add(locationItem);
                locationItemElements.Add(locationItem);
            }

            Log($"Populated location list with {filteredLocations.Count} items.");

            // Disable confirm button initially (no selection)
            if (confirmButton != null)
            {
                confirmButton.SetEnabled(false);
            }
        }

        /// <summary>
        /// Create a location item visual element for the given location.
        /// </summary>
        private VisualElement CreateLocationItem(LocationInfo location)
        {
            // Main container
            var item = new VisualElement();
            item.AddToClassList("location-item");
            item.userData = location; // Store reference

            // Content container (name + description)
            var content = new VisualElement();
            content.AddToClassList("location-item__content");

            // Name label
            var nameLabel = new Label(location.displayName);
            nameLabel.AddToClassList("location-item__name");
            content.Add(nameLabel);

            // Description label
            var descriptionLabel = new Label(location.description);
            descriptionLabel.AddToClassList("location-item__description");
            content.Add(descriptionLabel);

            item.Add(content);

            // Checkmark icon (shown when selected)
            var checkIcon = new VisualElement();
            checkIcon.AddToClassList("location-item__check");
            item.Add(checkIcon);

            // Click callback
            item.RegisterCallback<ClickEvent>(OnLocationItemClicked);

            return item;
        }

        /// <summary>
        /// Show empty state message in the location list.
        /// </summary>
        private void ShowEmptyState(string message)
        {
            if (locationList == null) return;

            locationList.Clear();
            locationItemElements.Clear();

            var emptyLabel = new Label(message);
            emptyLabel.AddToClassList("location-placeholder");
            locationList.Add(emptyLabel);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle search input changes.
        /// </summary>
        private void OnSearchInputChanged(ChangeEvent<string> evt)
        {
            currentSearchQuery = evt.newValue;
            Log($"Search query changed: {currentSearchQuery}");
            PopulateLocationList(); // Re-populate with filtered results
        }

        /// <summary>
        /// Handle filter chip click.
        /// </summary>
        private void OnFilterChanged(LocationType newFilter)
        {
            currentFilter = newFilter;
            Log($"Filter changed to: {newFilter}");

            // Update filter chip visual states
            UpdateFilterChipStates();

            // Re-populate list with new filter
            PopulateLocationList();
        }

        /// <summary>
        /// Update the visual state of filter chips based on current filter.
        /// </summary>
        private void UpdateFilterChipStates()
        {
            // Remove active class from all chips
            filterAll?.RemoveFromClassList("filter-chip--active");
            filterRooms?.RemoveFromClassList("filter-chip--active");
            filterHallways?.RemoveFromClassList("filter-chip--active");
            filterStairs?.RemoveFromClassList("filter-chip--active");
            filterCanteen?.RemoveFromClassList("filter-chip--active");
            filterEvacuation?.RemoveFromClassList("filter-chip--active");

            // Add active class to current filter chip
            switch (currentFilter)
            {
                case LocationType.All:
                    filterAll?.AddToClassList("filter-chip--active");
                    break;
                case LocationType.Room:
                    filterRooms?.AddToClassList("filter-chip--active");
                    break;
                case LocationType.Hallway:
                    filterHallways?.AddToClassList("filter-chip--active");
                    break;
                case LocationType.Stairs:
                    filterStairs?.AddToClassList("filter-chip--active");
                    break;
                case LocationType.Canteen:
                    filterCanteen?.AddToClassList("filter-chip--active");
                    break;
                case LocationType.Evacuation:
                    filterEvacuation?.AddToClassList("filter-chip--active");
                    break;
            }
        }

        /// <summary>
        /// Handle location item click.
        /// </summary>
        private void OnLocationItemClicked(ClickEvent evt)
        {
            var clickedItem = evt.currentTarget as VisualElement;
            if (clickedItem == null || clickedItem.userData == null) return;

            var location = clickedItem.userData as LocationInfo;
            if (location == null) return;

            // Deselect all items
            foreach (var item in locationItemElements)
            {
                item.RemoveFromClassList("location-item--selected");
            }

            // Select clicked item
            clickedItem.AddToClassList("location-item--selected");
            selectedLocation = location;

            // Enable confirm button
            if (confirmButton != null)
            {
                confirmButton.SetEnabled(true);
            }

            Log($"Selected location: {location.displayName} ({location.targetName})");
        }

        /// <summary>
        /// Handle back button click - returns to main menu by hiding the location selection overlay.
        /// </summary>
        private void OnBackButtonClicked()
        {
            Log("★★★ BACK BUTTON CLICKED ★★★");

            // Clear any selected location
            selectedLocation = null;

            // Disable confirm button
            if (confirmButton != null)
            {
                confirmButton.SetEnabled(false);
                Log("Confirm button disabled.");
            }

            // Clear search input
            if (searchInput != null)
            {
                searchInput.value = "";
                currentSearchQuery = "";
                Log("Search cleared.");
            }

            // Hide overlay with multiple safeguards
            if (overlay != null)
            {
                overlay.style.display = DisplayStyle.None;
                overlay.style.opacity = 0f;
                overlay.pickingMode = PickingMode.Ignore;
                overlay.visible = false;  // Extra insurance for complete hiding

                Log("Location selection overlay hidden successfully.");
                Log("Returned to main menu - user can now select disaster type.");
            }
            else
            {
                LogError("Cannot hide overlay - overlay element is null!");
            }
        }

        /// <summary>
        /// Handle skip button click (auto-detection fallback).
        /// </summary>
        private void OnSkipButtonClicked()
        {
            Log("Skip button clicked - using auto-detection.");

            // Clear selection (MainScene will use auto-detection)
            SelectedLocationManager.ClearSelectedLocation();

            // Load MainScene
            LoadMainScene();
        }

        /// <summary>
        /// Handle confirm button click.
        /// </summary>
        private void OnConfirmButtonClicked()
        {
            if (selectedLocation == null)
            {
                LogWarning("Confirm clicked but no location selected!");
                return;
            }

            Log($"Confirm clicked - selected location: {selectedLocation.displayName} ({selectedLocation.targetName})");

            // Store selected location in persistent manager
            SelectedLocationManager.SetSelectedLocation(selectedLocation.targetName);

            // Load MainScene
            LoadMainScene();
        }

        /// <summary>
        /// Load the MainScene using bl_SceneLoaderManager
        /// </summary>
        private void LoadMainScene()
        {
            Log($"Loading scene: {mainSceneName}");

            // Hide overlay before loading
            HideOverlay();

            // Use bl_SceneLoaderManager for consistent loading experience
            bl_SceneLoaderManager.LoadScene(mainSceneName);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Show the location selection overlay.
        /// Call this from MenuButtonHandler after user selects disaster type.
        /// </summary>
        public void ShowOverlay()
        {
            if (overlay == null)
            {
                LogError("Cannot show overlay - overlay element is null!");
                return;
            }

            Log("Showing location selection overlay.");
            overlay.style.display = DisplayStyle.Flex;
            // Re-enable interactivity and visibility (these were disabled in OnEnable or by back button)
            overlay.style.opacity = 1f;
            overlay.pickingMode = PickingMode.Position;
            overlay.visible = true;  // Restore visibility in case back button set it to false
            overlay.Focus();

            // Explicitly enable back button for mobile touch input
            if (backButton != null)
            {
                backButton.SetEnabled(true);
                backButton.pickingMode = PickingMode.Position;
                Log("Back button explicitly enabled for interaction.");
            }
            else
            {
                LogWarning("Back button is null in ShowOverlay()!");
            }

            // Reload locations in case they changed
            LoadAvailableLocations();

            // Clear search
            if (searchInput != null)
            {
                searchInput.value = "";
                currentSearchQuery = "";
            }

            // Clear selection
            selectedLocation = null;
            if (confirmButton != null)
            {
                confirmButton.SetEnabled(false);
            }
        }

        /// <summary>
        /// Hide the location selection overlay.
        /// </summary>
        public void HideOverlay()
        {
            if (overlay == null) return;

            Log("Hiding location selection overlay.");
            overlay.style.display = DisplayStyle.None;
            overlay.style.opacity = 0f;
            overlay.pickingMode = PickingMode.Ignore;
        }

        /// <summary>
        /// Check if the overlay is currently visible.
        /// </summary>
        public bool IsOverlayVisible()
        {
            if (overlay == null) return false;
            return overlay.style.display == DisplayStyle.Flex;
        }

        #endregion

        #region Debug Logging

        private void Log(string message)
        {
            if (!enableDebugLogs) return;
            Debug.Log($"<color=cyan>[MainMenuLocation]</color> {message}");
        }

        private void LogWarning(string message)
        {
            if (!enableDebugLogs) return;
            Debug.LogWarning($"[MainMenuLocation] {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"[MainMenuLocation] {message}");
        }

        #endregion
    }
}
