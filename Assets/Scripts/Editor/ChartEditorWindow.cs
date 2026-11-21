using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

public enum ChartNoteType
{
    Tap = 0,
    Hold = 1,
    Catch = 2
}

// ===== [ROTATE - Langkah 1] Data movement =====
public enum ChartMovementType
{
    Rotate = 0
}

[System.Serializable]
public class ChartNote
{
    public float beat;
    public int lane;            // 1-based index
    public ChartNoteType type;
    public float length;        // In beats (used by Hold notes)
}

// ===== [ROTATE - Langkah 1] Data movement =====
[System.Serializable]
public class ChartMovement
{
    public float beat;
    public ChartMovementType type;
    public float length;        // dalam beat
    public float degrees;       // + searah jarum jam, - berlawanan
}

public class ChartEditorWindow : EditorWindow
{
    // Project Data
    private string projectName = "New Chart";
    private float bpm = 120f;
    private float duration = 60f; // In seconds
    private string artistName = string.Empty;
    private AudioClip audioClip;
    private List<ChartNote> notes = new List<ChartNote>();

    // ===== [ROTATE - Langkah 2] =====
    private List<ChartMovement> movements = new List<ChartMovement>();

    // Snap & Subdivision synchronization
    private int snapDivision = 4; // Default subdivision = 4
    private static readonly int[] SnapValues = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 24, 32 };
    private static readonly string[] SnapLabels = {
        "2 beats", "3 beats", "4 beats", "5 beats", "6 beats", "7 beats", "8 beats",
        "9 beats", "10 beats", "11 beats", "12 beats", "13 beats", "14 beats", "15 beats", "16 beats", "24 beats", "32 beats"
    };

    // Timeline visualization
    private Vector2 scrollPosition;
    private float timelineWidth = 2000f;
    private float timelineHeight = 330f;   // [ROTATE - Langkah 2] dulu 300f
    private float currentTime = 0f; // Playhead position in seconds
    private float timelineViewWidth = 800f;
    private float visibleMinX = 0f;
    private float visibleMaxX = 2000f;

    // Grid settings
    private float pixelsPerSecond = 100f; // Default initial zoom set to 100
    private const float beatsPerMeasure = 1f;

    // Colors
    private Color gridColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    private Color measureLineColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    private Color playheadColor = new Color(0.2f, 0.6f, 1f, 1f);
    private Color gridBorderColor = new Color(0.32f, 0.32f, 0.32f, 1f);
    private static readonly Color TapColor = new Color(0.4f, 0.79f, 0.94f, 1f);
    private static readonly Color HoldColor = new Color(0.96f, 0.64f, 0.38f, 1f);
    private static readonly Color CatchColor = new Color(1f, 1f, 1f, 1f); // White
    // ===== [ROTATE - Langkah 2] =====
    private static readonly Color RotateColor = new Color(0.72f, 0.45f, 0.95f, 1f);    // searah jarum jam (ungu)
    private static readonly Color RotateColorCCW = new Color(0.95f, 0.82f, 0.86f, 1f); // berlawanan jarum jam (pink)

    // File path
    private string projectFilePath = "";

    // Audio playback
    private bool isPlaying = false;
    private double audioStartTime = 0;
    private System.Reflection.Assembly unityEditorAssembly;
    private System.Type audioUtilClass;
    private MethodInfo playPreviewClipMethod;
    private MethodInfo stopAllPreviewClipsMethod;
    private float laneLength = 4f;

    private bool showLanePreview = true;
    private float previewLeadBeats = 2f;
    private float editorContentWidth;

    // Layout constants
    private const float PanelHeight = 260f;          // [ROTATE - Langkah 2] dulu 220f
    private const float KeyframeRowHeight = 22f;
    private const float KeyframeListHeight = 158f;   // [ROTATE - Langkah 2] dulu 118f
    private const float RulerHeight = 24f;
    private const float MovementTrackHeight = 28f;   // [ROTATE - Langkah 2]
    private const float FooterHeight = 20f;
    private const float NoteHeadWidth = 14f;
    private const float StandardButtonHeight = 22f;

    private const float LanePreviewSidebarWidth = 300f;
    private const float LanePreviewSidebarGap = 8f;

    // Note editing
    private ChartNoteType selectedNoteType = ChartNoteType.Tap;
    private bool isPlacingHold = false;
    private int holdLane;
    private float holdStartBeat;
    private float holdPreviewLength;

    // ===== [ROTATE - Langkah 2] Movement editing =====
    private bool movementToolActive = false;
    private ChartMovementType selectedMovementType = ChartMovementType.Rotate;
    private bool isPlacingMovement = false;
    private float movementStartBeat;
    private float movementPreviewLength;
    private float rotateDegrees = 360f; // derajat untuk blok Rotate berikutnya

    // Keyframe panel
    private int keyframeFilter = 0;
    private bool followPlayhead = true;
    private Vector2 keyframeScroll;
    private readonly List<ChartNote> keyframeView = new List<ChartNote>();
    private bool keyframeViewDirty = true;
    private int lastCurrentRow = -2;
    private int pendingScrollRow = -1;
    private int resizingIndex = -1;

    // Styles
    private GUIStyle timeStyle, beatStyle, mutedLabel, hintStyle, emptyStyle, rowStyle, rowPastStyle, rowCenterStyle, headerBoxStyle;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Derived values
    private int LaneCount => Mathf.Clamp(Mathf.RoundToInt(laneLength), 1, 8);
    private float SecondsPerBeat => 60f / bpm;
    private float TotalBeats => duration / SecondsPerBeat;
    private float CurrentBeat => currentTime / SecondsPerBeat;
    private float SnapBeats => 1f / snapDivision;
    private float TimelineNoteHeadWidth => Mathf.Clamp(SecondsPerBeat * pixelsPerSecond * SnapBeats * 0.68f, 1f, NoteHeadWidth);
    // ===== [ROTATE - Langkah 2] =====
    private float LaneAreaTop => RulerHeight + MovementTrackHeight;
    private float LaneHeight => (timelineHeight - LaneAreaTop - FooterHeight) / LaneCount;

    private int tapCount = 0;
    private int holdCount = 0;
    private int catchCount = 0;

    [MenuItem("Window/Chart Editor")]
    public static void ShowWindow()
    {
        ChartEditorWindow window = GetWindow<ChartEditorWindow>("Chart Editor");
        window.minSize = new Vector2(1280, 850);
        window.Show();
    }

    private void OnEnable()
    {
        minSize = new Vector2(1280, 850);
        unityEditorAssembly = typeof(AudioImporter).Assembly;
        audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");

        if (audioUtilClass != null)
        {
            playPreviewClipMethod = audioUtilClass.GetMethod(
                "PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null
            );

            stopAllPreviewClipsMethod = audioUtilClass.GetMethod(
                "StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public
            );
        }

        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        StopAudio();
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (!isPlaying) return;

        double elapsed = EditorApplication.timeSinceStartup - audioStartTime;
        currentTime = (float)elapsed;

        float end = audioClip != null ? audioClip.length : duration;

        if (currentTime >= end)
        {
            currentTime = end;
            StopAudio();
        }

        Repaint();
    }

    private void OnGUI()
    {
        EnsureStyles();
        HandleGlobalShortcuts();

        editorContentWidth = Mathf.Max(900f, position.width - LanePreviewSidebarWidth - LanePreviewSidebarGap - 8f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(editorContentWidth), GUILayout.ExpandHeight(true));

        DrawToolbar();

        EditorGUILayout.Space(6);

        // Top Header Banner
        DrawTopHeaderPanel();

        EditorGUILayout.Space(6);

        // 3 Uniform Columns
        float totalWidth = editorContentWidth - 24f;
        float columnWidth = Mathf.Max(280f, totalWidth / 3f);

        EditorGUILayout.BeginHorizontal();

        // Column 1: Configuration & Note Tools
        DrawSettingsColumn(columnWidth);

        GUILayout.Space(6);

        // Column 2: Audio Playback Controls
        DrawAudioPanel(columnWidth);

        GUILayout.Space(6);

        // Column 3: Excel Grid Keyframes Panel
        DrawKeyframePanel(columnWidth);

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Timeline
        DrawTimeline();

        EditorGUILayout.EndVertical();

        GUILayout.Space(LanePreviewSidebarGap);
        DrawLanePreviewSidebar();

        EditorGUILayout.EndHorizontal();

        // [ROTATE - Langkah 7] tambah isPlacingMovement
        if (Event.current.type == EventType.MouseDrag || isPlacingHold || isPlacingMovement)
        {
            Repaint();
        }
    }

    private void EnsureStyles()
    {
        if (timeStyle != null) return;

        Font mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "Courier New" }, 12);
        Color muted = new Color(0.70f, 0.70f, 0.70f, 1f);

        timeStyle = new GUIStyle(EditorStyles.label) { font = mono, fontSize = 12, alignment = TextAnchor.MiddleLeft };

        beatStyle = new GUIStyle(timeStyle) { fontSize = 11 };
        beatStyle.normal.textColor = new Color(0.62f, 0.76f, 1f, 1f);

        mutedLabel = new GUIStyle(EditorStyles.label) { fontSize = 11, clipping = TextClipping.Clip };
        mutedLabel.normal.textColor = muted;

        hintStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 11,
            wordWrap = true,
            margin = new RectOffset(0, 0, 2, 2),
            padding = new RectOffset(0, 0, 0, 0)
        };
        hintStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f, 1f);

        emptyStyle = new GUIStyle(EditorStyles.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        emptyStyle.normal.textColor = muted;

        rowStyle = new GUIStyle(EditorStyles.label) { font = mono, fontSize = 11, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        rowStyle.normal.textColor = new Color(0.89f, 0.89f, 0.89f, 1f);

        rowPastStyle = new GUIStyle(rowStyle);
        rowPastStyle.normal.textColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        rowCenterStyle = new GUIStyle(rowStyle) { alignment = TextAnchor.MiddleCenter };
        rowCenterStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f, 1f);

        headerBoxStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            margin = new RectOffset(0, 0, 0, 6)
        };
    }

    private void HandleGlobalShortcuts()
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && EditorGUIUtility.editingTextField)
        {
            GUI.FocusControl(null);
        }
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Space && !EditorGUIUtility.editingTextField)
            {
                TogglePlayback();
                e.Use();
            }
            // ===== [ROTATE - Langkah 3] Esc juga membatalkan movement =====
            else if (e.keyCode == KeyCode.Escape && (isPlacingHold || isPlacingMovement))
            {
                isPlacingHold = false;
                isPlacingMovement = false;
                e.Use();
                Repaint();
            }
            else if (e.keyCode == KeyCode.S && !EditorGUIUtility.editingTextField)
            {
                currentTime = 0f;
                e.Use();
                Repaint();
            }
            else if (e.keyCode == KeyCode.Alpha1 && !EditorGUIUtility.editingTextField)
            {
                selectedNoteType = ChartNoteType.Tap;
                movementToolActive = false;   // [ROTATE - Langkah 3]
                e.Use();
                Repaint();
            }
            else if (e.keyCode == KeyCode.Alpha2 && !EditorGUIUtility.editingTextField)
            {
                selectedNoteType = ChartNoteType.Hold;
                movementToolActive = false;   // [ROTATE - Langkah 3]
                e.Use();
                Repaint();
            }
            else if (e.keyCode == KeyCode.Alpha3 && !EditorGUIUtility.editingTextField)
            {
                selectedNoteType = ChartNoteType.Catch;
                movementToolActive = false;   // [ROTATE - Langkah 3]
                e.Use();
                Repaint();
            }
            // ===== [ROTATE - Langkah 3] Tombol 4 = tool Rotate =====
            else if (e.keyCode == KeyCode.Alpha4 && !EditorGUIUtility.editingTextField)
            {
                movementToolActive = true;
                selectedMovementType = ChartMovementType.Rotate;
                isPlacingHold = false;
                e.Use();
                Repaint();
            }
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("New Project", EditorStyles.toolbarButton, GUILayout.Width(90)))
        {
            NewProject();
        }

        if (GUILayout.Button("Load Project", EditorStyles.toolbarButton, GUILayout.Width(90)))
        {
            LoadProject();
        }

        if (GUILayout.Button("Save Project", EditorStyles.toolbarButton, GUILayout.Width(90)))
        {
            SaveProject();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTopHeaderPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        float fieldWidth = Mathf.Min(450f, position.width * 0.45f);
        EditorGUIUtility.labelWidth = 100f;

        EditorGUILayout.BeginVertical(GUILayout.Width(fieldWidth));
        projectName = EditorGUILayout.TextField("Project Name", projectName);
        EditorGUILayout.Space(2);
        artistName = EditorGUILayout.TextField("Artist Name", artistName);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawSettingsColumn(float width)
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(width), GUILayout.Height(PanelHeight));
        EditorGUILayout.LabelField("Chart Configuration", headerBoxStyle);

        EditorGUIUtility.labelWidth = 105f;

        bpm = Mathf.Clamp(EditorGUILayout.FloatField("BPM", bpm), 1f, 999f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Beats / Measure", GUILayout.Width(105));
        EditorGUILayout.LabelField("1", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        if (audioClip != null)
        {
            duration = audioClip.length;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.FloatField("Duration (sec)", duration);
            EditorGUILayout.LabelField("(Auto)", EditorStyles.miniLabel, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            duration = Mathf.Clamp(EditorGUILayout.FloatField("Duration (sec)", duration), 1f, 1200f);
        }

        EditorGUILayout.LabelField($"Total Beats: {TotalBeats:F2}", hintStyle);

        EditorGUILayout.Space(4);
        laneLength = Mathf.Clamp(EditorGUILayout.FloatField("Lane Count", laneLength), 1f, 8f);

        // ===== Note Type [ROTATE - Langkah 4a] =====
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Note Type", GUILayout.Width(105));

        if (GUILayout.Toggle(!movementToolActive && selectedNoteType == ChartNoteType.Tap, "Tap", EditorStyles.toolbarButton, GUILayout.Width(48), GUILayout.Height(StandardButtonHeight)))
        {
            selectedNoteType = ChartNoteType.Tap;
            movementToolActive = false;
            isPlacingHold = false;
            isPlacingMovement = false;
        }
        if (GUILayout.Toggle(!movementToolActive && selectedNoteType == ChartNoteType.Hold, "Hold", EditorStyles.toolbarButton, GUILayout.Width(48), GUILayout.Height(StandardButtonHeight)))
        {
            selectedNoteType = ChartNoteType.Hold;
            movementToolActive = false;
            isPlacingMovement = false;
        }
        if (GUILayout.Toggle(!movementToolActive && selectedNoteType == ChartNoteType.Catch, "Catch", EditorStyles.toolbarButton, GUILayout.Width(48), GUILayout.Height(StandardButtonHeight)))
        {
            selectedNoteType = ChartNoteType.Catch;
            movementToolActive = false;
            isPlacingHold = false;
            isPlacingMovement = false;
        }
        EditorGUILayout.EndHorizontal();

        // ===== Movement Type [ROTATE - Langkah 4b] =====
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Movement Type", GUILayout.Width(105));
        if (GUILayout.Toggle(movementToolActive && selectedMovementType == ChartMovementType.Rotate, "Rotate", EditorStyles.toolbarButton, GUILayout.Width(60), GUILayout.Height(StandardButtonHeight)))
        {
            movementToolActive = true;
            selectedMovementType = ChartMovementType.Rotate;
            isPlacingHold = false;
        }
        GUILayout.Space(6);
        EditorGUILayout.LabelField("Deg", GUILayout.Width(26));
        rotateDegrees = EditorGUILayout.FloatField(rotateDegrees, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        snapDivision = EditorGUILayout.IntPopup("Snap / Subdiv", snapDivision, SnapLabels, SnapValues);

        // [ROTATE - Langkah 4b] cabang hint movement paling atas
        if (movementToolActive)
        {
            EditorGUILayout.LabelField("Rotate: klik track Movement (awal), klik lagi (akhir). Esc batal, klik kanan hapus.", hintStyle);
        }
        else if (selectedNoteType == ChartNoteType.Hold)
        {
            EditorGUILayout.LabelField("Hold Mode: Click 1st beat (start), click 2nd beat (end). Esc to cancel.", hintStyle);
        }
        else
        {
            EditorGUILayout.LabelField("Click lane to place note. Right-click note to delete.", hintStyle);
        }

        if (GUILayout.Button("Clear Note", GUILayout.Height(StandardButtonHeight), GUILayout.Width(70)))
        {
            notes.Clear();
            keyframeView.Clear();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawAudioPanel(float width)
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(width), GUILayout.Height(PanelHeight));

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Audio Playback", headerBoxStyle);
        if (GUILayout.Button(audioClip != null ? "Change" : "+ Add music", GUILayout.Width(82), GUILayout.Height(20)))
        {
            AddMusic();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Label(audioClip != null ? audioClip.name : "No music loaded", mutedLabel, GUILayout.Height(18));

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = playheadColor;
        if (GUILayout.Button(isPlaying ? "⏸ Pause" : "▶ Play", GUILayout.Height(StandardButtonHeight), GUILayout.Width(75)))
        {
            TogglePlayback();
        }
        GUI.backgroundColor = previousBackground;

        if (GUILayout.Button("⏹ Stop", GUILayout.Height(StandardButtonHeight), GUILayout.Width(60)))
        {
            StopAudio();
        }

        GUILayout.Space(4);

        EditorGUILayout.BeginVertical();
        GUILayout.Label(FormatTime(currentTime) + " / " + FormatTime(duration), timeStyle);
        GUILayout.Label("Beat " + CurrentBeat.ToString("0.00", Inv), beatStyle);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        EditorGUI.BeginChangeCheck();
        float seek = GUILayout.HorizontalSlider(currentTime, 0f, duration);
        if (EditorGUI.EndChangeCheck())
        {
            SeekTo(seek);
        }

        EditorGUILayout.Space(14);
        GUILayout.Label("Controls:\n• Scroll Wheel: Move Timeline Left/Right\n• Ctrl/Cmd + Scroll Wheel: Zoom In/Out\n• Space: Play / Pause", hintStyle);

        EditorGUILayout.EndVertical();
    }

    private void AddMusic()
    {
        string path = EditorUtility.OpenFilePanel("Select music", Application.dataPath, "mp3,wav,ogg");
        if (string.IsNullOrEmpty(path)) return;

        string assetPath;
        string dataPath = Application.dataPath;

        if (path.StartsWith(dataPath, System.StringComparison.OrdinalIgnoreCase))
        {
            assetPath = "Assets" + path.Substring(dataPath.Length);
        }
        else
        {
            bool copy = EditorUtility.DisplayDialog(
                "Copy music into project?",
                "This file is outside your Unity project. Copy it to Assets/ChartAudio?",
                "Copy",
                "Cancel");
            if (!copy) return;

            string folder = dataPath + "/ChartAudio";
            Directory.CreateDirectory(folder);

            string fileName = Path.GetFileName(path);
            File.Copy(path, folder + "/" + fileName, true);
            assetPath = "Assets/ChartAudio/" + fileName;
        }

        AssetDatabase.ImportAsset(assetPath);
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);

        if (clip == null)
        {
            EditorUtility.DisplayDialog("Add music", "Couldn't load that file as an audio clip.", "OK");
            return;
        }

        StopAudio();
        audioClip = clip;
        duration = clip.length;
        currentTime = 0f;
        Repaint();
    }

    private void TogglePlayback()
    {
        if (isPlaying) PauseAudio();
        else PlayAudio();
    }

    private void DrawKeyframePanel(float width)
    {
        EnsureKeyframeView();
        tapCount = 0;
        holdCount = 0;
        catchCount = 0;

        for (int i = 0; i < notes.Count; i++)
        {
            ChartNote note = notes[i];
            if (note.type == ChartNoteType.Tap)
            {
                tapCount++;
            }
            else if (note.type == ChartNoteType.Hold)
            {
                holdCount++;
            }
            else if (note.type == ChartNoteType.Catch)
            {
                catchCount++;
            }
        }
        string[] FilterLabels = { $"All ({notes.Count})", $"Tap ({tapCount})", $"Hold ({holdCount})", $"Catch ({catchCount})" };

        EditorGUILayout.BeginVertical("box", GUILayout.Width(width), GUILayout.Height(PanelHeight));

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Keyframes (" + notes.Count + ")", headerBoxStyle);
        if (GUILayout.Button(new GUIContent("‹", "Previous keyframe"), EditorStyles.miniButtonLeft, GUILayout.Width(24), GUILayout.Height(StandardButtonHeight - 2f)))
        {
            JumpToAdjacentKeyframe(-1);
        }
        if (GUILayout.Button(new GUIContent("›", "Next keyframe"), EditorStyles.miniButtonRight, GUILayout.Width(24), GUILayout.Height(StandardButtonHeight - 2f)))
        {
            JumpToAdjacentKeyframe(1);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        keyframeFilter = GUILayout.Toolbar(keyframeFilter, FilterLabels, GUILayout.Width(250), GUILayout.Height(StandardButtonHeight));
        if (EditorGUI.EndChangeCheck())
        {
            keyframeViewDirty = true;
        }
        GUILayout.FlexibleSpace();
        followPlayhead = GUILayout.Toggle(followPlayhead, new GUIContent("Follow", "Auto-scroll list with playhead"), GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();

        EnsureKeyframeView();

        EditorGUILayout.Space(2);

        // Excel Table Header with Box Separators
        Rect header = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(header, new Color(0.22f, 0.22f, 0.22f, 1f));

        float col1W = 75f;
        float col2W = 50f;

        GUI.Label(new Rect(header.x + 6, header.y + 2, col1W - 6, header.height - 2), "Beat", mutedLabel);
        GUI.Label(new Rect(header.x + col1W + 6, header.y + 2, col2W - 6, header.height - 2), "Lane", mutedLabel);
        GUI.Label(new Rect(header.x + col1W + col2W + 6, header.y + 2, header.width - col1W - col2W - 6, header.height - 2), "Type", mutedLabel);

        // Vertical Excel grid lines for Header
        EditorGUI.DrawRect(new Rect(header.x + col1W, header.y, 1f, header.height), gridBorderColor);
        EditorGUI.DrawRect(new Rect(header.x + col1W + col2W, header.y, 1f, header.height), gridBorderColor);
        EditorGUI.DrawRect(new Rect(header.x, header.y + header.height - 1f, header.width, 1f), gridBorderColor);

        Rect listRect = GUILayoutUtility.GetRect(0, KeyframeListHeight - 20, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(listRect, new Color(0.16f, 0.16f, 0.16f, 1f));
        DrawKeyframeList(listRect, col1W, col2W);

        EditorGUILayout.EndVertical();
    }

    private void DrawKeyframeList(Rect listRect, float col1W, float col2W)
    {
        if (keyframeView.Count == 0)
        {
            GUI.Label(listRect, "No keyframes yet.\nClick a lane in the timeline to add notes.", emptyStyle);
            return;
        }

        int cur = FindCurrentRow();
        if (cur != lastCurrentRow)
        {
            lastCurrentRow = cur;
            if (followPlayhead && cur >= 0)
            {
                pendingScrollRow = cur;
            }
        }

        if (pendingScrollRow >= 0)
        {
            keyframeScroll.y = Mathf.Max(0f, pendingScrollRow * KeyframeRowHeight - listRect.height * 0.5f + KeyframeRowHeight * 0.5f);
            pendingScrollRow = -1;
        }

        float contentHeight = keyframeView.Count * KeyframeRowHeight;
        float viewWidth = listRect.width - (contentHeight > listRect.height ? 15f : 0f);

        keyframeScroll = GUI.BeginScrollView(listRect, keyframeScroll, new Rect(0, 0, viewWidth, contentHeight));

        int first = Mathf.Max(0, Mathf.FloorToInt(keyframeScroll.y / KeyframeRowHeight));
        int last = Mathf.Min(keyframeView.Count - 1, Mathf.CeilToInt((keyframeScroll.y + listRect.height) / KeyframeRowHeight));

        Event e = Event.current;
        ChartNote toDelete = null;
        ChartNote toJump = null;

        float col3W = viewWidth - col1W - col2W - 24f;

        for (int i = first; i <= last; i++)
        {
            ChartNote n = keyframeView[i];
            Rect row = new Rect(0, i * KeyframeRowHeight, viewWidth, KeyframeRowHeight);
            bool isCurrent = i == cur;
            bool isPast = i < cur;

            if (isCurrent)
            {
                EditorGUI.DrawRect(row, new Color(0.23f, 0.51f, 0.96f, 0.22f));
                EditorGUI.DrawRect(new Rect(row.x, row.y, 3, row.height), playheadColor);
            }
            else if (i % 2 == 1)
            {
                EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.03f));
            }

            GUIStyle style = isPast ? rowPastStyle : rowStyle;

            // Excel Column 1: Beat
            GUI.Label(new Rect(row.x + 6, row.y, col1W - 6, row.height), FormatBeat(n.beat), style);

            // Excel Column 2: Lane
            GUI.Label(new Rect(row.x + col1W + 6, row.y, col2W - 6, row.height), "L" + n.lane, style);

            // Excel Column 3: Type
            Color typeColor = n.type == ChartNoteType.Tap ? TapColor : (n.type == ChartNoteType.Hold ? HoldColor : CatchColor);
            EditorGUI.DrawRect(new Rect(row.x + col1W + col2W + 6, row.y + row.height * 0.5f - 4f, 8, 8), typeColor);

            string typeText = n.type == ChartNoteType.Tap ? "Tap" :
                             (n.type == ChartNoteType.Hold ? "Hold (" + n.length.ToString("0.##", Inv) + "b)" : "Catch");
            GUI.Label(new Rect(row.x + col1W + col2W + 18, row.y, Mathf.Max(0f, col3W - 18), row.height), typeText, style);

            // Excel Column 4: Delete button
            Rect deleteRect = new Rect(viewWidth - 24f, row.y, 24f, row.height);
            GUI.Label(deleteRect, "×", rowCenterStyle);

            // Excel Box Lines (Vertical Separators & Bottom Border)
            EditorGUI.DrawRect(new Rect(row.x + col1W, row.y, 1f, row.height), gridBorderColor);
            EditorGUI.DrawRect(new Rect(row.x + col1W + col2W, row.y, 1f, row.height), gridBorderColor);
            EditorGUI.DrawRect(new Rect(viewWidth - 24f, row.y, 1f, row.height), gridBorderColor);
            EditorGUI.DrawRect(new Rect(row.x, row.y + row.height - 1f, viewWidth, 1f), gridBorderColor);

            if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
            {
                if (deleteRect.Contains(e.mousePosition))
                {
                    toDelete = n;
                }
                else
                {
                    toJump = n;
                }
                e.Use();
            }
        }

        GUI.EndScrollView();

        if (toDelete != null)
        {
            notes.Remove(toDelete);
            keyframeViewDirty = true;
            Repaint();
        }
        else if (toJump != null)
        {
            JumpToBeat(toJump.beat);
        }
    }

    private void EnsureKeyframeView()
    {
        if (!keyframeViewDirty) return;
        keyframeViewDirty = false;

        keyframeView.Clear();
        for (int i = 0; i < notes.Count; i++)
        {
            ChartNote n = notes[i];
            bool include = keyframeFilter == 0
                || (keyframeFilter == 1 && n.type == ChartNoteType.Tap)
                || (keyframeFilter == 2 && n.type == ChartNoteType.Hold)
                || (keyframeFilter == 3 && n.type == ChartNoteType.Catch);
            if (include)
            {
                keyframeView.Add(n);
            }
        }

        lastCurrentRow = -2;
    }

    private int FindCurrentRow()
    {
        float beat = CurrentBeat + 0.0005f;
        int cur = -1;
        for (int i = 0; i < keyframeView.Count; i++)
        {
            if (keyframeView[i].beat <= beat)
            {
                cur = i;
            }
            else
            {
                break;
            }
        }
        return cur;
    }

    private void JumpToAdjacentKeyframe(int direction)
    {
        EnsureKeyframeView();
        float beat = CurrentBeat;
        ChartNote target = null;

        if (direction < 0)
        {
            for (int i = 0; i < keyframeView.Count; i++)
            {
                if (keyframeView[i].beat < beat - 0.001f) target = keyframeView[i];
                else break;
            }
        }
        else
        {
            for (int i = 0; i < keyframeView.Count; i++)
            {
                if (keyframeView[i].beat > beat + 0.001f)
                {
                    target = keyframeView[i];
                    break;
                }
            }
        }

        if (target != null)
        {
            JumpToBeat(target.beat);
        }
    }

    private void JumpToBeat(float beat)
    {
        SeekTo(beat * SecondsPerBeat);
        EnsurePlayheadVisible();
    }

    private void EnsurePlayheadVisible()
    {
        float x = currentTime * pixelsPerSecond;
        if (x < scrollPosition.x + 20f || x > scrollPosition.x + timelineViewWidth - 20f)
        {
            scrollPosition.x = Mathf.Max(0f, x - timelineViewWidth * 0.3f);
        }
    }

    // Overlap Prevention Helper
    private bool CanPlaceNote(int lane, float startBeat, float endBeat, ChartNoteType type, ChartNote ignoreNote = null)
    {
        if (type == ChartNoteType.Catch) return true;
        for (int i = 0; i < notes.Count; i++)
        {
            ChartNote n = notes[i];
            if (n == ignoreNote || n.lane != lane) continue;

            // RULE 2: Catch notes can coexist with Tap/Hold notes at the same beat or within a Hold duration,
            // but cannot stack on another Catch note (handled by Rule 1 above).
            if (n.type == ChartNoteType.Catch)
            {
                continue;
            }

            // RULE 3: Tap & Hold overlap rules
            float existingStart = n.beat;
            float existingEnd = (n.type == ChartNoteType.Hold) ? (n.beat + n.length) : n.beat;

            float newStart = startBeat;
            float newEnd = (type == ChartNoteType.Hold) ? endBeat : startBeat;

            const float overlapTolerance = 0.0001f;

            if (newStart <= existingEnd + overlapTolerance && newEnd >= existingStart - overlapTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private void AddNote(int lane, float beat, ChartNoteType type, float length)
    {
        float endBeat = (type == ChartNoteType.Hold) ? (beat + length) : beat;

        if (!CanPlaceNote(lane, beat, endBeat, type))
        {
            return;
        }

        notes.Add(new ChartNote
        {
            beat = beat,
            lane = lane,
            type = type,
            length = type == ChartNoteType.Hold ? length : 0f
        });

        SortNotes();
        Repaint();
    }

    private void SortNotes()
    {
        notes.Sort((a, b) =>
        {
            int c = a.beat.CompareTo(b.beat);
            return c != 0 ? c : a.lane.CompareTo(b.lane);
        });
        keyframeViewDirty = true;
    }

    // ===== [ROTATE - Langkah 5] Movement helpers =====

    // 5a. Cegah dua blok Rotate saling menumpuk (boleh menempel)
    private bool CanPlaceMovement(ChartMovementType type, float startBeat, float endBeat, ChartMovement ignore = null)
    {
        const float tol = 0.0001f;
        for (int i = 0; i < movements.Count; i++)
        {
            ChartMovement m = movements[i];
            if (m.type != type || m == ignore) continue;

            float mEnd = m.beat + m.length;
            if (startBeat < mEnd - tol && endBeat > m.beat + tol) return false;
        }
        return true;
    }

    // 5b. Tambah blok baru
    private void AddMovement(ChartMovementType type, float beat, float length, float degrees)
    {
        if (length <= 0f || Mathf.Approximately(degrees, 0f)) return;
        if (!CanPlaceMovement(type, beat, beat + length)) return;

        movements.Add(new ChartMovement { beat = beat, type = type, length = length, degrees = degrees });
        SortMovements();
        Repaint();
    }

    private void SortMovements()
    {
        movements.Sort((a, b) => a.beat.CompareTo(b.beat));
    }

    // Klik pertama: mulai menempatkan blok
    private void BeginPlaceMovement(float localX)
    {
        float beat = SnapBeat(localX);
        if (!CanPlaceMovement(selectedMovementType, beat, beat + SnapBeats)) return;

        isPlacingMovement = true;
        movementStartBeat = beat;
        movementPreviewLength = SnapBeats;
    }

    // 5c. Posisi blok di track dan hit-test untuk klik kanan
    private Rect GetMovementRect(float beat, float length)
    {
        float x = beat * SecondsPerBeat * pixelsPerSecond;
        float w = Mathf.Max(2f, length * SecondsPerBeat * pixelsPerSecond);
        return new Rect(x, RulerHeight + 2f, w, MovementTrackHeight - 4f);
    }

    private ChartMovement HitTestMovement(Vector2 position)
    {
        for (int i = movements.Count - 1; i >= 0; i--)
        {
            ChartMovement m = movements[i];
            if (GetMovementRect(m.beat, m.length).Contains(position)) return m;
        }
        return null;
    }

    // ===== [ROTATE - Langkah 6a & 6b] Gambar track Movement =====
    private void DrawMovements()
    {
        // Background track
        EditorGUI.DrawRect(new Rect(0, RulerHeight, timelineWidth, MovementTrackHeight), new Color(0.15f, 0.15f, 0.19f, 1f));
        EditorGUI.DrawRect(new Rect(0, RulerHeight, timelineWidth, 2f), gridColor);

        for (int i = 0; i < movements.Count; i++)
        {
            ChartMovement m = movements[i];
            Rect r = GetMovementRect(m.beat, m.length);
            if (r.xMax < visibleMinX || r.xMin > visibleMaxX) continue;

            // Searah = ungu, berlawanan = oranye
            Color c = m.degrees >= 0f ? RotateColor : RotateColorCCW;
            c.a = 0.85f;
            EditorGUI.DrawRect(r, c);
            if (r.width > 90f)
            {
                GUI.Label(r, "Rotate " + m.degrees.ToString("0.##", Inv) + "° / " + m.length.ToString("0.##", Inv) + "b", EditorStyles.miniLabel);
            }
        }

        if (isPlacingMovement)
        {
            Rect preview = GetMovementRect(movementStartBeat, movementPreviewLength);
            Color c = rotateDegrees >= 0f ? RotateColor : RotateColorCCW;
            c.a = 0.5f;
            EditorGUI.DrawRect(preview, c);
        }

        GUI.Label(new Rect(scrollPosition.x + 4f, RulerHeight + 4f, 90f, 18f), "Movement", mutedLabel);
    }

    // ===== [ROTATE - Langkah 6c] Hitung derajat putaran pada beat sekarang =====
    private float GetPreviewRotation()
    {
        float beat = CurrentBeat;
        float total = 0f;

        for (int i = 0; i < movements.Count; i++)
        {
            ChartMovement m = movements[i];
            if (m.type != ChartMovementType.Rotate || m.length <= 0f) continue;

            if (beat >= m.beat + m.length)
            {
                total += m.degrees;                                                // blok sudah selesai
            }
            else if (beat > m.beat)
            {
                total += Mathf.Clamp01((beat - m.beat) / m.length) * m.degrees;    // blok sedang berjalan
                break;
            }
            else
            {
                break; // blok berikutnya belum mulai (daftar sudah terurut)
            }
        }

        return total;
    }

    // Memutar sebuah vektor sebesar 'degrees'. Positif = searah jarum jam di layar
    // (sumbu Y GUI Unity mengarah ke bawah).
    private static Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private float SnapBeat(float localX)
    {
        float beat = localX / pixelsPerSecond / SecondsPerBeat;
        beat = Mathf.Round(beat * snapDivision) / snapDivision;
        return Mathf.Clamp(beat, 0f, TotalBeats);
    }

    // [ROTATE - Langkah 9] pakai LaneAreaTop
    private int LaneFromY(float y)
    {
        if (y < LaneAreaTop || y > timelineHeight - FooterHeight) return -1;
        int lane = Mathf.FloorToInt((y - LaneAreaTop) / LaneHeight) + 1;
        return (lane >= 1 && lane <= LaneCount) ? lane : -1;
    }

    // [ROTATE - Langkah 9] pakai LaneAreaTop
    private Rect GetNoteRect(float beat, int lane, ChartNoteType type, float length)
    {
        float laneHeight = LaneHeight;
        float x = beat * SecondsPerBeat * pixelsPerSecond;
        float y = LaneAreaTop + (lane - 1) * laneHeight + 3f;
        float headWidth = TimelineNoteHeadWidth;
        float width = headWidth;

        if (type == ChartNoteType.Hold)
        {
            width += length * SecondsPerBeat * pixelsPerSecond;
        }

        return new Rect(x - headWidth * 0.5f, y, width, laneHeight - 6f);
    }

    private ChartNote HitTestNote(Vector2 position)
    {
        int lane = LaneFromY(position.y);
        if (lane < 0) return null;

        for (int i = notes.Count - 1; i >= 0; i--)
        {
            ChartNote n = notes[i];
            if (n.lane != lane) continue;

            Rect r = GetNoteRect(n.beat, n.lane, n.type, n.length);
            if (position.x >= r.xMin && position.x <= r.xMax)
            {
                return n;
            }
        }
        return null;
    }

    private void DrawNotes()
    {
        int laneCount = LaneCount;
        float secondsPerBeat = SecondsPerBeat;
        float pixelsPerBeat = secondsPerBeat * pixelsPerSecond;
        Event e = Event.current;

        for (int i = 0; i < notes.Count; i++)
        {
            ChartNote n = notes[i];
            if (n.lane > laneCount) continue;

            Rect r = GetNoteRect(n.beat, n.lane, n.type, n.length);
            if (r.xMax < visibleMinX || r.xMin > visibleMaxX) continue;

            DrawNoteRect(r, n.type, 1f);

            if (n.type == ChartNoteType.Hold)
            {
                float handleWidth = Mathf.Min(6f, r.width * 0.3f);
                Rect handle = new Rect(r.xMax - handleWidth, r.y, handleWidth, r.height);
                EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeHorizontal);

                if (e.type == EventType.MouseDown && e.button == 0 && handle.Contains(e.mousePosition))
                {
                    resizingIndex = i;
                    e.Use();
                }
            }
        }

        if (resizingIndex >= 0 && e.type == EventType.MouseDrag)
        {
            ChartNote n = notes[resizingIndex];
            float mouseBeat = e.mousePosition.x / pixelsPerBeat;
            float rawLength = mouseBeat - n.beat;
            float newLength = Mathf.Max(SnapBeats, Mathf.Round(rawLength / SnapBeats) * SnapBeats);

            if (CanPlaceNote(n.lane, n.beat, n.beat + newLength, ChartNoteType.Hold, ignoreNote: n))
            {
                n.length = newLength;
                notes[resizingIndex] = n;
            }

            e.Use();
            Repaint();
        }

        if (e.type == EventType.MouseUp)
            resizingIndex = -1;

        if (isPlacingHold)
        {
            Rect preview = GetNoteRect(holdStartBeat, holdLane, ChartNoteType.Hold, holdPreviewLength);
            DrawNoteRect(preview, ChartNoteType.Hold, 0.55f);
        }
    }

    private void DrawNoteRect(Rect r, ChartNoteType type, float alpha)
    {
        if (type == ChartNoteType.Catch)
        {
            float catchWidth = TimelineNoteHeadWidth * 0.75f;
            float catchHeight = r.height * 0.55f;
            Rect catchRect = new Rect(
                r.x + (r.width - catchWidth) * 0.5f,
                r.y + (r.height - catchHeight) * 0.5f,
                catchWidth,
                catchHeight
            );
            Color head = CatchColor;
            head.a = alpha;
            EditorGUI.DrawRect(catchRect, head);
            return;
        }

        Color baseColor = type == ChartNoteType.Tap ? TapColor : HoldColor;

        if (type == ChartNoteType.Hold)
        {
            Color body = baseColor;
            body.a = 0.55f * alpha;
            EditorGUI.DrawRect(r, body);
        }

        Color headColor = baseColor;
        headColor.a = alpha;
        EditorGUI.DrawRect(new Rect(r.x, r.y, Mathf.Min(TimelineNoteHeadWidth, r.width), r.height), headColor);
    }

    private void DrawTimeline()
    {
        EditorGUILayout.LabelField("Timeline View", headerBoxStyle);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Zoom:", GUILayout.Width(50));
        pixelsPerSecond = EditorGUILayout.Slider(pixelsPerSecond, 10f, 600f);
        EditorGUILayout.EndHorizontal();

        timelineWidth = duration * pixelsPerSecond;

        Rect scrollViewRect = GUILayoutUtility.GetRect(Mathf.Max(280f, editorContentWidth - 20f), timelineHeight + 15);
        if (Event.current.type == EventType.Repaint)
        {
            timelineViewWidth = scrollViewRect.width;
        }

        scrollPosition = GUI.BeginScrollView(
            scrollViewRect,
            scrollPosition,
            new Rect(0, 0, timelineWidth, timelineHeight),
            true, false);

        visibleMinX = scrollPosition.x;
        visibleMaxX = scrollPosition.x + scrollViewRect.width;

        EditorGUI.DrawRect(new Rect(0, 0, timelineWidth, timelineHeight), new Color(0.2f, 0.2f, 0.2f, 1f));
        EditorGUI.DrawRect(new Rect(0, 0, timelineWidth, RulerHeight), new Color(0.17f, 0.17f, 0.17f, 1f));

        DrawGridLines();
        DrawMovements();   // [ROTATE - Langkah 7] baru
        DrawNotes();
        DrawPlayhead();

        HandleTimelineInput(scrollViewRect);

        GUI.EndScrollView();
    }

    private void DrawGridLines()
    {
        float secondsPerBeat = SecondsPerBeat;
        float totalBeats = TotalBeats;

        int majorStep = -1;
        if (snapDivision % 3 == 0) majorStep = 3;
        else if (snapDivision % 4 == 0) majorStep = 4;
        else if (snapDivision % 5 == 0) majorStep = 5;
        else if (snapDivision % 7 == 0) majorStep = 7;

        for (int beat = 0; beat <= Mathf.CeilToInt(totalBeats); beat++)
        {
            float timeInSeconds = beat * secondsPerBeat;
            float xPos = timeInSeconds * pixelsPerSecond;

            bool isMeasureLine = (beat % beatsPerMeasure == 0);
            Color lineColor = isMeasureLine ? measureLineColor : gridColor;
            float lineWidth = isMeasureLine ? 2.5f : 1.5f;

            Rect lineRect = new Rect(xPos, 0, lineWidth, timelineHeight);
            EditorGUI.DrawRect(lineRect, lineColor);

            if (isMeasureLine)
            {
                Rect labelRect = new Rect(xPos + 3, 4, 50, 18);
                GUI.Label(labelRect, $"B{beat}", EditorStyles.miniLabel);
            }

            for (int sub = 1; sub < snapDivision; sub++)
            {
                float subBeatTime = (beat + ((float)sub / snapDivision)) * secondsPerBeat;
                float subXPos = subBeatTime * pixelsPerSecond;

                bool isMajorSub = (majorStep > 0 && sub % majorStep == 0);
                Color subColor = isMajorSub
                    ? new Color(0.85f, 0.85f, 0.85f, 0.95f)
                    : new Color(0.6f, 0.6f, 0.6f, 0.65f);

                float subLineWidth = isMajorSub ? 2f : 1.2f;
                Rect subRect = new Rect(subXPos, 0, subLineWidth, timelineHeight);
                EditorGUI.DrawRect(subRect, subColor);
            }
        }

        for (int sec = 0; sec <= Mathf.CeilToInt(duration); sec++)
        {
            float xPos = sec * pixelsPerSecond;
            Rect labelRect = new Rect(xPos + 3, timelineHeight - FooterHeight, 50, 18);
            GUI.Label(labelRect, $"{sec}s", EditorStyles.miniLabel);
        }

        int laneCount = LaneCount;
        float laneHeight = LaneHeight;

        for (int gridLane = 0; gridLane <= laneCount; gridLane++)
        {
            float lineThickness = 2f;
            float yPos = LaneAreaTop + laneHeight * gridLane;   // [ROTATE - Langkah 7] dulu RulerHeight

            Rect laneRect = new Rect(0, yPos, timelineWidth, lineThickness);
            EditorGUI.DrawRect(laneRect, gridColor);
        }
    }

    private void DrawPlayhead()
    {
        float xPos = currentTime * pixelsPerSecond;

        Rect playheadRect = new Rect(xPos - 1, 0, 3, timelineHeight);
        EditorGUI.DrawRect(playheadRect, playheadColor);

        Handles.BeginGUI();
        Handles.color = playheadColor;
        Vector3[] trianglePoints = new Vector3[3]
        {
            new Vector3(xPos, 0),
            new Vector3(xPos - 8, 15),
            new Vector3(xPos + 8, 15)
        };
        Handles.DrawAAConvexPolygon(trianglePoints);
        Handles.EndGUI();
    }

    private bool isScrubbing = false;

    private void HandleTimelineInput(Rect scrollViewRect)
    {
        Event e = Event.current;

        if (e.type == EventType.ScrollWheel)
        {
            if (e.control || e.command || e.alt)
            {
                // Zoom
                float zoomDelta = e.delta.y != 0 ? e.delta.y : e.delta.x;
                pixelsPerSecond = Mathf.Clamp(pixelsPerSecond - zoomDelta * 2.5f, 10f, 600f);
            }
            else
            {
                // Scroll left/right
                float scrollDelta = e.delta.y != 0 ? e.delta.y : e.delta.x;
                scrollPosition.x = Mathf.Clamp(scrollPosition.x + scrollDelta * 25f, 0f, Mathf.Max(0f, timelineWidth - timelineViewWidth));
            }

            e.Use();
            Repaint();
            return;
        }

        // ===== [ROTATE - Langkah 8a] Area klik =====
        Rect rulerRect = new Rect(0, 0, timelineWidth, RulerHeight);
        Rect movementAreaRect = new Rect(0, RulerHeight, timelineWidth, MovementTrackHeight);
        Rect fullTrackRect = new Rect(0, RulerHeight, timelineWidth, timelineHeight - RulerHeight - FooterHeight);
        Rect laneAreaRect = new Rect(0, LaneAreaTop, timelineWidth, timelineHeight - LaneAreaTop - FooterHeight);

        if (isPlacingHold && laneAreaRect.Contains(e.mousePosition))
        {
            float mouseBeat = SnapBeat(e.mousePosition.x);
            if (mouseBeat > holdStartBeat)
            {
                float desiredLength = mouseBeat - holdStartBeat;
                if (CanPlaceNote(holdLane, holdStartBeat, holdStartBeat + desiredLength, ChartNoteType.Hold))
                {
                    holdPreviewLength = desiredLength;
                }
            }
            else
            {
                holdPreviewLength = SnapBeats;
            }
        }

        // [ROTATE - Langkah 8a] Preview panjang blok mengikuti mouse
        if (isPlacingMovement && fullTrackRect.Contains(e.mousePosition))
        {
            float mouseBeat = SnapBeat(e.mousePosition.x);
            if (mouseBeat > movementStartBeat)
            {
                float desiredLength = mouseBeat - movementStartBeat;
                if (CanPlaceMovement(selectedMovementType, movementStartBeat, movementStartBeat + desiredLength))
                {
                    movementPreviewLength = desiredLength;
                }
            }
            else
            {
                movementPreviewLength = SnapBeats;
            }
        }

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0 && rulerRect.Contains(e.mousePosition))
                {
                    isScrubbing = true;
                    SetTimeFromMouse(e.mousePosition.x);
                    e.Use();
                }
                // ===== [ROTATE - Langkah 8b] Klik kiri di track Movement =====
                else if (e.button == 0 && movementToolActive && fullTrackRect.Contains(e.mousePosition))
                {
                    if (isPlacingMovement)
                    {
                        float targetBeat = SnapBeat(e.mousePosition.x);
                        if (targetBeat > movementStartBeat)
                        {
                            AddMovement(selectedMovementType, movementStartBeat, targetBeat - movementStartBeat, rotateDegrees);
                        }
                        isPlacingMovement = false;
                    }
                    else if (movementAreaRect.Contains(e.mousePosition))
                    {
                        BeginPlaceMovement(e.mousePosition.x);
                    }
                    e.Use();
                }
                // ===== [ROTATE - Langkah 8c] Klik kanan menghapus blok =====
                else if (e.button == 1 && movementAreaRect.Contains(e.mousePosition))
                {
                    if (isPlacingMovement)
                    {
                        isPlacingMovement = false;
                    }
                    else
                    {
                        ChartMovement hitMove = HitTestMovement(e.mousePosition);
                        if (hitMove != null)
                        {
                            movements.Remove(hitMove);
                            Repaint();
                        }
                    }
                    e.Use();
                }
                else if (e.button == 0 && laneAreaRect.Contains(e.mousePosition))
                {
                    if (isPlacingHold)
                    {
                        float targetBeat = SnapBeat(e.mousePosition.x);
                        if (targetBeat > holdStartBeat)
                        {
                            float finalLength = targetBeat - holdStartBeat;
                            AddNote(holdLane, holdStartBeat, ChartNoteType.Hold, finalLength);
                        }
                        isPlacingHold = false;
                        e.Use();
                    }
                    else
                    {
                        BeginPlaceNote(e.mousePosition);
                        e.Use();
                    }
                }
                else if (e.button == 1 && laneAreaRect.Contains(e.mousePosition))
                {
                    if (isPlacingHold)
                    {
                        isPlacingHold = false;
                        e.Use();
                    }
                    else
                    {
                        ChartNote hit = HitTestNote(e.mousePosition);
                        if (hit != null)
                        {
                            notes.Remove(hit);
                            keyframeViewDirty = true;
                            Repaint();
                        }
                        e.Use();
                    }
                }
                break;

            case EventType.MouseDrag:
                if (isScrubbing)
                {
                    SetTimeFromMouse(e.mousePosition.x);
                    e.Use();
                }
                else if (e.button == 2)
                {
                    scrollPosition.x -= e.delta.x;
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (isScrubbing)
                {
                    isScrubbing = false;
                    e.Use();
                }
                break;

            case EventType.MouseLeaveWindow:
                isScrubbing = false;
                break;
        }
    }

    private void DrawLanePreviewSidebar()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(LanePreviewSidebarWidth), GUILayout.ExpandHeight(true));

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Lane Preview", headerBoxStyle);
        if (GUILayout.Button(showLanePreview ? "Hide" : "Show", EditorStyles.toolbarButton, GUILayout.Width(48)))
        {
            showLanePreview = !showLanePreview;
        }
        EditorGUILayout.EndHorizontal();

        if (showLanePreview)
        {
            EditorGUILayout.LabelField("Beat " + CurrentBeat.ToString("0.00", Inv), beatStyle);
            EditorGUILayout.LabelField(isPlaying ? "Playing chart preview" : "Play or scrub to preview notes", mutedLabel);
            EditorGUILayout.LabelField("Approach (beats)", mutedLabel);
            previewLeadBeats = EditorGUILayout.Slider(previewLeadBeats, 0.5f, 8f);

            Rect previewRect = GUILayoutUtility.GetRect(0f, 146f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawLanePreviewContents(previewRect);
        }
        else
        {
            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawLanePreviewContents(Rect previewRect)
    {
        EditorGUI.DrawRect(previewRect, new Color(0.12f, 0.13f, 0.16f, 1f));

        // [ROTATE - Langkah 9] arena diperkecil agar ujung lane tidak terpotong saat berputar
        float arenaSize = Mathf.Max(120f, Mathf.Min(previewRect.width - 48f, previewRect.height - 62f));
        float armLength = arenaSize * 0.50f;
        float laneThickness = Mathf.Clamp(arenaSize * 0.14f, 22f, 36f);

        Vector2 center = previewRect.center;
        float missBeats = 1f;

        // [ROTATE - Langkah 9] derajat putaran pada beat sekarang.
        // Karena lane digambar lewat Handles (bukan GUI biasa), arah lane diputar
        // langsung dengan RotateVector, bukan dengan GUI.matrix.
        float rotation = GetPreviewRotation();

        for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
        {
            Vector2 direction = RotateVector(GetPreviewLaneDirection(laneIndex), rotation);
            Vector2 outerPoint = center - direction * armLength;

            Color laneColor = laneIndex < 4
                ? new Color(0.31f, 0.33f, 0.38f, 1f)
                : new Color(0.27f, 0.31f, 0.40f, 0.85f);

            DrawLanePreviewQuad(outerPoint, center, laneThickness - 1f, laneColor);

            Vector2 labelPoint = outerPoint - direction * 12f;
            GUI.Label(new Rect(labelPoint.x - 20f, labelPoint.y - 8f, 40f, 16f), "L" + (laneIndex + 1), rowCenterStyle);
        }

        float targetSize = Mathf.Max(18f, laneThickness * 0.7f);
        EditorGUI.DrawRect(
            new Rect(center.x - targetSize * 0.5f, center.y - targetSize * 0.5f, targetSize, targetSize),
            new Color(1f, 0.34f, 0.35f, 1f));

        EditorGUI.DrawRect(
            new Rect(center.x - 4f, center.y - 4f, 8f, 8f),
            new Color(0.12f, 0.13f, 0.16f, 1f));

        float currentBeat = CurrentBeat;
        float safeLeadBeats = Mathf.Max(0.01f, previewLeadBeats);

        for (int i = 0; i < notes.Count; i++)
        {
            ChartNote note = notes[i];
            if (note == null || note.lane < 1 || note.lane > LaneCount) continue;

            bool isHold = note.type == ChartNoteType.Hold;
            float endBeat = isHold ? note.beat + Mathf.Max(0f, note.length) : note.beat;

            bool inPreviewRange = isHold
                ? note.beat <= currentBeat + safeLeadBeats && endBeat >= currentBeat - missBeats
                : note.beat >= currentBeat - missBeats && note.beat <= currentBeat + safeLeadBeats;

            if (!inPreviewRange) continue;

            int laneIndex = note.lane - 1;
            // [ROTATE - Langkah 9] note memakai arah yang sudah diputar, jadi ikut berputar bersama lane
            Vector2 direction = RotateVector(GetPreviewLaneDirection(laneIndex), rotation);
            Vector2 outerPoint = center - direction * armLength;

            float startProgress = Mathf.Clamp01(1f - (note.beat - currentBeat) / safeLeadBeats);
            Vector2 notePoint = Vector2.Lerp(outerPoint, center, startProgress);

            float fadeBeat = isHold ? endBeat : note.beat;
            float alpha = fadeBeat < currentBeat
                ? Mathf.Clamp01(1f - (currentBeat - fadeBeat) / missBeats)
                : 1f;

            Color noteColor = note.type == ChartNoteType.Tap ? TapColor : (note.type == ChartNoteType.Hold ? HoldColor : CatchColor);

            if (isHold)
            {
                float endProgress = Mathf.Clamp01(1f - (endBeat - currentBeat) / safeLeadBeats);
                Vector2 holdEndPoint = Vector2.Lerp(outerPoint, center, endProgress);

                Color bodyColor = noteColor;
                bodyColor.a *= 0.58f * alpha;
                DrawLanePreviewQuad(notePoint, holdEndPoint, Mathf.Max(3f, laneThickness * 0.30f), bodyColor);
            }

            noteColor.a *= alpha;
            float markerLength = note.type == ChartNoteType.Catch ? 6f : 9f;
            float markerWidth = Mathf.Max(7f, laneThickness * 0.72f);

            if (note.type == ChartNoteType.Catch)
            {
                markerWidth *= 0.7f;
            }

            DrawLanePreviewQuad(
                notePoint - direction * (markerLength * 0.5f),
                notePoint + direction * (markerLength * 0.5f),
                markerWidth,
                noteColor);
        }

        GUI.Label(
            new Rect(previewRect.x + 10f, previewRect.yMax - 19f, previewRect.width - 20f, 14f),
            LaneCount > 4 ? "L1 kiri | L2 atas | L3 kanan | L4 bawah | L5 kiri-atas | L6 kanan-atas | L7 kiri-bawah | L8 kanan" : "L1 kiri | L2 atas | L3 kanan | L4 bawah", mutedLabel);
    }

    private Vector2 GetPreviewLaneDirection(int laneIndex)
    {
        switch (laneIndex)
        {
            case 0: return new Vector2(0, 1f);
            case 1: return new Vector2(-1f, 0);
            case 2: return new Vector2(0, -1f);
            case 3: return new Vector2(1f, 0);

            case 4: return new Vector2(-1f, 1f).normalized;
            case 5: return new Vector2(-1f, -1f).normalized;
            case 6: return new Vector2(1f, -1f).normalized;
            case 7: return new Vector2(1f, 1f).normalized;

            default: return Vector2.down;
        }
    }

    private void DrawLanePreviewQuad(Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        dir.Normalize();

        Vector2 half = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        Vector3[] pts =
        {
            a + half,
            b + half,
            b - half,
            a - half
        };

        Handles.BeginGUI();

        Color prev = Handles.color;

        // Use the passed-in color parameter instead of hardcoded gray
        Handles.color = color;

        Handles.DrawAAConvexPolygon(pts);

        Handles.color = prev;

        Handles.EndGUI();
    }

    private void BeginPlaceNote(Vector2 mousePosition)
    {
        int lane = LaneFromY(mousePosition.y);
        if (lane < 0) return;

        float beat = SnapBeat(mousePosition.x);

        if (selectedNoteType == ChartNoteType.Tap)
        {
            AddNote(lane, beat, ChartNoteType.Tap, 0f);
        }
        else if (selectedNoteType == ChartNoteType.Catch)
        {
            AddNote(lane, beat, ChartNoteType.Catch, 0f);
        }
        else if (selectedNoteType == ChartNoteType.Hold)
        {
            if (CanPlaceNote(lane, beat, beat + SnapBeats, ChartNoteType.Hold))
            {
                isPlacingHold = true;
                holdLane = lane;
                holdStartBeat = beat;
                holdPreviewLength = SnapBeats;
            }
        }
    }

    private void SetTimeFromMouse(float localX)
    {
        SeekTo(localX / pixelsPerSecond);
    }

    private void SeekTo(float time)
    {
        currentTime = Mathf.Clamp(time, 0f, duration);

        if (isPlaying)
        {
            BeginPlayback();
        }

        Repaint();
    }

    private void PlayAudio()
    {
        if (currentTime >= duration - 0.001f)
        {
            currentTime = 0f;
        }

        BeginPlayback();
    }

    private void BeginPlayback()
    {
        StopPreviewClips();

        if (audioClip != null)
        {
            if (playPreviewClipMethod != null)
            {
                int startSample = Mathf.Clamp((int)(currentTime * audioClip.frequency), 0, Mathf.Max(0, audioClip.samples - 1));
                playPreviewClipMethod.Invoke(null, new object[] { audioClip, startSample, false });
            }
        }

        audioStartTime = EditorApplication.timeSinceStartup - currentTime;
        isPlaying = true;
    }

    private void StopPreviewClips()
    {
        if (stopAllPreviewClipsMethod != null)
        {
            stopAllPreviewClipsMethod.Invoke(null, null);
        }
    }

    private void PauseAudio()
    {
        StopPreviewClips();
        isPlaying = false;
    }

    private void StopAudio()
    {
        PauseAudio();
        currentTime = 0f;
    }

    private static string FormatBeat(float beat)
    {
        return "B" + beat.ToString("0.00", Inv);
    }

    private static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float rest = seconds - minutes * 60f;
        return minutes + ":" + rest.ToString("00.0", Inv);
    }

    private void NewProject()
    {
        StopAudio();

        projectName = "New Chart";
        artistName = "";
        bpm = 120f;
        duration = 60f;
        currentTime = 0f;
        projectFilePath = "";
        audioClip = null;

        notes.Clear();
        movements.Clear();            // [ROTATE - Langkah 10]
        isPlacingHold = false;
        isPlacingMovement = false;    // [ROTATE - Langkah 10]
        movementToolActive = false;   // [ROTATE - Langkah 10]
        keyframeViewDirty = true;
        keyframeScroll = Vector2.zero;

        Debug.Log("New project created");
        Repaint();
    }

    private void LoadProject()
    {
        string path = EditorUtility.OpenFilePanel("Load Chart Project", Application.dataPath, "json");

        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                StopAudio();

                string jsonContent = File.ReadAllText(path);
                ChartProjectData data = JsonUtility.FromJson<ChartProjectData>(jsonContent);

                projectName = data.projectName;
                artistName = data.artistName;
                bpm = data.bpm;
                duration = data.duration;
                currentTime = 0f;
                projectFilePath = path;

                if (data.laneCount > 0)
                {
                    laneLength = data.laneCount;
                }

                notes = data.notes != null ? new List<ChartNote>(data.notes) : new List<ChartNote>();
                isPlacingHold = false;
                keyframeScroll = Vector2.zero;
                SortNotes();

                // ===== [ROTATE - Langkah 10] Load movements =====
                movements = data.movements != null ? new List<ChartMovement>(data.movements) : new List<ChartMovement>();
                isPlacingMovement = false;
                SortMovements();

                // Kompatibilitas chart lama: movement tanpa degrees dianggap satu putaran penuh
                for (int i = 0; i < movements.Count; i++)
                {
                    if (Mathf.Approximately(movements[i].degrees, 0f)) movements[i].degrees = 360f;
                }

                if (!string.IsNullOrEmpty(data.audioClipPath))
                {
                    audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(data.audioClipPath);
                    if (audioClip != null)
                    {
                        duration = audioClip.length;
                    }
                }
                else
                {
                    audioClip = null;
                }

                Debug.Log($"Project loaded: {projectName}");
                Repaint();
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Load Error", $"Failed to load project:\n{e.Message}", "OK");
            }
        }
    }

    private void SaveProject()
    {
        string path = projectFilePath;

        if (string.IsNullOrEmpty(path))
        {
            path = EditorUtility.SaveFilePanel("Save Chart Project", Application.dataPath, projectName, "json");
        }

        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                ChartProjectData data = new ChartProjectData
                {
                    projectName = projectName,
                    artistName = artistName,
                    bpm = bpm,
                    duration = duration,
                    audioClipPath = audioClip != null ? AssetDatabase.GetAssetPath(audioClip) : "",
                    laneCount = LaneCount,
                    notes = new List<ChartNote>(notes),
                    movements = new List<ChartMovement>(movements)   // [ROTATE - Langkah 10]
                };

                string jsonContent = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, jsonContent);
                projectFilePath = path;

                AssetDatabase.Refresh();

                Debug.Log($"Project saved: {path}");
                EditorUtility.DisplayDialog("Save Successful", $"Project saved to:\n{path}", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Save Error", $"Failed to save project:\n{e.Message}", "OK");
            }
        }
    }
}

[System.Serializable]
public class ChartProjectData
{
    public string projectName;
    public string artistName;
    public float bpm;
    public float duration;
    public string audioClipPath;
    public int laneCount;
    public List<ChartNote> notes = new List<ChartNote>();
    public List<ChartMovement> movements = new List<ChartMovement>();   // [ROTATE - Langkah 1]
}