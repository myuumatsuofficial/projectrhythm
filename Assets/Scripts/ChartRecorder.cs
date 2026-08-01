using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class ChartRecorder : MonoBehaviour
{
    [Header("Input Configuration")]
    [Tooltip("Tombol keyboard yang akan direkam")]
    public Key inputKey;

    [Header("Target Lane Data")]
    [Tooltip("Reference ke LaneData di Scriptable Object")]
    public LaneData targetLaneData;

    [Header("Settings")]
    [Tooltip("Tampilkan log di Console saat merekam beat")]
    public bool showDebugLog = true;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[inputKey].wasPressedThisFrame)
        {
            RecordBeat();
        }
    }

    public void RecordBeat()
    {
        if (targetLaneData == null)
        {
            Debug.LogError("ChartRecorder: Target LaneData tidak ada!");
            return;
        }
        float currentBeat = Conductor.Instance.songPositionInBeats;

        targetLaneData.recordedBeats.Add(currentBeat);

        if (showDebugLog)
        {
            Debug.Log($"ChartRecorder [{inputKey}]: Beat {currentBeat:F2} direkam ke '{targetLaneData.laneName}' " + $"(Total {targetLaneData.recordedBeats.Count})");
        }
    }
    
    public void ClearRecording()
    {
        if (targetLaneData != null)
        {
            int count = targetLaneData.recordedBeats.Count;
            targetLaneData.recordedBeats.Clear();

            Debug.Log($"ChartRecorder [{inputKey}]: {count} beats dihapus dari '{targetLaneData.laneName}'");
        }
    }
}
