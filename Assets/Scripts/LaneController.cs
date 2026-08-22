using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class LaneController : MonoBehaviour
{
    [Header("Config")]
    public Key inputKey;
    public GameObject notePrefab;
    public Transform spawnPoint;
    public Transform hitPoint;
    public Transform missPoint;

    [Header("Timing")]
    public float spawnDistanceInBeats = 2f;

    public List<float> noteTimestamps = new List<float>();
    public List<float> holdDurations = new List<float>();

    private int spawnIndex = 0;
    private int inputIndex = 0;

    private List<NoteController> activeNotes = new List<NoteController>();

    void Update()
    {
        CheckAndSpawnNotes();

        CheckPlayerInput();

        CheckMissedNotes();
    }

    void CheckAndSpawnNotes()
    {
        if (spawnIndex < noteTimestamps.Count)
        {
            float targetTime = noteTimestamps[spawnIndex];
            float currentBeat = Conductor.Instance.songPositionInBeats;
            if (currentBeat >= targetTime - spawnDistanceInBeats)
            {
                float holdDuration = (holdDurations != null && spawnIndex < holdDurations.Count)
                    ? holdDurations[spawnIndex]
                    : 0f;
                SpawnNote(targetTime, holdDuration);
                spawnIndex++;
            }
        }
    }

    void SpawnNote(float targetBeat, float holdDuration = 0f)
    {
        GameObject obj = Instantiate(notePrefab, spawnPoint.position, Quaternion.identity);
        NoteController note = obj.GetComponent<NoteController>();

        note.targetBeat = targetBeat;
        note.spawnBeat = targetBeat - spawnDistanceInBeats;
        note.startPos = spawnPoint.position;
        note.endPos = hitPoint.position;
        note.missPos = missPoint.position;
        note.holdDuration = holdDuration;

        note.Initialize();

        activeNotes.Add(note);
    }

    void CheckPlayerInput()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[inputKey].wasPressedThisFrame) HandleInput();
        if (Keyboard.current[inputKey].wasReleasedThisFrame) HandleRelease();
    }

    void HandleInput()
    {
        float currentBeat = Conductor.Instance.songPositionInBeats;
        NoteController targetNote = GetNextActiveNote();

        if (targetNote != null)
        {
            float diff = Mathf.Abs(currentBeat - targetNote.targetBeat);

            if (diff < 0.35f)
            {
                ScoreManager.Instance.Hit("Perfect");
                if (targetNote.isHoldNote) targetNote.StartHold();
                else targetNote.Deactivate();
            }
            else if (diff < 0.7f)
            {
                if (targetNote.isHoldNote)
                {
                    ScoreManager.Instance.Miss();
                    targetNote.MarkMissed();
                }
                else
                {
                    ScoreManager.Instance.Hit("Good");
                    targetNote.Deactivate();
                }
            }
            else if (diff < 1f)
            {
                if (targetNote.isHoldNote)
                {
                    ScoreManager.Instance.Miss();
                    targetNote.MarkMissed();
                }
                else
                {
                    ScoreManager.Instance.Bad();
                    targetNote.Deactivate();
                }
            }
            else
            {
                Debug.Log("Too Early / Whiff!");
            }
        }
    }
    void HandleRelease()
    {
        foreach (var note in activeNotes)
        {
            // If we find a note that is currently being held, release it
            if (note.isHolding)
            {
                note.ReleaseHold();
                break; // We found the active hold note, no need to check the rest
            }
        }
    }

    NoteController GetNextActiveNote()
    {
        foreach (var note in activeNotes)
        {
            //Skip note yang sudah di miss (hold menunggu tail habis) atau sudah di hit
            if (!note.isHit && !note.isMissed &&!note.isHolding && note.gameObject.activeSelf)
                return note;
        }
        return null;
    }

    void CheckMissedNotes()
    {
        if (inputIndex < spawnIndex)
        {
            foreach (var note in activeNotes)
            {
                if (!note.isHit && !note.isHolding && Conductor.Instance.songPositionInBeats > note.targetBeat + 0.7f)
                {
                    Debug.Log("MISS");
                    ScoreManager.Instance.Miss();

                    if (note.isHoldNote) note.MarkMissed();
                    else note.Deactivate();
                }
            }
        }
    }
}