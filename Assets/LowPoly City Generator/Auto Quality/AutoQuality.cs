using UnityEngine;
using UnityEngine.UI;

// Namespace used for organization in the Fantastic City Generator project
namespace LPCG
{
    public class AutoQuality : MonoBehaviour
    {
        [Header("Display Options")]
        public bool ShowQualityLevel = true; // Whether to display the current Quality Level on screen
        public bool ShowFps = true;          // Whether to display the current FPS on screen
        public bool UpdateColor = true;      // Whether the text color should reflect FPS performance

        float UpdateFrequency = 0.5f;        // How often the displayed text is updated (in seconds)

        [Header("Optional")]
        public GameObject postProcess;       // Optional: GameObject controlling post-processing, toggled based on quality level

        // Internal variables for FPS calculation
        private float accum = 0f;               // Sum of frame rates over time
        private int frames = 0;                 // Number of frames counted
        private float fps;                      // Current calculated FPS
        private string sFPS;                    // FPS as string (for display)
        private int currentQualityLevel = -1;   // Tracks the current quality level
        private int numberOfQualityLevels = 5;  // Total number of available quality levels

        private Text displayText;               // Reference to the UI Text component showing info
        private float timeLeftToUpdateText;     // Timer to control how often the text is updated

        void Start()
        {
            // Get the number of quality levels defined in the project
            numberOfQualityLevels = QualitySettings.names.Length;

            // Create Canvas and UI Text dynamically
            CreateUI();

            // Start the repeating quality evaluation every 2 seconds
            InvokeRepeating(nameof(UpdateAutoQuality), 0.5f, 2f);

            // Initialize update timer
            timeLeftToUpdateText = UpdateFrequency;
        }

        void Update()
        {
            // Accumulate frame rate values
            accum += Time.timeScale / Time.deltaTime;
            frames++;

            // Countdown to update the on-screen text
            timeLeftToUpdateText -= Time.deltaTime;
            if (timeLeftToUpdateText <= 0f)
            {
                UpdateDisplay(); // Update the display
                accum = 0f;
                frames = 0;
                timeLeftToUpdateText = UpdateFrequency;
            }
        }

        void UpdateAutoQuality()
        {
            // Initialize quality level on first call
            if (currentQualityLevel == -1)
                currentQualityLevel = QualitySettings.GetQualityLevel();

            // Decrease quality if FPS is too low
            if (fps < 28 && currentQualityLevel > 0)
            {
                currentQualityLevel--;
                QualitySettings.SetQualityLevel(currentQualityLevel, false);
            }
            // Increase quality if FPS is high enough
            else if (fps > 50 && currentQualityLevel < numberOfQualityLevels - 1)
            {
                currentQualityLevel++;
                QualitySettings.SetQualityLevel(currentQualityLevel, false);
            }

            // Optionally enable/disable post-processing based on quality
            if (postProcess != null)
                postProcess.SetActive(currentQualityLevel >= numberOfQualityLevels - 1);
        }

        void UpdateDisplay()
        {
            // Avoid division by zero
            if (frames == 0) return;

            // Calculate and round FPS
            fps = accum / frames;
            sFPS = Mathf.RoundToInt(fps).ToString();

            if (displayText == null) return;

            // Build the display string
            string text = "";
            if (ShowFps)
                text += sFPS + " FPS";

            if (ShowQualityLevel)
            {
                if (text != "")
                    text += " | ";
                text += "Quality: " + currentQualityLevel;
            }

            displayText.text = text;

            // Update text color based on performance
            if (UpdateColor)
            {
                if (fps >= 30)
                    displayText.color = Color.green;
                else if (fps > 10)
                    displayText.color = Color.yellow;
                else
                    displayText.color = Color.red;
            }
            else
            {
                displayText.color = Color.white;
            }
        }

        void CreateUI()
        {
            // Prevent multiple UI canvases from being created
            if (GetComponentInChildren<Canvas>() != null)
                return;

            // Create Canvas object
            GameObject canvasGO = new GameObject("AutoQualityCanvas");
            canvasGO.transform.SetParent(this.transform);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Create Text UI object
            GameObject textGO = new GameObject("AutoQualityText");
            textGO.transform.SetParent(canvasGO.transform);
            displayText = textGO.AddComponent<Text>();

            // Set font based on Unity version
#if UNITY_2022_1_OR_NEWER
            displayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            displayText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif

            displayText.fontSize = 26;
            displayText.alignment = TextAnchor.UpperCenter;
            displayText.color = Color.white;

            // Positioning and size
            RectTransform rectTransform = displayText.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0, -40);
            rectTransform.sizeDelta = new Vector2(600, 100);
        }
    }
}
