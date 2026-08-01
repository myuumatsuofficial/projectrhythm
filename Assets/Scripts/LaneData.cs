using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling.LowLevel.Unsafe;

[System.Serializable]
public class LaneData
{
    [Header("Lane Configuration")]
    [Tooltip("Nama lane untuk identifikasi (contoh: Lane 1)")]
    public string laneName;

    [Tooltip("Tombol keyboard untuk lane ini")]
    public UnityEngine.InputSystem.Key inputKey;

    [Header("Chart Data")]
    [Tooltip("List waktu beat (dalam satuan beat) untuk spawn note")]
    public List <float> recordedBeats = new List<float>();

    [Tooltip("Durasi hold per not (dalam detik), 0 = note biasa")]
    public List <float> holdDurations = new List<float>();

    public LaneData(string name, UnityEngine.InputSystem.Key key)
    {
        laneName = name;
        inputKey = key;
        recordedBeats = new List<float>();
        holdDurations = new List<float>();
    }
}
