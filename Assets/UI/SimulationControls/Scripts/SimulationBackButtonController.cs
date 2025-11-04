using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using Lovatto.SceneLoader;
using ARSafe.Modular;

namespace ARSafe.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class SimulationBackButtonController : MonoBehaviour
    {
        [Header("Layering")]
        [SerializeField]
        [Tooltip("Optional override. Sets the UI Document panel sorting order so the hamburger sits above other canvases (including loading screens).")]
        private int documentSortingOrder = 600;
        private const string HelpActionId = "help";
    private const string LearnActionId = "learn";

        [Header("Navigation")]
        [SerializeField]
        private string menuSceneName = "MainMenu";

        [SerializeField]
        [Tooltip("Use the Scene Loader plugin to return to the menu (recommended). Disable to call SceneManager.LoadScene directly.")]
        private bool useSceneLoaderManager = true;

        [Header("Input")]
        [SerializeField]
        [Tooltip("Allow hardware/system back inputs (Escape/Menu keys, Android back, controller B).")]
        private bool allowSystemBackInput = true;

        [Header("Styling")]
        [SerializeField]
        [Tooltip("Optional override. If null, the controller will auto-load the default stylesheet from Resources/UI/SimulationControls/.")]
        private StyleSheet backButtonStyles;

        [Header("Loading Overlay")]
        [SerializeField]
        [Tooltip("Primary message displayed when exiting the simulation.")]
        private string exitOverlayTitle = "Exiting Simulation";

        [SerializeField]
        [Tooltip("Secondary message displayed beneath the exit title.")]
        private string exitOverlaySubtitle = "Returning to Main Menu...";

        [SerializeField]
        [Tooltip("Seconds before the exit overlay is fully hidden after a fade-out.")]
        private float exitOverlayFadeDuration = 0.3f;

        [SerializeField]
        [Tooltip("Minimum time to show the exit overlay before the menu scene loads.")]
        private float exitDelaySeconds = 1.5f;

        [Header("Menu Drawer")]
        [SerializeField]
        [Tooltip("Seconds to wait before fully hiding the drawer after the close animation starts.")]
        private float menuAnimationDuration = 0.25f;

        [System.Serializable]
        private class MenuEntryConfig
        {
            public string id = "action";
            public string label = "Menu Action";
            [Tooltip("Sprite icon to display (recommended for Android compatibility)")]
            public Sprite iconSprite;
            [Tooltip("Fallback text icon if sprite is not assigned")]
            public string iconText = "•";
            public MenuEntryAction action = MenuEntryAction.Custom;
            public bool enabled = true;
        }

        private enum MenuEntryAction
        {
            BackToMenu,
            Help,
            Learn,
            Relocalize,
            Custom
        }

        [System.Serializable]
        private class LearnEntry
        {
            public DisasterType disasterType = DisasterType.None;
            public string displayName = "Scenario";
            [TextArea(2, 4)] public string summary = "Key context for this scenario.";

            [Header("Preparedness")]
            public string[] beforeActions = new[]
            {
                "Review procedures and confirm your team knows the plan."
            };

            [Header("Immediate Response")]
            public string[] duringActions = new[]
            {
                "Follow the on-screen prompts and stay aware of hazards."
            };

            [Header("Recovery")]
            public string[] afterActions = new[]
            {
                "Account for teammates and report unsafe conditions."
            };

            [Header("Emergency Kit")]
            public string[] supplies = new[]
            {
                "Basic first-aid kit",
                "Portable light source"
            };

            [Header("AR Guidance")]
            public string[] arGuidance = new[]
            {
                "Ensure markers stay within view.",
                "Hold the device steady while content loads."
            };
        }

        [System.Serializable]
        public class MenuActionEvent : UnityEvent<string> { }

        [SerializeField]
        [Tooltip("Menu items rendered inside the drawer. Default entries include Back to Menu and Help. Assign iconSprite in Inspector for Android compatibility.")]
        private List<MenuEntryConfig> menuEntries = new List<MenuEntryConfig>
        {
            new MenuEntryConfig
            {
                id = "back",
                label = "Back to Menu",
                iconText = "←",
                action = MenuEntryAction.BackToMenu,
                enabled = true
            },
            new MenuEntryConfig
            {
                id = "learn",
                label = "Learn This Scenario",
                iconText = "•",
                action = MenuEntryAction.Learn,
                enabled = true
            },
            new MenuEntryConfig
            {
                id = "help",
                label = "Help & Tips",
                iconText = "?",
                action = MenuEntryAction.Help,
                enabled = true
            },
            new MenuEntryConfig
            {
                id = "relocalize",
                label = "Relocalize Position",
                iconText = "•",
                action = MenuEntryAction.Relocalize,
                enabled = true
            }
        };

        [Header("Learn Panel")]
        [SerializeField]
        private string learnOverlayTitle = "Learn the Scenario";

        [SerializeField]
        [TextArea(2, 3)]
        private string learnOverlaySubtitle = "Review what to do for each emergency and how the AR simulation guides you.";

        [SerializeField]
        private List<LearnEntry> learnEntries = new List<LearnEntry>
        {
            new LearnEntry
            {
                disasterType = DisasterType.Earthquake,
                displayName = "Earthquake Response",
                summary = "Strong shaking can topple fixtures and send debris across the room. Focus on Drop, Cover, and Hold On while the simulation tracks debris risks. Earthquakes strike without warning and can cause catastrophic structural damage within seconds.",
                beforeActions = new[]
                {
                    "Secure tall furniture, shelving, and overhead equipment to walls with brackets.",
                    "Identify sturdy cover locations (under desks, doorways) in every work area.",
                    "Review Drop-Cover-Hold On drills with teammates quarterly.",
                    "Store heavy items on lower shelves to prevent falling hazards.",
                    "Keep emergency supplies accessible: water, food, first aid kit, flashlight.",
                    "Know how to shut off gas, water, and electricity if lines are damaged.",
                    "Establish family/team communication plans with out-of-area contacts.",
                    "Install safety film on windows to prevent glass shattering."
                },
                duringActions = new[]
                {
                    "Drop to your hands and knees as soon as the shaking begins.",
                    "Cover your head and neck under sturdy shelter (desk, table) or with your arms.",
                    "Hold on to your shelter and be prepared to move with it during shaking.",
                    "Stay clear of glass windows, mirrors, and hanging fixtures that may fall.",
                    "If outdoors, move away from buildings, streetlights, and utility wires.",
                    "If in a vehicle, pull over safely, stay inside, and avoid overpasses.",
                    "Stay inside until the shaking stops unless fire or structural damage forces you out.",
                    "Do not run outside where falling debris poses the greatest danger."
                },
                afterActions = new[]
                {
                    "Check yourself and others for injuries before moving; provide first aid as needed.",
                    "Expect aftershocks and move cautiously toward designated safe zones.",
                    "Report gas leaks, fires, or structural hazards to emergency teams immediately.",
                    "Inspect building for cracks, damaged utilities, and structural instability.",
                    "Use stairs, never elevators, which may be damaged or lose power.",
                    "Stay away from damaged areas unless you are qualified to help.",
                    "Listen to battery-powered radio for official emergency information.",
                    "Document damage with photos for insurance claims when safe to do so.",
                    "Be prepared for disrupted services (power, water, communications) for days."
                },
                supplies = new[]
                {
                    "Hard hat or other head protection for falling debris.",
                    "Sturdy gloves and closed-toe shoes for navigating debris.",
                    "Flashlight with spare batteries (avoid candles due to fire risk).",
                    "Portable radio or charged mobile device with backup power bank.",
                    "Three-day supply of water (1 gallon per person per day).",
                    "Non-perishable food and manual can opener.",
                    "Comprehensive first aid kit with bandages, antiseptic, medications.",
                    "Whistle to signal for help if trapped under debris.",
                    "Dust masks to protect lungs from airborne particles.",
                    "Plastic sheeting and duct tape for shelter repairs."
                },
                arGuidance = new[]
                {
                    "Stay within the highlighted safe zone until the anchors stabilize.",
                    "Watch for debris indicators and follow evacuation prompts once shaking stops.",
                    "AR overlays show structural damage zones to avoid during aftershocks.",
                    "Follow the AR-guided path to avoid unstable areas and falling hazards."
                }
            },
            new LearnEntry
            {
                disasterType = DisasterType.Fire,
                displayName = "Fire Response & Evacuation",
                summary = "Fire scenarios emphasize fast, safe evacuation while minimizing smoke exposure and heat hazards. Fires can double in size every minute, making immediate action critical. Most fire deaths result from smoke inhalation, not burns.",
                beforeActions = new[]
                {
                    "Test smoke alarms and emergency lighting monthly; replace batteries annually.",
                    "Identify two exit routes from every workspace and practice evacuation drills.",
                    "Keep fire extinguishers visible, unobstructed, and inspected regularly.",
                    "Store flammable materials in proper containers away from heat sources.",
                    "Maintain clear evacuation paths; never block exits or stairwells.",
                    "Install fire-resistant doors and ensure they close automatically.",
                    "Post evacuation maps and emergency numbers in visible locations.",
                    "Train all personnel in fire extinguisher use (P.A.S.S. method).",
                    "Establish a designated outdoor muster point for headcounts.",
                    "Keep important documents in fireproof safes or off-site backups."
                },
                duringActions = new[]
                {
                    "Activate the fire alarm and call for help as soon as fire is detected.",
                    "Stay low under smoke (crawl if necessary) while moving toward the nearest safe exit.",
                    "Check doors for heat with the back of your hand before opening them.",
                    "If a door is hot, use an alternate exit route immediately.",
                    "Close doors behind you to slow fire spread but do not lock them.",
                    "Use stairs instead of elevators during evacuation (power may fail).",
                    "If clothes catch fire: Stop, Drop, and Roll to smother flames.",
                    "Cover your nose and mouth with a cloth to filter smoke if possible.",
                    "Alert others as you evacuate; knock on doors and shout 'Fire!'",
                    "Never go back inside for belongings; alert firefighters if someone is missing."
                },
                afterActions = new[]
                {
                    "Call emergency services (911) once you are safely outside.",
                    "Perform a headcount at the designated muster point to account for everyone.",
                    "Do not re-enter the building until cleared by fire department officials.",
                    "Provide firefighters with information about trapped individuals or hazards.",
                    "Seek medical attention for smoke inhalation, burns, or injuries.",
                    "Contact insurance company to report damage and start claims process.",
                    "Secure the property to prevent unauthorized entry or looting.",
                    "Arrange temporary shelter for displaced occupants.",
                    "Document all damage with photos and video for insurance records.",
                    "Coordinate with authorities before cleanup or salvage operations."
                },
                supplies = new[]
                {
                    "Smoke hood or N95 respirator for respiratory protection.",
                    "ABC fire extinguisher (rated for multiple fire types) and training reference.",
                    "Flashlight or headlamp for navigating dark, smoke-filled corridors.",
                    "Emergency contact list and laminated building evacuation maps.",
                    "Fire blanket for smothering small fires or protecting from heat.",
                    "Battery-powered radio for emergency updates.",
                    "First aid kit with burn treatment supplies (sterile gauze, burn gel).",
                    "Portable ladder or rope escape device for upper floors.",
                    "Reflective safety vests for visibility in smoke.",
                    "Charged mobile phone with backup battery for emergency calls."
                },
                arGuidance = new[]
                {
                    "Follow illuminated exit signage and directional arrows within the AR scene.",
                    "Use AR prompts to avoid blocked passages, flames, and smoke-filled areas.",
                    "AR temperature indicators show heat zones to steer clear of.",
                    "Real-time path recalculation guides you to alternate exits if primary routes are blocked.",
                    "Visual timers remind you to stay low and move quickly during evacuation."
                }
            },
            new LearnEntry
            {
                disasterType = DisasterType.Flood,
                displayName = "Flood Response & Water Safety",
                summary = "Flood simulations focus on vertical evacuation, electrical hazards, and keeping out of moving water. Just six inches of moving water can knock you down; twelve inches can carry away a vehicle. Floodwater often contains contaminants and hidden hazards.",
                beforeActions = new[]
                {
                    "Monitor weather alerts, flood warnings, and facility flood gauges continuously.",
                    "Relocate critical equipment, electronics, and documents above flood level.",
                    "Plan vertical evacuation paths to higher floors, roofs, or elevated areas.",
                    "Identify safe assembly points on high ground outside flood zones.",
                    "Install flood barriers, sandbags, or water-resistant doors where possible.",
                    "Photograph property and inventory belongings for insurance documentation.",
                    "Back up important digital files to cloud storage or off-site locations.",
                    "Prepare emergency kit with water, food, and medical supplies for 72 hours.",
                    "Know how to shut off utilities (gas, electricity, water) safely.",
                    "Arrange transportation and shelter for evacuation if ordered by authorities."
                },
                duringActions = new[]
                {
                    "Avoid walking through moving water deeper than ankle height (6 inches).",
                    "Turn off electrical circuits at the breaker panel if you can reach it safely.",
                    "Move to higher ground immediately; do not wait for water to rise further.",
                    "Follow designated safe routes and avoid shortcuts through floodwater.",
                    "Keep clear of pits, basements, elevators, and low-lying areas that fill quickly.",
                    "Do not drive through flooded roads; turn around if you encounter water.",
                    "Stay away from downed power lines and electrical equipment in water.",
                    "If trapped in a building, go to the highest level (not the attic unless you can break through to the roof).",
                    "Signal for help from windows or rooftops if rescue is needed.",
                    "Listen to battery-powered radio for official evacuation orders and updates."
                },
                afterActions = new[]
                {
                    "Stay away from standing water until it is tested, treated, and cleared by authorities.",
                    "Document flood damage with photos and video when safe to do so for insurance claims.",
                    "Disinfect all surfaces contacted by floodwater using bleach solution (1 cup per gallon of water).",
                    "Check for structural damage before re-entering: cracks, foundation shifts, weakened floors.",
                    "Pump out basements gradually (1/3 per day) to prevent structural collapse.",
                    "Discard contaminated food, medications, and porous materials (mattresses, carpets).",
                    "Wear protective gear (gloves, boots, masks) when cleaning flood-damaged areas.",
                    "Watch for wildlife (snakes, insects) that may have entered during flooding.",
                    "Prevent mold growth by drying structures within 24-48 hours; use fans and dehumidifiers.",
                    "Contact insurance company immediately to report damage and start claims process."
                },
                supplies = new[]
                {
                    "Waterproof boots (steel-toe) and heavy-duty gloves for protection.",
                    "Battery-powered lantern or waterproof flashlight with spare batteries.",
                    "Portable weather radio and fully charged power bank for mobile devices.",
                    "Waterproof bags or containers for electronics, documents, and medications.",
                    "Life jacket or flotation device if trapped in rising water.",
                    "Rope and whistle for signaling rescuers.",
                    "Disinfectants (bleach, antibacterial soap) for cleaning contaminated areas.",
                    "Three-day supply of water and non-perishable food.",
                    "First aid kit with waterproof packaging.",
                    "Important documents in waterproof safe or sealed plastic bags."
                },
                arGuidance = new[]
                {
                    "Observe depth markers and color-coded water level warnings throughout the simulation.",
                    "Locate safe staging areas and vertical evacuation routes identified by AR overlays.",
                    "AR indicators show current dangers submerged in water (electrical, structural).",
                    "Real-time water rise projections help you plan evacuation timing.",
                    "Follow AR-guided paths that avoid deep water, strong currents, and contaminated zones."
                }
            },
            new LearnEntry
            {
                disasterType = DisasterType.GeneralSafety,
                displayName = "General Safety & Emergency Preparedness",
                summary = "Use these comprehensive guidelines for readiness across any emergency situation. Being prepared significantly increases survival rates and reduces panic during actual disasters. Practice these principles regularly to build muscle memory and confidence.",
                beforeActions = new[]
                {
                    "Review facility emergency procedures, evacuation routes, and assembly points with your team quarterly.",
                    "Keep emergency contact lists, medical information, and insurance details current and accessible.",
                    "Audit work areas for loose items, unstable shelving, or hazards that could escalate during incidents.",
                    "Participate in regular safety drills for fire, earthquake, active shooter, and medical emergencies.",
                    "Maintain situational awareness: identify exits, emergency equipment, and AED locations wherever you go.",
                    "Build and maintain emergency supply kits for home, work, and vehicles.",
                    "Learn basic first aid, CPR, and stop-the-bleed techniques through certified training.",
                    "Establish family/team communication plans with out-of-area contacts and meeting points.",
                    "Keep physical fitness levels adequate for emergency evacuation and prolonged stress.",
                    "Document medical conditions, allergies, and medications for emergency responders.",
                    "Secure important documents (ID, insurance, deeds) in fireproof/waterproof storage.",
                    "Install and test smoke detectors, carbon monoxide alarms, and fire extinguishers regularly."
                },
                duringActions = new[]
                {
                    "Stay calm and assess the situation before taking action; panic increases risk.",
                    "Follow incident commander instructions immediately without questioning during crisis.",
                    "Communicate your location, status, and needs clearly to teammates and responders.",
                    "Use protective equipment appropriate to the hazard (masks, gloves, helmets).",
                    "Help others if safe to do so, but never put yourself in unnecessary danger.",
                    "Account for people with disabilities, children, elderly, and non-English speakers.",
                    "Stay informed through official channels (radio, text alerts, emergency apps).",
                    "Document the situation with photos/video if safe; valuable for insurance and investigations.",
                    "Conserve phone battery by limiting non-emergency calls; use text messages instead.",
                    "Mark your location if trapped (write on walls, hang bright cloth from windows).",
                    "Ration water and food supplies if rescue may be delayed.",
                    "Maintain hope and positive mental attitude; many rescues occur hours or days after disasters."
                },
                afterActions = new[]
                {
                    "Report new hazards, injuries, or damages to incident commanders immediately.",
                    "Assist with accountability and headcount at designated muster points.",
                    "Provide accurate witness statements to investigators and authorities.",
                    "Seek medical evaluation even for minor injuries (shock can mask serious trauma).",
                    "Access mental health support for yourself and team; trauma responses are normal.",
                    "Participate in after-action reviews and debriefs to capture lessons learned.",
                    "Document timeline of events while fresh in memory for insurance and legal purposes.",
                    "Follow return-to-work or return-to-home protocols; do not rush reoccupation.",
                    "Share information with family and colleagues to reduce rumor and misinformation.",
                    "Review and update emergency plans based on experience from the incident.",
                    "Recognize and support team members showing signs of PTSD or prolonged stress.",
                    "Express gratitude to responders, volunteers, and mutual aid teams."
                },
                supplies = new[]
                {
                    "Comprehensive first aid kit with trauma supplies (tourniquets, chest seals, Israeli bandages).",
                    "Personal medications (7-day supply minimum) with prescriptions and dosage information.",
                    "Multi-tool with knife, pliers, screwdrivers, and can opener.",
                    "Durable work gloves (leather or Kevlar) for handling debris safely.",
                    "Fully charged mobile phone with backup battery bank (10,000+ mAh capacity).",
                    "Battery-powered or hand-crank emergency radio for official updates.",
                    "Laminated copies of evacuation maps, contact lists, and meeting points.",
                    "Water (1 gallon per person per day for 3 days minimum).",
                    "Non-perishable food (3-day supply) including high-calorie energy bars.",
                    "Flashlight or headlamp with extra batteries (LED preferred for long life).",
                    "Whistle for signaling rescuers if trapped or injured.",
                    "Dust masks or N95 respirators for airborne contaminants.",
                    "Emergency blanket (mylar) for warmth and protection from elements.",
                    "Personal hygiene items (toilet paper, hand sanitizer, feminine products).",
                    "Important documents in waterproof bag (ID, insurance cards, emergency contacts).",
                    "Cash in small denominations (ATMs may be offline during emergencies).",
                    "Duct tape and plastic sheeting for shelter repairs or contamination barriers.",
                    "Local maps (paper) in case GPS and cell towers fail.",
                    "Copies of house/car keys with trusted neighbor or off-site location.",
                    "Pet supplies if applicable (food, water, carrier, medications, vet records)."
                },
                arGuidance = new[]
                {
                    "Use AR overlays to quickly identify muster points, first aid stations, and accountability zones.",
                    "Reference the interactive help panel for device handling tips during extended training sessions.",
                    "AR indicators highlight emergency equipment locations (fire extinguishers, AEDs, eyewash stations).",
                    "Practice AR-guided evacuation routes from multiple starting points in the facility.",
                    "Review AR scenario replays to identify areas for improvement in your response.",
                    "Share AR training completion certificates with supervisors to document competency.",
                    "Use AR checklists to verify you've covered all preparedness steps before finishing training."
                }
            }
        };

        [Header("Events")]
        [SerializeField]
        private MenuActionEvent onMenuEntryInvoked;

        [Header("Debug")]
        [SerializeField]
        private bool enableDebugLogs = false;

        private UIDocument uiDocument;
        private VisualElement root;
        private VisualElement menuDrawer;
        private VisualElement menuScrim;
        private VisualElement menuActionList;
        private Button menuToggleButton;
    private Button menuHelpButton;
        private Button menuCloseButton;
        private VisualElement exitOverlay;
        private Label exitOverlayTitleLabel;
        private Label exitOverlaySubtitleLabel;
    private VisualElement helpOverlay;
    private Label helpOverlayTitleLabel;
    private Label helpOverlaySubtitleLabel;
    private Button helpOverlayCloseButton;
    private VisualElement learnOverlay;
    private Label learnOverlayTitleLabel;
    private Label learnOverlaySubtitleLabel;
    private ScrollView learnOverlayScroll;
    private VisualElement learnOverlayContent;
    private Button learnOverlayCloseButton;
        private bool isReturning;
        private bool isInitialized;
        private bool isMenuOpen;
    private bool isHelpOverlayVisible;
    private bool isLearnOverlayVisible;
        private IVisualElementScheduledItem exitOverlayHideSchedule;
        private IVisualElementScheduledItem drawerHideSchedule;
        private IVisualElementScheduledItem scrimHideSchedule;
    private IVisualElementScheduledItem helpOverlayHideSchedule;
    private IVisualElementScheduledItem learnOverlayHideSchedule;
        private Coroutine exitRoutine;
        private readonly List<Button> generatedMenuButtons = new List<Button>();
        private readonly List<(LearnEntry entry, VisualElement container)> learnEntryVisuals = new List<(LearnEntry, VisualElement)>();
        private readonly List<KeyCode> backKeys = new List<KeyCode>
        {
            KeyCode.Escape,
            KeyCode.Menu,
            KeyCode.JoystickButton1
        };

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            EnsureDocumentReference();
            RegisterForDocumentReady();
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterTypeChanged;
        }

        private void OnDisable()
        {
            UnregisterDocumentReady();
            DetachEvents();
            HideExitOverlay(immediate: true);
            HideHelpOverlay(immediate: true);
            HideLearnOverlay(immediate: true);
            isReturning = false;
            isInitialized = false;
            if (exitRoutine != null)
            {
                StopCoroutine(exitRoutine);
                exitRoutine = null;
            }
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterTypeChanged;
        }

        private void Update()
        {
            if (!allowSystemBackInput || isReturning)
            {
                return;
            }

            for (int i = 0; i < backKeys.Count; i++)
            {
                if (Input.GetKeyDown(backKeys[i]))
                {
                    if (isMenuOpen)
                    {
                        CloseMenu();
                        break;
                    }

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[SimulationBackButton] Hardware/system back input detected ({backKeys[i]}).", this);
                    }

                    TriggerReturn();
                    break;
                }
            }
        }

        private void EnsureDocumentReference()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }
        }

        private void RegisterForDocumentReady()
        {
            if (uiDocument == null)
            {
                Debug.LogError("[SimulationBackButton] UIDocument missing. Attach this component to a GameObject with a UIDocument.", this);
                return;
            }

            root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[SimulationBackButton] UIDocument has no root visual element.", this);
                return;
            }

            // Ensure our panel is not behind other UI layers
            if (uiDocument != null && uiDocument.panelSettings != null)
            {
                uiDocument.sortingOrder = documentSortingOrder;
            }

            root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

            TryInitializeUI(false);
        }

        private void UnregisterDocumentReady()
        {
            root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            TryInitializeUI(true);
        }

        private void TryInitializeUI(bool logWhenMissing)
        {
            if (isInitialized)
            {
                return;
            }

            if (!InitializeUI(logWhenMissing))
            {
                return;
            }

            isInitialized = true;
            UnregisterDocumentReady();
        }

        private bool InitializeUI(bool logWhenMissing)
        {
            if (uiDocument == null)
            {
                Debug.LogError("[SimulationBackButton] UIDocument missing. Attach this component to a GameObject with a UIDocument.", this);
                return false;
            }

            root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[SimulationBackButton] UIDocument has no root visual element.", this);
                return false;
            }

            AttachStyles(root);

            menuToggleButton = root.Q<Button>("menu-toggle-button");
            // Optional header buttons (may be removed in UXML)
            menuHelpButton = root.Q<Button>("menu-help-button");
            menuCloseButton = root.Q<Button>("menu-close-button");
            menuDrawer = root.Q<VisualElement>("menu-drawer");
            menuScrim = root.Q<VisualElement>("menu-scrim");
            menuActionList = root.Q<VisualElement>("menu-action-list");

            if (menuToggleButton == null)
            {
                if (logWhenMissing || enableDebugLogs)
                {
                    Debug.LogWarning("[SimulationBackButton] Menu toggle element not found in UXML yet (expected name 'menu-toggle-button').", this);
                }
                return false;
            }

            CacheExitOverlayElements();
            CacheHelpOverlayElements();
            CacheLearnOverlayElements();
            AttachMenuEvents();
            EnsureDefaultMenuEntries();
            BuildMenuActionList();
            CloseMenu(true);

            HideExitOverlay(immediate: true);
            HideHelpOverlay(immediate: true);
            HideLearnOverlay(immediate: true);

            // Keep the hamburger above other elements in this document
            menuToggleButton?.BringToFront();

            if (enableDebugLogs)
            {
                Debug.Log($"[SimulationBackButton] Initialized. Target menu scene: {menuSceneName}.", this);
            }

            return true;
        }

        private void DetachEvents()
        {
            if (menuToggleButton != null)
            {
                menuToggleButton.clicked -= OnMenuToggleClicked;
                menuToggleButton = null;
            }

            if (menuHelpButton != null)
            {
                menuHelpButton.clicked -= OnMenuHelpClicked;
                menuHelpButton = null;
            }

            if (menuCloseButton != null)
            {
                menuCloseButton.clicked -= OnMenuCloseClicked;
                menuCloseButton = null;
            }

            if (menuScrim != null)
            {
                menuScrim.UnregisterCallback<ClickEvent>(OnScrimClicked);
                menuScrim = null;
            }

            if (helpOverlayCloseButton != null)
            {
                helpOverlayCloseButton.clicked -= OnHelpOverlayCloseClicked;
                helpOverlayCloseButton = null;
            }

            helpOverlayHideSchedule?.Pause();
            helpOverlayHideSchedule = null;

            if (learnOverlayCloseButton != null)
            {
                learnOverlayCloseButton.clicked -= OnLearnOverlayCloseClicked;
                learnOverlayCloseButton = null;
            }

            learnOverlayHideSchedule?.Pause();
            learnOverlayHideSchedule = null;

            generatedMenuButtons.Clear();
        }

        private void CacheExitOverlayElements()
        {
            if (root == null)
            {
                return;
            }

            exitOverlay = root.Q<VisualElement>("exit-overlay");
            exitOverlayTitleLabel = root.Q<Label>("exit-overlay-title");
            exitOverlaySubtitleLabel = root.Q<Label>("exit-overlay-subtitle");

            if (exitOverlayTitleLabel != null && !string.IsNullOrEmpty(exitOverlayTitle))
            {
                exitOverlayTitleLabel.text = exitOverlayTitle;
            }

            if (exitOverlaySubtitleLabel != null && !string.IsNullOrEmpty(exitOverlaySubtitle))
            {
                exitOverlaySubtitleLabel.text = exitOverlaySubtitle;
            }

            if (exitOverlay != null)
            {
                exitOverlay.style.display = DisplayStyle.None;
                exitOverlay.style.visibility = Visibility.Hidden;
                exitOverlay.style.opacity = 0f;
            }
        }

        private void CacheHelpOverlayElements()
        {
            if (root == null)
            {
                return;
            }

            helpOverlay = root.Q<VisualElement>("help-overlay");
            helpOverlayTitleLabel = root.Q<Label>("help-overlay-title");
            helpOverlaySubtitleLabel = root.Q<Label>("help-overlay-subtitle");
            helpOverlayCloseButton = root.Q<Button>("help-overlay-close-button");

            // The title and subtitle are already set in UXML, so we don't override them here
            // Unless you want dynamic text, keep the UXML values

            if (helpOverlay != null)
            {
                helpOverlay.style.display = DisplayStyle.None;
                helpOverlay.style.opacity = 0f;
                helpOverlay.style.visibility = Visibility.Hidden;
                helpOverlay.RemoveFromClassList("help-overlay--visible");
                helpOverlay.pickingMode = PickingMode.Ignore;
            }

            isHelpOverlayVisible = false;
        }

        private void CacheLearnOverlayElements()
        {
            if (root == null)
            {
                return;
            }

            learnOverlay = root.Q<VisualElement>("learn-overlay");
            learnOverlayTitleLabel = root.Q<Label>("learn-overlay-title");
            learnOverlaySubtitleLabel = root.Q<Label>("learn-overlay-subtitle");
            learnOverlayScroll = root.Q<ScrollView>("learn-overlay-scroll");
            learnOverlayContent = root.Q<VisualElement>("learn-overlay-content");
            learnOverlayCloseButton = root.Q<Button>("learn-overlay-close-button");

            if (learnOverlayTitleLabel != null && !string.IsNullOrEmpty(learnOverlayTitle))
            {
                learnOverlayTitleLabel.text = learnOverlayTitle;
            }

            if (learnOverlaySubtitleLabel != null && !string.IsNullOrEmpty(learnOverlaySubtitle))
            {
                learnOverlaySubtitleLabel.text = learnOverlaySubtitle;
            }

            if (learnOverlay != null)
            {
                learnOverlay.style.display = DisplayStyle.None;
                learnOverlay.style.opacity = 0f;
                learnOverlay.style.visibility = Visibility.Hidden;
                learnOverlay.RemoveFromClassList("learn-overlay--visible");
                learnOverlay.pickingMode = PickingMode.Ignore;
            }

            isLearnOverlayVisible = false;
            BuildLearnOverlayContent();
        }

        private void BuildLearnOverlayContent()
        {
            if (learnOverlayContent == null)
            {
                return;
            }

            learnOverlayContent.Clear();
            learnEntryVisuals.Clear();

            DisasterType currentType = DisasterTypeManager.SelectedDisasterType;
            List<LearnEntry> entriesToRender = new List<LearnEntry>();

            if (learnEntries != null)
            {
                for (int i = 0; i < learnEntries.Count; i++)
                {
                    LearnEntry entry = learnEntries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    bool matchesSelection = currentType != DisasterType.None && entry.disasterType == currentType;
                    bool isGeneralFallback = currentType == DisasterType.None && entry.disasterType == DisasterType.GeneralSafety;

                    if (matchesSelection || isGeneralFallback)
                    {
                        entriesToRender.Add(entry);
                    }
                }
            }

            if (learnOverlayTitleLabel != null)
            {
                string baseTitle = string.IsNullOrEmpty(learnOverlayTitle) ? "Learn the Scenario" : learnOverlayTitle;
                if (entriesToRender.Count == 1)
                {
                    string scenarioName = string.IsNullOrEmpty(entriesToRender[0].displayName)
                        ? entriesToRender[0].disasterType.ToString()
                        : entriesToRender[0].displayName;
                    learnOverlayTitleLabel.text = $"{baseTitle}: {scenarioName}";
                }
                else
                {
                    learnOverlayTitleLabel.text = baseTitle;
                }
            }

            bool useSummaryAsSubtitle = entriesToRender.Count == 1 && !string.IsNullOrEmpty(entriesToRender[0].summary);

            if (learnOverlaySubtitleLabel != null)
            {
                learnOverlaySubtitleLabel.text = useSummaryAsSubtitle
                    ? entriesToRender[0].summary
                    : string.IsNullOrEmpty(learnOverlaySubtitle)
                        ? "Review key actions for each disaster before you continue."
                        : learnOverlaySubtitle;
            }

            if (entriesToRender.Count == 0)
            {
                string message = currentType == DisasterType.None
                    ? "Select a disaster scenario to view learn guidance."
                    : $"Learn content for {currentType} is not configured yet.";

                Label placeholder = new Label(message)
                {
                    pickingMode = PickingMode.Ignore
                };
                placeholder.AddToClassList("learn-entry__summary");
                learnOverlayContent.Add(placeholder);
                return;
            }

            for (int i = 0; i < entriesToRender.Count; i++)
            {
                LearnEntry entry = entriesToRender[i];
                if (entry == null)
                {
                    continue;
                }

                VisualElement entryRoot = new VisualElement();
                entryRoot.AddToClassList("learn-entry");

                string titleText = !string.IsNullOrEmpty(entry.displayName)
                    ? entry.displayName
                    : entry.disasterType.ToString();

                Label titleLabel = new Label(titleText)
                {
                    pickingMode = PickingMode.Ignore
                };
                titleLabel.AddToClassList("learn-entry__label");
                entryRoot.Add(titleLabel);

                if (!string.IsNullOrEmpty(entry.summary) && !useSummaryAsSubtitle)
                {
                    Label summaryLabel = new Label(entry.summary)
                    {
                        pickingMode = PickingMode.Ignore
                    };
                    summaryLabel.AddToClassList("learn-entry__summary");
                    entryRoot.Add(summaryLabel);
                }

                AddCollapsibleLearnSection(entryRoot, "Before the Disaster", ">>", entry.beforeActions, true);
                AddCollapsibleLearnSection(entryRoot, "During the Disaster", "!", entry.duringActions, true);
                AddCollapsibleLearnSection(entryRoot, "After the Disaster", "v", entry.afterActions, false);
                AddCollapsibleLearnSection(entryRoot, "What to Bring", "+", entry.supplies, false);
                AddCollapsibleLearnSection(entryRoot, "AR Simulation Tips", "?", entry.arGuidance, false);

                learnOverlayContent.Add(entryRoot);
                learnEntryVisuals.Add((entry, entryRoot));
            }
        }

        private void AddCollapsibleLearnSection(VisualElement container, string title, string icon, string[] items, bool startExpanded)
        {
            if (container == null || items == null || items.Length == 0)
            {
                return;
            }

            List<string> validItems = new List<string>();
            for (int i = 0; i < items.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(items[i]))
                {
                    validItems.Add(items[i].Trim());
                }
            }

            if (validItems.Count == 0)
            {
                return;
            }

            VisualElement sectionRoot = new VisualElement();
            sectionRoot.AddToClassList("learn-section");
            if (startExpanded)
            {
                sectionRoot.AddToClassList("learn-section--expanded");
            }

            VisualElement header = new VisualElement();
            header.AddToClassList("learn-section__header");
            if (startExpanded)
            {
                header.AddToClassList("learn-section__header--expanded");
            }

            VisualElement titleContainer = new VisualElement();
            titleContainer.AddToClassList("learn-section__title-container");

            Label iconLabel = new Label(icon);
            iconLabel.AddToClassList("learn-section__icon");
            iconLabel.pickingMode = PickingMode.Ignore;

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("learn-section__title");
            titleLabel.pickingMode = PickingMode.Ignore;

            titleContainer.Add(iconLabel);
            titleContainer.Add(titleLabel);

            Label chevron = new Label(">");
            chevron.AddToClassList("learn-section__chevron");
            if (startExpanded)
            {
                chevron.AddToClassList("learn-section__chevron--expanded");
            }
            chevron.pickingMode = PickingMode.Ignore;

            header.Add(titleContainer);
            header.Add(chevron);

            VisualElement content = new VisualElement();
            content.AddToClassList("learn-section__content");
            if (startExpanded)
            {
                content.AddToClassList("learn-section__content--expanded");
            }

            for (int i = 0; i < validItems.Count; i++)
            {
                VisualElement itemRow = new VisualElement();
                itemRow.AddToClassList("learn-section__item");
                itemRow.pickingMode = PickingMode.Ignore;

                Label bulletLabel = new Label("-");
                bulletLabel.AddToClassList("learn-section__bullet");
                bulletLabel.pickingMode = PickingMode.Ignore;

                Label textLabel = new Label(validItems[i]);
                textLabel.AddToClassList("learn-section__text");
                textLabel.pickingMode = PickingMode.Ignore;

                itemRow.Add(bulletLabel);
                itemRow.Add(textLabel);
                content.Add(itemRow);
            }

            sectionRoot.Add(header);
            sectionRoot.Add(content);

            header.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                bool isExpanded = content.ClassListContains("learn-section__content--expanded");

                if (isExpanded)
                {
                    content.RemoveFromClassList("learn-section__content--expanded");
                    header.RemoveFromClassList("learn-section__header--expanded");
                    sectionRoot.RemoveFromClassList("learn-section--expanded");
                    chevron.RemoveFromClassList("learn-section__chevron--expanded");
                }
                else
                {
                    content.AddToClassList("learn-section__content--expanded");
                    header.AddToClassList("learn-section__header--expanded");
                    sectionRoot.AddToClassList("learn-section--expanded");
                    chevron.AddToClassList("learn-section__chevron--expanded");
                }
            });

            container.Add(sectionRoot);
        }

        private void RefreshLearnOverlayHighlight()
        {
            if (learnEntryVisuals == null || learnEntryVisuals.Count == 0)
            {
                return;
            }

            DisasterType currentType = DisasterTypeManager.SelectedDisasterType;

            for (int i = 0; i < learnEntryVisuals.Count; i++)
            {
                (LearnEntry entry, VisualElement container) tuple = learnEntryVisuals[i];
                if (tuple.container == null)
                {
                    continue;
                }

                if (tuple.entry != null && tuple.entry.disasterType == currentType && currentType != DisasterType.None)
                {
                    tuple.container.AddToClassList("learn-entry--active");
                }
                else
                {
                    tuple.container.RemoveFromClassList("learn-entry--active");
                }
            }
        }

        private void ShowExitOverlay()
        {
            if (exitOverlay != null)
            {
                exitOverlayHideSchedule?.Pause();
                exitOverlayHideSchedule = null;

                exitOverlay.style.display = DisplayStyle.Flex;
                exitOverlay.style.visibility = Visibility.Visible;
                exitOverlay.style.opacity = 0f;
                exitOverlay.BringToFront();

                exitOverlay.schedule.Execute(() => exitOverlay.style.opacity = 1f);
            }

            if (exitOverlayTitleLabel != null)
            {
                exitOverlayTitleLabel.text = string.IsNullOrEmpty(exitOverlayTitle) ? "Exiting Simulation" : exitOverlayTitle;
            }

            if (exitOverlaySubtitleLabel != null)
            {
                exitOverlaySubtitleLabel.text = string.IsNullOrEmpty(exitOverlaySubtitle) ? "Returning to Main Menu..." : exitOverlaySubtitle;
            }
        }

        private void HideExitOverlay(bool immediate = false)
        {
            if (exitOverlay != null)
            {
                exitOverlayHideSchedule?.Pause();
                exitOverlayHideSchedule = null;
                exitOverlay.style.opacity = 0f;

                if (immediate)
                {
                    exitOverlay.style.visibility = Visibility.Hidden;
                    exitOverlay.style.display = DisplayStyle.None;
                    return;
                }

                long delayMs = Mathf.RoundToInt(Mathf.Max(0.05f, exitOverlayFadeDuration) * 1000f);
                exitOverlayHideSchedule = exitOverlay.schedule.Execute(() =>
                {
                    exitOverlay.style.display = DisplayStyle.None;
                    exitOverlay.style.visibility = Visibility.Hidden;
                    exitOverlayHideSchedule = null;
                });
                exitOverlayHideSchedule.ExecuteLater(delayMs);
            }
        }

        private void ShowHelpOverlay()
        {
            if (helpOverlay == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning("[SimulationBackButton] Help overlay element not found in UXML.", this);
                }
                return;
            }

            if (isHelpOverlayVisible)
            {
                return;
            }

            helpOverlayHideSchedule?.Pause();
            helpOverlayHideSchedule = null;

            helpOverlay.style.display = DisplayStyle.Flex;
            helpOverlay.style.visibility = Visibility.Visible;
            helpOverlay.style.opacity = 1f;
            helpOverlay.AddToClassList("help-overlay--visible");
            helpOverlay.pickingMode = PickingMode.Position;
            helpOverlay.BringToFront();

            SetMenuInteractivity(false);
            isHelpOverlayVisible = true;

            if (enableDebugLogs)
            {
                Debug.Log("[SimulationBackButton] Help overlay shown.", this);
            }
        }

        private void HideHelpOverlay(bool immediate = false)
        {
            if (helpOverlay == null)
            {
                return;
            }

            if (!isHelpOverlayVisible && !immediate)
            {
                return;
            }

            isHelpOverlayVisible = false;

            helpOverlayHideSchedule?.Pause();
            helpOverlayHideSchedule = null;

            helpOverlay.RemoveFromClassList("help-overlay--visible");
            helpOverlay.style.opacity = 0f;

            if (immediate)
            {
                helpOverlay.style.display = DisplayStyle.None;
                helpOverlay.style.visibility = Visibility.Hidden;
                helpOverlay.pickingMode = PickingMode.Ignore;
                SetMenuInteractivity(true);
                return;
            }

            const int FadeDurationMs = 250;
            helpOverlayHideSchedule = helpOverlay.schedule.Execute(() =>
            {
                helpOverlay.style.display = DisplayStyle.None;
                helpOverlay.style.visibility = Visibility.Hidden;
                helpOverlay.pickingMode = PickingMode.Ignore;
                helpOverlayHideSchedule = null;
                SetMenuInteractivity(true);
            });
            helpOverlayHideSchedule.ExecuteLater(FadeDurationMs);
        }

        private void ShowLearnOverlay()
        {
            if (learnOverlay == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning("[SimulationBackButton] Learn overlay element not found in UXML.", this);
                }
                return;
            }

            BuildLearnOverlayContent();
            RefreshLearnOverlayHighlight();

            if (learnOverlayScroll != null && learnEntryVisuals.Count > 0)
            {
                VisualElement targetElement = null;
                DisasterType currentType = DisasterTypeManager.SelectedDisasterType;
                if (currentType != DisasterType.None)
                {
                    for (int i = 0; i < learnEntryVisuals.Count; i++)
                    {
                        if (learnEntryVisuals[i].entry != null && learnEntryVisuals[i].entry.disasterType == currentType)
                        {
                            targetElement = learnEntryVisuals[i].container;
                            break;
                        }
                    }
                }

                if (targetElement == null)
                {
                    targetElement = learnEntryVisuals[0].container;
                }

                if (targetElement != null)
                {
                    learnOverlayScroll.ScrollTo(targetElement);
                }
            }

            if (isLearnOverlayVisible)
            {
                return;
            }

            learnOverlayHideSchedule?.Pause();
            learnOverlayHideSchedule = null;

            learnOverlay.style.display = DisplayStyle.Flex;
            learnOverlay.style.visibility = Visibility.Visible;
            learnOverlay.style.opacity = 1f;
            learnOverlay.AddToClassList("learn-overlay--visible");
            learnOverlay.pickingMode = PickingMode.Position;
            learnOverlay.BringToFront();

            SetMenuInteractivity(false);
            isLearnOverlayVisible = true;

            if (enableDebugLogs)
            {
                Debug.Log("[SimulationBackButton] Learn overlay shown.", this);
            }
        }

        private void HideLearnOverlay(bool immediate = false)
        {
            if (learnOverlay == null)
            {
                return;
            }

            if (!isLearnOverlayVisible && !immediate)
            {
                return;
            }

            isLearnOverlayVisible = false;

            learnOverlayHideSchedule?.Pause();
            learnOverlayHideSchedule = null;

            learnOverlay.RemoveFromClassList("learn-overlay--visible");
            learnOverlay.style.opacity = 0f;

            if (immediate)
            {
                learnOverlay.style.display = DisplayStyle.None;
                learnOverlay.style.visibility = Visibility.Hidden;
                learnOverlay.pickingMode = PickingMode.Ignore;
                SetMenuInteractivity(true);
                return;
            }

            const int FadeDurationMs = 250;
            learnOverlayHideSchedule = learnOverlay.schedule.Execute(() =>
            {
                learnOverlay.style.display = DisplayStyle.None;
                learnOverlay.style.visibility = Visibility.Hidden;
                learnOverlay.pickingMode = PickingMode.Ignore;
                learnOverlayHideSchedule = null;
                SetMenuInteractivity(true);
            });
            learnOverlayHideSchedule.ExecuteLater(FadeDurationMs);
        }

        /// <summary>
        /// Trigger relocalization - Shows UI panel with recent anchors and manual selection
        /// </summary>
        private void HandleRelocalize()
        {
            var activationController = FindFirstObjectByType<ARSafeActivationController>();
            if (activationController != null)
            {
                activationController.RequestRelocalization();

                if (enableDebugLogs)
                {
                    Debug.Log("[SimulationBackButton] Relocalization UI requested.", this);
                }
            }
            else
            {
                Debug.LogWarning("[SimulationBackButton] ARSafeActivationController not found - cannot relocalize!", this);
            }
        }

        private void SetMenuInteractivity(bool enabled)
        {
            if (menuToggleButton == null)
            {
                return;
            }

            if (!enabled)
            {
                menuToggleButton.SetEnabled(false);
                return;
            }

            if (!isReturning && !isHelpOverlayVisible && !isLearnOverlayVisible)
            {
                menuToggleButton.SetEnabled(true);
            }
        }

        private void OnLearnOverlayCloseClicked()
        {
            HideLearnOverlay();
        }

        private void HandleDisasterTypeChanged(DisasterType disasterType)
        {
            BuildLearnOverlayContent();
            RefreshLearnOverlayHighlight();
        }

        private void AttachStyles(VisualElement target)
        {
            if (target == null)
            {
                return;
            }

            StyleSheet sheetToApply = backButtonStyles;
            if (sheetToApply == null)
            {
                sheetToApply = Resources.Load<StyleSheet>("UI/SimulationControls/SimulationBackButtonStyles");
            }

            if (sheetToApply == null)
            {
                Debug.LogWarning("[SimulationBackButton] StyleSheet not found. Assign one in the inspector or place SimulationBackButtonStyles.uss under Resources/UI/SimulationControls/.", this);
                return;
            }

            if (!target.styleSheets.Contains(sheetToApply))
            {
                target.styleSheets.Add(sheetToApply);
            }
        }

        private void OnMenuToggleClicked()
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SimulationBackButton] Menu toggle pressed. MenuOpen={isMenuOpen}.", this);
            }

            if (isMenuOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }

        private void OnMenuCloseClicked()
        {
            CloseMenu();
        }

        private void OnMenuHelpClicked()
        {
            ShowHelpOverlay();
            InvokeMenuEvent(HelpActionId);
        }

        private void OnScrimClicked(ClickEvent evt)
        {
            evt.StopPropagation();
            CloseMenu();
        }

        private void OnHelpOverlayCloseClicked()
        {
            HideHelpOverlay();
        }

        private void TriggerReturn()
        {
            if (isReturning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(menuSceneName))
            {
                Debug.LogWarning("[SimulationBackButton] Menu scene name is empty. Please assign a valid scene.", this);
                return;
            }

            isReturning = true;
            CloseMenu(true);
            ShowExitOverlay();
            menuToggleButton?.SetEnabled(false);

            if (enableDebugLogs)
            {
                Debug.Log($"[SimulationBackButton] Returning to menu scene '{menuSceneName}'.", this);
            }

            if (exitRoutine != null)
            {
                StopCoroutine(exitRoutine);
            }

            exitRoutine = StartCoroutine(ExecuteReturnAfterDelay());
        }

        private IEnumerator ExecuteReturnAfterDelay()
        {
            float waitDuration = Mathf.Max(0f, exitDelaySeconds);
            if (waitDuration > 0f)
            {
                yield return new WaitForSeconds(waitDuration);
            }

            // CRITICAL: Clean up navigation and warning systems BEFORE scene unload
            CleanupNavigationAndWarningSystems();

            if (DisasterTypeManager.SelectedDisasterType != DisasterType.None)
            {
                // Clear the active disaster so the next menu selection re-triggers setup.
                DisasterTypeManager.SetDisasterType(DisasterType.None);
            }

            if (useSceneLoaderManager)
            {
                bl_SceneLoaderManager.LoadScene(menuSceneName);
            }
            else
            {
                SceneManager.LoadScene(menuSceneName);
            }

            exitRoutine = null;
        }

        /// <summary>
        /// Clean up navigation and wrong-way warning systems when exiting simulation.
        /// CRITICAL: Call this BEFORE scene unload to properly reset all systems.
        /// Uses try-catch protection to prevent crashes during Unity shutdown.
        /// </summary>
        private void CleanupNavigationAndWarningSystems()
        {
            if (enableDebugLogs)
            {
                Debug.Log("<color=cyan>[SimulationBackButton]</color> Cleaning up navigation and warning systems...");
            }

            // Wrap entire cleanup in try-catch to prevent crashes during scene unload
            try
            {
                // 1. Clean up wrong-way warning system
                try
                {
                    var wrongWayWarning = FindFirstObjectByType<ARSafe.UI.ARSafeWrongWayWarning>();
                    if (wrongWayWarning != null && wrongWayWarning.gameObject != null)
                    {
                        wrongWayWarning.DisableWarnings();

                        if (enableDebugLogs)
                        {
                            Debug.Log("  ✓ ARSafeWrongWayWarning disabled");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SimulationBackButton] Could not clean up wrong-way warning: {ex.Message}");
                }

                // 2. Clean up navigation validator (FULL reset including virtual exits)
                try
                {
                    var navigationValidator = FindFirstObjectByType<ARSafe.Modular.ARSafeNavigationValidator>();
                    if (navigationValidator != null && navigationValidator.gameObject != null)
                    {
                        navigationValidator.ResetForNewSimulation();

                        if (enableDebugLogs)
                        {
                            Debug.Log("  ✓ ARSafeNavigationValidator fully reset");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SimulationBackButton] Could not reset navigation validator: {ex.Message}");
                }

                // 3. Reset all virtual exit markers
                try
                {
                    var virtualExits = FindObjectsByType<ARSafe.Content.VirtualExitMarker>(FindObjectsSortMode.None);
                    if (virtualExits != null && virtualExits.Length > 0)
                    {
                        int resetCount = 0;
                        foreach (var virtualExit in virtualExits)
                        {
                            if (virtualExit != null && virtualExit.gameObject != null)
                            {
                                try
                                {
                                    virtualExit.ResetExit();
                                    resetCount++;
                                }
                                catch
                                {
                                    // Skip this virtual exit if it's already being destroyed
                                }
                            }
                        }

                        if (enableDebugLogs && resetCount > 0)
                        {
                            Debug.Log($"  ✓ {resetCount} virtual exit marker(s) reset");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SimulationBackButton] Could not reset virtual exits: {ex.Message}");
                }

                if (enableDebugLogs)
                {
                    Debug.Log("<color=green>[SimulationBackButton]</color> Navigation and warning cleanup complete!");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SimulationBackButton] Critical error during cleanup (non-fatal): {ex.Message}");
            }
        }
        private void AttachMenuEvents()
        {
            if (menuToggleButton != null)
            {
                menuToggleButton.clicked -= OnMenuToggleClicked;
                menuToggleButton.clicked += OnMenuToggleClicked;
            }

            if (menuHelpButton != null)
            {
                menuHelpButton.clicked -= OnMenuHelpClicked;
                menuHelpButton.clicked += OnMenuHelpClicked;
            }

            if (menuCloseButton != null)
            {
                menuCloseButton.clicked -= OnMenuCloseClicked;
                menuCloseButton.clicked += OnMenuCloseClicked;
            }

            if (menuScrim != null)
            {
                menuScrim.style.display = DisplayStyle.None;
                menuScrim.pickingMode = PickingMode.Ignore;
                menuScrim.UnregisterCallback<ClickEvent>(OnScrimClicked);
                menuScrim.RegisterCallback<ClickEvent>(OnScrimClicked);
            }

            if (helpOverlayCloseButton != null)
            {
                helpOverlayCloseButton.clicked -= OnHelpOverlayCloseClicked;
                helpOverlayCloseButton.clicked += OnHelpOverlayCloseClicked;
            }

            if (learnOverlayCloseButton != null)
            {
                learnOverlayCloseButton.clicked -= OnLearnOverlayCloseClicked;
                learnOverlayCloseButton.clicked += OnLearnOverlayCloseClicked;
            }
        }

        private void EnsureDefaultMenuEntries()
        {
            if (menuEntries == null)
            {
                menuEntries = new List<MenuEntryConfig>();
            }

            bool hasBack = false;
            bool hasLearn = false;
            bool hasHelp = false;

            for (int i = 0; i < menuEntries.Count; i++)
            {
                var entry = menuEntries[i];
                if (entry == null)
                {
                    continue;
                }

                switch (entry.action)
                {
                    case MenuEntryAction.BackToMenu:
                        hasBack = true;
                        break;
                    case MenuEntryAction.Learn:
                        hasLearn = true;
                        break;
                    case MenuEntryAction.Help:
                        hasHelp = true;
                        break;
                }
            }

            if (!hasBack)
            {
                menuEntries.Insert(0, new MenuEntryConfig
                {
                    id = "back",
                    label = "Back to Menu",
                    iconText = "←",
                    action = MenuEntryAction.BackToMenu,
                    enabled = true
                });
            }

            if (!hasLearn)
            {
                menuEntries.Add(new MenuEntryConfig
                {
                    id = "learn",
                    label = "Learn This Scenario",
                    iconText = "•",
                    action = MenuEntryAction.Learn,
                    enabled = true
                });
            }

            if (!hasHelp)
            {
                menuEntries.Add(new MenuEntryConfig
                {
                    id = "help",
                    label = "Help & Tips",
                    iconText = "?",
                    action = MenuEntryAction.Help,
                    enabled = true
                });
            }
        }

        private void BuildMenuActionList()
        {
            if (menuActionList == null)
            {
                return;
            }

            menuActionList.Clear();
            generatedMenuButtons.Clear();

            bool hasRenderableEntry = false;
            if (menuEntries == null || menuEntries.Count == 0)
            {
                menuEntries = new List<MenuEntryConfig>
                {
                    new MenuEntryConfig
                    {
                        id = "back",
                        label = "Back to Menu",
                        iconText = "←",
                        action = MenuEntryAction.BackToMenu,
                        enabled = true
                    }
                };
            }

            for (int i = 0; i < menuEntries.Count; i++)
            {
                MenuEntryConfig entry = menuEntries[i];
                if (entry == null || !entry.enabled)
                {
                    continue;
                }

                hasRenderableEntry = true;
                Button entryButton = CreateMenuButton(entry);
                menuActionList.Add(entryButton);
                generatedMenuButtons.Add(entryButton);
            }

            if (!hasRenderableEntry)
            {
                Label placeholder = new Label("No menu items configured.");
                placeholder.AddToClassList("menu-placeholder");
                menuActionList.Add(placeholder);
            }
        }

        private Button CreateMenuButton(MenuEntryConfig entry)
        {
            Button button = new Button
            {
                name = $"menu-action-{entry.id}",
                text = string.Empty
            };
            button.AddToClassList("menu-action-button");

            // Use sprite icon if available, otherwise use text icon
            if (entry.iconSprite != null)
            {
                VisualElement iconElement = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                iconElement.AddToClassList("menu-action-button__icon");
                iconElement.AddToClassList("menu-action-button__icon--sprite");
                iconElement.style.backgroundImage = new StyleBackground(entry.iconSprite);
                button.Add(iconElement);
            }
            else
            {
                Label icon = new Label(string.IsNullOrEmpty(entry.iconText) ? "•" : entry.iconText)
                {
                    pickingMode = PickingMode.Ignore
                };
                icon.AddToClassList("menu-action-button__icon");
                icon.AddToClassList("menu-action-button__icon--text");
                button.Add(icon);
            }

            Label label = new Label(string.IsNullOrEmpty(entry.label) ? entry.id : entry.label)
            {
                pickingMode = PickingMode.Ignore
            };
            label.AddToClassList("menu-action-button__label");
            button.Add(label);

            button.clicked += () => HandleMenuEntryInvoked(entry);
            return button;
        }

        private void HandleMenuEntryInvoked(MenuEntryConfig entry)
        {
            if (entry == null)
            {
                return;
            }

            bool keepMenuOpen = entry.action == MenuEntryAction.Help || entry.action == MenuEntryAction.Learn;

            if (!keepMenuOpen)
            {
                CloseMenu();
            }

            switch (entry.action)
            {
                case MenuEntryAction.BackToMenu:
                    TriggerReturn();
                    break;
                case MenuEntryAction.Help:
                    ShowHelpOverlay();
                    InvokeMenuEvent(string.IsNullOrEmpty(entry.id) ? HelpActionId : entry.id);
                    if (enableDebugLogs)
                    {
                        Debug.Log("[SimulationBackButton] Help action invoked. Hook onMenuEntryInvoked for custom behavior.", this);
                    }
                    break;
                case MenuEntryAction.Learn:
                    ShowLearnOverlay();
                    InvokeMenuEvent(string.IsNullOrEmpty(entry.id) ? LearnActionId : entry.id);
                    if (enableDebugLogs)
                    {
                        Debug.Log("[SimulationBackButton] Learn action invoked.", this);
                    }
                    break;
                case MenuEntryAction.Relocalize:
                    HandleRelocalize();
                    InvokeMenuEvent(string.IsNullOrEmpty(entry.id) ? "relocalize" : entry.id);
                    if (enableDebugLogs)
                    {
                        Debug.Log("[SimulationBackButton] Relocalize action invoked.", this);
                    }
                    break;
                default:
                    InvokeMenuEvent(entry.id);
                    break;
            }
        }

        private void InvokeMenuEvent(string actionId)
        {
            if (onMenuEntryInvoked != null)
            {
                onMenuEntryInvoked.Invoke(actionId);
            }
        }

        private void OpenMenu()
        {
            if (isMenuOpen)
            {
                return;
            }

            isMenuOpen = true;

            if (drawerHideSchedule != null)
            {
                drawerHideSchedule.Pause();
                drawerHideSchedule = null;
            }
            if (scrimHideSchedule != null)
            {
                scrimHideSchedule.Pause();
                scrimHideSchedule = null;
            }

            if (menuDrawer != null)
            {
                menuDrawer.style.display = DisplayStyle.Flex;
                menuDrawer.AddToClassList("menu-drawer--open");
                menuDrawer.BringToFront();
            }

            if (menuScrim != null)
            {
                menuScrim.style.display = DisplayStyle.Flex;
                menuScrim.AddToClassList("menu-scrim--visible");
                menuScrim.pickingMode = PickingMode.Position;
            }

            if (menuToggleButton != null)
            {
                menuToggleButton.AddToClassList("menu-toggle-button--active");
            }
        }

        /// <summary>
        /// Public method to close the menu (can be called from other scripts)
        /// </summary>
        public void CloseMenu() => CloseMenu(false);

        private void CloseMenu(bool immediate = false)
        {
            if (!isMenuOpen && !immediate)
            {
                return;
            }

            isMenuOpen = false;

            HideHelpOverlay(immediate: true);
            HideLearnOverlay(immediate: true);

            if (drawerHideSchedule != null)
            {
                drawerHideSchedule.Pause();
                drawerHideSchedule = null;
            }
            if (scrimHideSchedule != null)
            {
                scrimHideSchedule.Pause();
                scrimHideSchedule = null;
            }

            if (menuToggleButton != null)
            {
                menuToggleButton.RemoveFromClassList("menu-toggle-button--active");
            }

            if (menuDrawer != null)
            {
                menuDrawer.RemoveFromClassList("menu-drawer--open");

                if (immediate)
                {
                    menuDrawer.style.display = DisplayStyle.None;
                }
                else
                {
                    long delayMs = Mathf.RoundToInt(Mathf.Max(0.05f, menuAnimationDuration) * 1000f);
                    drawerHideSchedule = menuDrawer.schedule.Execute(() =>
                    {
                        menuDrawer.style.display = DisplayStyle.None;
                        drawerHideSchedule = null;
                    });
                    drawerHideSchedule.ExecuteLater(delayMs);
                }
            }

            if (menuScrim != null)
            {
                menuScrim.RemoveFromClassList("menu-scrim--visible");

                if (immediate)
                {
                    menuScrim.style.display = DisplayStyle.None;
                    menuScrim.pickingMode = PickingMode.Ignore;
                }
                else
                {
                    long delayMs = Mathf.RoundToInt(Mathf.Max(0.05f, menuAnimationDuration) * 1000f);
                    scrimHideSchedule = menuScrim.schedule.Execute(() =>
                    {
                        menuScrim.style.display = DisplayStyle.None;
                        menuScrim.pickingMode = PickingMode.Ignore;
                        scrimHideSchedule = null;
                    });
                    scrimHideSchedule.ExecuteLater(delayMs);
                }
            }
        }
    }
}
