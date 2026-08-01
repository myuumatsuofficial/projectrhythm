using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class Conductor : MonoBehaviour
{
    public static Conductor Instance;

    [Header("Data Source")]
    public SongDataSO songData;

    [Header("Runtime Info (Read Only")]
    public float songPosition;
    public float songPositionInBeats;
    public float secPerBeat;

    public float dspSongTime;
    private AudioSource audioSource;

    private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);

        audioSource = GetComponent<AudioSource>();
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        secPerBeat = 60f / songData.bpm;

        audioSource.clip = songData.audioClip;

        dspSongTime = (float)AudioSettings.dspTime;

        audioSource.Play();
    }

    // Update is called once per frame
    void Update()
    {
        songPosition = (float)(AudioSettings.dspTime - dspSongTime - songData.noteOffset);

        songPositionInBeats = songPosition / secPerBeat;
    }
}
