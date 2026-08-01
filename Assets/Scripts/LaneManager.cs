using System.Collections.Generic;
using System.IO.IsolatedStorage;
using UnityEngine;
using UnityEngine.InputSystem;

public class LaneManager : MonoBehaviour
{
    [Header("Chart Configuration")]
    [Tooltip("ScriptableObject yang berisi data chart lagu")]
    public SongChartSO currentChart;

    [Header("Lane Setup")]
    [Tooltip("Drag semua LaneController objects ke sini")]
    public List<LaneController> laneControllers = new List<LaneController>();

    [Header("Recording Mode (Read Only)")]
    [Tooltip("Status recording mode - diubah otomatis oleh system")]
    public bool isRecordingMode = false;

    private List<ChartRecorder> recorders = new List<ChartRecorder>();

    private void Start()
    {
        if (currentChart != null) LoadChart();
        else Debug.LogWarning("LaneManager: Tidak ada chart yang diassign");
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current[Key.R].wasPressedThisFrame)
        {
            if (!isRecordingMode) StartRecording();
            else StopRecording();
        }
        if (isRecordingMode && Keyboard.current[Key.C].wasPressedThisFrame) ClearAllRecordings();
    }

    public void LoadChart()
    {
        if (currentChart == null)
        {
            Debug.LogWarning("LaneManager: Tidak ada chart yang di-assign");
            return;
        }
        if (laneControllers.Count != currentChart.lanes.Count)
        {
            Debug.LogWarning($"LaneManager: Jumlah lane tidak sesuai " + $"Controllers: {laneControllers.Count}, Chart lanes {currentChart.lanes.Count}");
        }

        int laneCount = Mathf.Min(laneControllers.Count, currentChart.lanes.Count);

        for (int i = 0; i < laneCount; i++)
        {
            LaneController controller = laneControllers[i];
            LaneData laneData = currentChart.lanes[i];

            controller.inputKey = laneData.inputKey;
            controller.noteTimestamps = new List<float> (laneData.recordedBeats);
            controller.holdDurations = new List<float> (laneData.holdDurations);

            Debug.Log($"LaneManager: Loaded {laneData.laneName} - {laneData.recordedBeats.Count} notes");
        }

        Debug.Log($"LaneManager: Chart '{currentChart.difficulty()}' berhasil di-load");
    }

    public void StartRecording()
    {
        if (currentChart == null)
        {
            Debug.LogError("LaneManager: Tidak ada chart untuk recording");
            return;
        }

        foreach (var recorder in recorders)
        {
            if (recorder == null) Destroy(recorder);
        }
        recorders.Clear();

        foreach (var controller in laneControllers)
        {
            controller.enabled = false;
        }

        for (int i = 0; i < laneControllers.Count && i < currentChart.lanes.Count; i++)
        {
            ChartRecorder recorder = laneControllers[i].gameObject.AddComponent<ChartRecorder>();
            recorder.inputKey = currentChart.lanes[i].inputKey;
            recorder.targetLaneData = currentChart.lanes[i];
            recorders.Add(recorder);
        }

        isRecordingMode = true;

        Debug.Log("Recording...");
        Debug.Log("Tekan tombol sesuai beat musik.");
        Debug.Log("Tekan R lagi untuk stop recording.");
        Debug.Log("Tekan C lagi untuk clear recording.");
    }

    public void StopRecording()
    {
        if(!isRecordingMode)
        {
            Debug.LogWarning("LaneManager: Tidak dalam recording mode");
            return;
        }

        foreach (var recorder in recorders)
        {
            if (recorder != null) Destroy(recorder);
        }
        recorders.Clear();

        foreach (var controller in laneControllers)
        {
            controller.enabled = true;
        }

        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(currentChart);
        UnityEditor.AssetDatabase.SaveAssets();
        #endif

        isRecordingMode = false;

        Debug.Log("Recording Finished!");
        Debug.Log($"Chart '{currentChart.difficulty()}' berhasil disimpan..");

        foreach(var lane in currentChart.lanes)
        {
            Debug.Log($" -{lane.laneName}:{lane.recordedBeats.Count} beats");
        }
    }

    public void ClearAllRecordings()
    {
        if (!isRecordingMode)
        {
            Debug.LogWarning("LaneManager: Clear hanya bisa dilakukan saat recording mode");
            return;
        }

        foreach (var lane in currentChart.lanes)
        {
            lane.recordedBeats.Clear();
        }

        Debug.Log("LaneManager Semua recording dihapus!");
    }

    public void AddLaneController(LaneController controller)
    {
        if (!laneControllers.Contains(controller))
        {
            laneControllers.Add(controller);
            Debug.Log("LaneManager: Lane controller ditambahkan.");
        }
    }

    public void RemoveLaneController(LaneController controller)
    {
        if (laneControllers.Remove(controller))
        {
            Debug.Log("LaneManager: Lane controller dihapus.");
        }
    }
}
