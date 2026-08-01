using UnityEngine;
[CreateAssetMenu(fileName = "New Song Data",menuName = "Rhythm Game/Song Data")]

public class SongDataSO : ScriptableObject
{
    [Header("Song Metadata")]
    public string songName;
    public string artistName;

    [Header("Audio Config")]
    public AudioClip audioClip;

    [Tooltip("Jumlah ketukan per menit, Wajib Akurat")]
    public float bpm;

    [Tooltip("Penyesuaian waktu (detik) untuk mengatasi latency/hening di awal lagu")]
    public float noteOffset;
}
