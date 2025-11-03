using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Lovatto.SceneLoader;

namespace ARSafe.UI
{
    [Serializable]
    public class ExitScenarioContent
    {
        public DisasterType disasterType = DisasterType.None;
        [Tooltip("Headline that appears beneath the evacuation title")] public string scenarioLabel = "General Guidance";
        [TextArea] public string introText = "You have reached the designated evacuation zone.";
        [TextArea] public string footerText = "Await further instructions from safety personnel.";
        [Tooltip("Individual action steps shown in the checklist section")]
        public List<string> steps = new List<string>();
        [Tooltip("Primary CTA label")] public string proceedButtonText = "Proceed to Safe Zone";
        [Tooltip("Secondary CTA label")] public string detailsButtonText = "Review Disaster Checklist";
    }

    public class ExitOverlayController : MonoBehaviour
    {
        public static ExitOverlayController Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private UIDocument overlayDocument;
        [SerializeField] private StyleSheet overlayStylesheet;

        [Header("Scenario Content")]
        [SerializeField] private ExitScenarioContent defaultContent = new ExitScenarioContent();
        [SerializeField] private List<ExitScenarioContent> scenarioContent = new List<ExitScenarioContent>();

        [Header("Button Events")]
        [SerializeField] private UnityEvent onProceedToSafeZone;
        [SerializeField] private UnityEvent onReviewChecklist;

        [Header("Return to Menu Settings")]
        [SerializeField] private string menuSceneName = "MainMenu";
        [SerializeField] private bool useSceneLoaderManager = true;
        [SerializeField] private float congratulationsDisplayTime = 3f;
        [SerializeField] private string congratulationsMessage = "Congratulations on completing the simulation!";

        private readonly Dictionary<DisasterType, ExitScenarioContent> scenarioLookup = new Dictionary<DisasterType, ExitScenarioContent>();

        private VisualElement overlayRoot;
        private Label titleLabel;
        private Label scenarioLabel;
        private Label introLabel;
        private VisualElement stepsContainer;
        private Label footerLabel;
        private Label congratsLabel;
        private Button closeButton;
        private Button detailsButton;

        private bool initialized;
        private Coroutine returnToMenuCoroutine;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            CacheDocument();
            BuildScenarioLookup();
        }

        void OnEnable()
        {
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterTypeChanged;

            // Subscribe to virtual exit events
            var navigationValidator = FindFirstObjectByType<ARSafe.Modular.ARSafeNavigationValidator>();
            if (navigationValidator != null)
            {
                navigationValidator.OnVirtualExitReached += HandleVirtualExitReached;
            }
        }

        void OnDisable()
        {
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterTypeChanged;

            // Unsubscribe from virtual exit events
            var navigationValidator = FindFirstObjectByType<ARSafe.Modular.ARSafeNavigationValidator>();
            if (navigationValidator != null)
            {
                navigationValidator.OnVirtualExitReached -= HandleVirtualExitReached;
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void CacheDocument()
        {
            if (overlayDocument == null)
            {
                overlayDocument = GetComponent<UIDocument>();
            }

            if (overlayStylesheet == null)
            {
                overlayStylesheet = Resources.Load<StyleSheet>("UI/ExitOverlay/ExitOverlayStyles");
                if (overlayStylesheet == null)
                {
                    Debug.LogWarning("[ExitOverlayController] ExitOverlayStyles.uss not found in Resources/UI/ExitOverlay.", this);
                }
            }

            if (overlayDocument == null)
            {
                Debug.LogWarning("[ExitOverlayController] UIDocument reference missing.", this);
                return;
            }

            var root = overlayDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning("[ExitOverlayController] UIDocument root not ready.", this);
                return;
            }

            if (overlayStylesheet != null && !root.styleSheets.Contains(overlayStylesheet))
            {
                root.styleSheets.Add(overlayStylesheet);
            }

            overlayRoot = root.Q<VisualElement>("exit-overlay");
            titleLabel = root.Q<Label>("exit-title");
            scenarioLabel = root.Q<Label>("exit-scenario");
            introLabel = root.Q<Label>("exit-intro");
            stepsContainer = root.Q<VisualElement>("exit-steps");
            footerLabel = root.Q<Label>("exit-footer");
            congratsLabel = root.Q<Label>("exit-congrats");
            closeButton = root.Q<Button>("exit-close-button");
            detailsButton = root.Q<Button>("exit-details-button");

            if (overlayRoot == null)
            {
                Debug.LogWarning("[ExitOverlayController] Overlay root could not be located.", this);
                return;
            }

            overlayRoot.style.display = DisplayStyle.None;

            if (closeButton != null)
            {
                closeButton.clicked += HandleProceedClicked;
            }

            if (detailsButton != null)
            {
                detailsButton.clicked += HandleDetailsClicked;
            }

            initialized = true;
            ApplyScenarioContent(GetContentForType(DisasterTypeManager.SelectedDisasterType));
        }

        private void BuildScenarioLookup()
        {
            scenarioLookup.Clear();

            if (defaultContent == null)
            {
                defaultContent = CreateDefaultContent();
            }
            NormalizeContent(defaultContent);

            foreach (var content in scenarioContent)
            {
                if (content == null)
                {
                    continue;
                }

                NormalizeContent(content);
                scenarioLookup[content.disasterType] = content;
            }

            RegisterBuiltinDefaults();
        }

        private void RegisterBuiltinDefaults()
        {
            AddContentIfMissing(DisasterType.Fire,
                "Fire Evacuation Protocol",
                "You have successfully reached the safe evacuation zone. You are now clear of immediate fire hazards and danger zones.",
                "Stay alert and wait for fire safety officers to provide clearance before any movement.",
                new[]
                {
                    "Confirm all team members are present at this assembly point",
                    "Stay low and avoid smoke if it drifts toward this area",
                    "Keep evacuation routes clear for emergency responders",
                    "Do NOT re-enter the building until authorized by fire wardens"
                },
                "Finished Simulation",
                "");

            AddContentIfMissing(DisasterType.Earthquake,
                "Earthquake Safety Protocol",
                "You have successfully evacuated to a safe zone away from structural hazards. Remain alert as aftershocks may still occur.",
                "Stay in this open area and keep distance from buildings, power lines, and glass structures.",
                new[]
                {
                    "Move away from building facades, windows, and overhead power lines",
                    "Conduct a quick self-check and assist injured teammates if safe",
                    "Remain in open ground away from structures that may collapse",
                    "Await further instructions from safety coordinators"
                },
                "Finished Simulation",
                "");

            AddContentIfMissing(DisasterType.Flood,
                "Flood Safety Protocol",
                "You have reached higher ground and are now safe from immediate flood pathways. Water levels may continue to rise—remain vigilant.",
                "Stay on high ground and maintain clear communication channels with emergency responders.",
                new[]
                {
                    "Avoid contact with any standing or flowing water—it may be contaminated or electrified",
                    "Move personal belongings and equipment above projected water rise levels",
                    "Keep evacuation routes clear for emergency response vehicles",
                    "Report water level changes and hazards to emergency services immediately"
                },
                "Finished Simulation",
                "");

            AddContentIfMissing(DisasterType.GeneralSafety,
                "General Evacuation",
                defaultContent.introText,
                defaultContent.footerText,
                defaultContent.steps,
                defaultContent.proceedButtonText,
                defaultContent.detailsButtonText);
        }

        private void AddContentIfMissing(
            DisasterType type,
            string scenarioLabelText,
            string intro,
            string footer,
            IReadOnlyList<string> steps,
            string proceedText,
            string detailsText)
        {
            if (scenarioLookup.ContainsKey(type))
            {
                return;
            }

            var content = new ExitScenarioContent
            {
                disasterType = type,
                scenarioLabel = scenarioLabelText,
                introText = intro,
                footerText = footer,
                steps = steps != null ? new List<string>(steps) : null,
                proceedButtonText = proceedText,
                detailsButtonText = detailsText
            };

            NormalizeContent(content);
            scenarioLookup[type] = content;
        }

        private ExitScenarioContent CreateDefaultContent()
        {
            var content = new ExitScenarioContent
            {
                disasterType = DisasterType.GeneralSafety,
                scenarioLabel = "General Evacuation Protocol",
                introText = "You have successfully reached the designated evacuation assembly point. You are now in a safe zone away from immediate hazards.",
                footerText = "Remain calm, stay in this safe area, and await further instructions from safety personnel.",
                steps = new List<string>
                {
                    "Report your arrival to the safety officer or team leader immediately",
                    "Assist anyone in your team who may need help or medical attention",
                    "Stay within the marked safe perimeter boundaries at all times",
                    "Keep communication lines open and listen for further instructions"
                },
                proceedButtonText = "Finished Simulation",
                detailsButtonText = ""
            };

            return content;
        }

        private void NormalizeContent(ExitScenarioContent content)
        {
            if (content.steps == null || content.steps.Count == 0)
            {
                content.steps = new List<string>
                {
                    "Report your arrival to the safety officer or team leader",
                    "Assist anyone in your team who needs help or medical attention",
                    "Stay within the designated safe perimeter at all times",
                    "Keep communication open and await further instructions"
                };
            }

            if (string.IsNullOrWhiteSpace(content.introText))
            {
                content.introText = "You have successfully reached the designated evacuation assembly point. You are now in a safe zone away from immediate hazards.";
            }

            if (string.IsNullOrWhiteSpace(content.footerText))
            {
                content.footerText = "Remain calm, stay in this safe area, and await further instructions from safety personnel.";
            }

            if (string.IsNullOrWhiteSpace(content.scenarioLabel))
            {
                content.scenarioLabel = "General Evacuation Protocol";
            }

            if (string.IsNullOrWhiteSpace(content.proceedButtonText))
            {
                content.proceedButtonText = "Finished Simulation";
            }

            if (string.IsNullOrWhiteSpace(content.detailsButtonText))
            {
                content.detailsButtonText = "";
            }
        }

        private void HandleDisasterTypeChanged(DisasterType disasterType)
        {
            ApplyScenarioContent(GetContentForType(disasterType));
        }

        private ExitScenarioContent GetContentForType(DisasterType disasterType)
        {
            if (scenarioLookup.TryGetValue(disasterType, out var content))
            {
                return content;
            }

            if (scenarioLookup.TryGetValue(DisasterType.GeneralSafety, out var fallback))
            {
                return fallback;
            }

            return defaultContent ?? CreateDefaultContent();
        }

        private void ApplyScenarioContent(ExitScenarioContent content)
        {
            if (!initialized || overlayRoot == null || content == null)
            {
                return;
            }

            if (titleLabel != null)
            {
                titleLabel.text = "Evacuation Point Reached";
            }

            if (scenarioLabel != null)
            {
                scenarioLabel.text = content.scenarioLabel;
            }

            if (introLabel != null)
            {
                introLabel.text = content.introText;
            }

            if (footerLabel != null)
            {
                footerLabel.text = content.footerText;
            }

            if (closeButton != null)
            {
                closeButton.text = content.proceedButtonText;
            }

            if (detailsButton != null)
            {
                detailsButton.text = content.detailsButtonText;
                detailsButton.style.display = string.IsNullOrWhiteSpace(content.detailsButtonText)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            if (stepsContainer != null)
            {
                stepsContainer.Clear();
                var steps = content.steps;
                for (int i = 0; i < steps.Count; i++)
                {
                    var stepText = steps[i];
                    if (string.IsNullOrWhiteSpace(stepText))
                    {
                        continue;
                    }

                    var label = new Label
                    {
                        text = $"{i + 1}. {stepText}"
                    };
                    label.AddToClassList("exit-step");
                    if (i > 0)
                    {
                        label.style.marginTop = 12f;
                    }
                    stepsContainer.Add(label);
                }
            }
        }

        public void Show()
        {
            ShowForDisaster(DisasterTypeManager.SelectedDisasterType);
        }

        public void ShowForDisaster(DisasterType disasterType)
        {
            if (!initialized)
            {
                CacheDocument();
            }

            ApplyScenarioContent(GetContentForType(disasterType));

            if (overlayRoot != null)
            {
                overlayRoot.style.display = DisplayStyle.Flex;
                overlayRoot.Focus();
            }
        }

        public void Hide()
        {
            if (overlayRoot != null)
            {
                overlayRoot.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Handle virtual exit reached event from navigation validator.
        /// Virtual exits now act like standard exits - just show the exit overlay for current disaster.
        /// (Flood 2nd floor logic is handled by ARSafeActivationController)
        /// </summary>
        private void HandleVirtualExitReached(ARSafe.Content.VirtualExitMarker exitMarker)
        {
            if (exitMarker == null)
                return;

            Debug.Log($"[ExitOverlayController] Virtual exit reached: {exitMarker.gameObject.name}");

            // Show standard exit overlay for current disaster type
            ShowForDisaster(DisasterTypeManager.SelectedDisasterType);
        }

        private void HandleProceedClicked()
        {
            // Start congratulations and return to menu sequence
            if (returnToMenuCoroutine != null)
            {
                StopCoroutine(returnToMenuCoroutine);
            }
            returnToMenuCoroutine = StartCoroutine(ShowCongratulationsAndReturnToMenu());

            onProceedToSafeZone?.Invoke();
        }

        private void HandleDetailsClicked()
        {
            Hide();
            onReviewChecklist?.Invoke();
        }

        private IEnumerator ShowCongratulationsAndReturnToMenu()
        {
            // Hide the exit steps, intro, and footer
            if (introLabel != null)
            {
                introLabel.style.display = DisplayStyle.None;
            }
            if (stepsContainer != null)
            {
                stepsContainer.style.display = DisplayStyle.None;
            }
            if (footerLabel != null)
            {
                footerLabel.style.display = DisplayStyle.None;
            }
            if (closeButton != null)
            {
                closeButton.style.display = DisplayStyle.None;
            }

            // Show congratulations message
            if (congratsLabel != null)
            {
                congratsLabel.text = congratulationsMessage;
                congratsLabel.style.display = DisplayStyle.Flex;
            }

            // Update title
            if (titleLabel != null)
            {
                titleLabel.text = "Simulation Complete!";
            }

            // Wait for specified time
            yield return new WaitForSeconds(congratulationsDisplayTime);

            // Return to main menu
            ReturnToMainMenu();
        }

        private void ReturnToMainMenu()
        {
            if (useSceneLoaderManager)
            {
                bl_SceneLoaderManager.LoadScene(menuSceneName);
            }
            else
            {
                SceneManager.LoadScene(menuSceneName);
            }
        }
    }
}
