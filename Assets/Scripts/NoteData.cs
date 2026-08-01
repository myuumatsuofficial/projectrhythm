using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NoteData
{
    public string name;
    public int row;
    public float time;
    public float duration;
}

[System.Serializable]
public class GroupData
{
    public List<NoteData> Notes;
}

[System.Serializable]
public class ProjectData
{
    public string projectName;
    public int bpm;
    public float duration;
    public GroupData groups;
}
