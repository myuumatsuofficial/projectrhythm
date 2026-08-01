using NUnit.Framework;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class JsonLoader : MonoBehaviour
{
    public SongChartSO chart;
    public TextAsset jsonFile;

    [TextArea(3, 10)]
    public string jsonText;
    public ProjectData myProjectData;

    void Start()
    {
        if (jsonFile == null)
        {
            Debug.LogWarning("JSON file is empty or not assigned.");
            myProjectData = null;
            return;
        }
        string jsonString = jsonFile.text;

        if (!string.IsNullOrEmpty(jsonString))
        { 
            myProjectData = JsonUtility.FromJson<ProjectData>(jsonString);
            for (int a = 0; a < chart.lanes.Count; a++)
            {
                chart.lanes[a].recordedBeats.Clear();
                chart.lanes[a].holdDurations.Clear();
            }
            Debug.Log(myProjectData.projectName);
            for (int i = 0; i < myProjectData.groups.Notes.Count; i++) 
                {
                for (int j = 0; j < chart.lanes.Count; j++) 
                {
                    if (myProjectData.groups.Notes[i].row == j)
                    {
                        chart.lanes[j].recordedBeats.Add(myProjectData.groups.Notes[i].time * myProjectData.bpm / 60f);
                        chart.lanes[j].holdDurations.Add(myProjectData.groups.Notes[i].duration * myProjectData.bpm / 60f);
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("JSON file is empty or not assigned.");
            myProjectData = null;
        }
    }
}
