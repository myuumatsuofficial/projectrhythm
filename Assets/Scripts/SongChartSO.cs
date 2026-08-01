using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Overlays;
using UnityEngine;

[CreateAssetMenu(fileName = "New Song Chart", menuName = "Rhythm Game/Song Chart")]

public class SongChartSO : ScriptableObject
{
    [Header("Song Reference")]
    [Tooltip("Reference ke Song Data (metadata lagu")]
    public SongDataSO songData;

    [Header("Chart Data")]
    [Tooltip("List semua lane dalam chart ini")]
    public List<LaneData> lanes = new List<LaneData>();

    [Tooltip("Difficulty (Contoh: Easy, Normal, Hard")]
    public string difficulty ()
    {
        string diffName = string.Empty;
        if (diffNumber() >= 1 && diffNumber() <= 4) diffName = "Easy";
        else if (diffNumber() >= 5 && diffNumber() <= 7) diffName = "Normal";
        else diffName = "Hard";
        return diffName;
    }

    [Tooltip("Tingkat kesuitan (1-10)")]
    public int diffNumber()
    {
        int diff = 1;
        if (diff < 1) diff = 1;
        if (diff > 10) diff = 10;
        return diff;
    }

    public LaneData GetLane(int index)
    {
        if (index >= 0 && index < lanes.Count) return lanes[index];

        Debug.LogWarning($"SongChartSO: Index {index} tidak valid!");
        return null;
    }

    public void AddLane(LaneData laneData)
    {
        lanes.Add(laneData);
        Debug.Log($"SongChartSO: Lane '{laneData.laneName}' ditambahkan.");
    }

    public void RemoveLane(int index)
    {
        if (index >= 0 && index < lanes.Count)
        {
            string laneName = lanes[index].laneName;
            lanes.RemoveAt(index);
            Debug.Log($"SongChartSO: Lane '{laneName}' dihapus");
        }
        else Debug.LogWarning($"SongChartSO: Index {index} tidak valid!");
    }
}
