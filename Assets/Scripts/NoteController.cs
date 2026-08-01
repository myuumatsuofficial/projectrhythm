using Unity.VisualScripting;
using UnityEngine;

public class NoteController : MonoBehaviour
{
    [Header("Timing")]
    public float targetBeat;
    public float spawnBeat;

    [Header("Hold")]
    [Tooltip("Durasi hold dalam detik, 0 = note biasa")]
    public float holdDuration = 0f;
    public bool isHoldNote => holdDuration > 0.05f;

    [Header("Position")]
    public Vector3 startPos;
    public Vector3 endPos;
    public Vector3 missPos;

    [Header("State")]
    public bool isHit = false;
    public bool isHolding = false;
    public bool isHoldCompleted = false;
    public bool isMissed = false;
    public bool holdStarted = false;

    [Header("Hold Tick")]
    [Tooltip("Interval waktu (detik) antar tick score saat hold")]
    public float holdTickInterval = 0.1f;
    private float holdTickTimer = 0f;

    [Header("Visual")]
    [Tooltip("Transform child untuk ekor hold note, bisa kosong jika nggak ada")]
    public Transform holdTail;
    [Tooltip("Transform child untuk kepala note. Disembunyikan saat hold dimulai")]
    public Transform noteHead;

    private float initialTailLength = 0f;
    private Vector3 backDirLocal;

    private float holdEndBeat;
    private float secPerBeat;

    private float holdElapsed = 0f;

    public void Initialize()
    {
        secPerBeat = Conductor.Instance.secPerBeat;

        if (isHoldNote)
        {
            holdEndBeat = targetBeat + (holdDuration / secPerBeat);

            if (holdTail == null)
            {
                Transform found = transform.Find("HoldTail");
                if (found != null) holdTail = found;
            }

            if (noteHead == null)
            {
                Transform found = transform.Find("NoteHead");
                if (found != null) noteHead = found;
            }

            UpdateHoldTailVisual();
        }
        else
        {
            if (holdTail != null) holdTail.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        float currentBeat = Conductor.Instance.songPositionInBeats;
        float t = Mathf.Clamp01((currentBeat - spawnBeat) / Mathf.Max(targetBeat - spawnBeat, 0.001f));

        transform.position = Vector3.LerpUnclamped(startPos, endPos, t);

        float tMiss = (currentBeat - targetBeat) / 1.0f;
        transform.position = Vector3.LerpUnclamped(endPos, missPos, tMiss);
      

        // Note missed: tail tetap gerak, tunggu selesai lalu hilang
        if (isMissed && isHoldNote)
        {
            // Hitung berapa lama sejak kepala note melewati hit point
            float timeSinceHead = (currentBeat - targetBeat) * secPerBeat;
            // Tail dianggap selesai pas waktu berlalu
            if (timeSinceHead >= holdDuration)
            {
                Deactivate();
            }
            return; // Jangan proses hold logic
        }

        if (isHolding && isHoldNote)
        {
            holdElapsed += Time.deltaTime;

            // Tick Score
            holdTickTimer += Time.deltaTime;
            if (holdTickTimer > holdTickInterval)
            {
                holdTickTimer = 0f;
                ScoreManager.Instance.HoldTick();
            }
            
            UpdateHoldTailConsumed();

            if (currentBeat >= holdEndBeat) CompleteHold();
        }
    }

    public void StartHold()
    {
        if (!isHoldNote) return;
        if (holdStarted) return;
        holdStarted = true;
        isHolding = true;
        holdTickTimer = 0f;
        holdElapsed = 0f;

        // Sembunyikan kepala note saat hold dimulai
        if (noteHead != null)
        {
            noteHead.gameObject.SetActive(false);
        }
        else
        {
            // Fallback: sembunykan renderer di root object sendiri
            Renderer r = GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
    }

    public void MarkMissed()
    {
        if (!isHoldNote) { Deactivate(); return; }
        isMissed = true;
        holdStarted = true; // Block note input
        isHolding = false;
        holdTickTimer = 0f;
        holdElapsed = 0f;

        // Sembunyikan kepala note
        if (noteHead != null) noteHead.gameObject.SetActive(false);
        else
        {
            Renderer r = GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
        if (holdTail != null)
        {
            holdTail.gameObject.SetActive(false);
        }
        Deactivate();
    }

    public void ReleaseHold()
    {
        if (!isHoldNote || !isHolding) return;
        isHolding = false;
        holdTickTimer = 0f;

        float currentBeat = Conductor.Instance.songPositionInBeats;
        float holdProgress = (currentBeat - targetBeat) / (holdEndBeat - targetBeat);

        if (holdProgress >= 0.8f) CompleteHold();
        else
        {
            Debug.Log($"Hold released early: {holdProgress * 100f:F0}%");
            Deactivate();
        }
    }

    private void CompleteHold()
    {
        if (isHoldCompleted) return;
        isHoldCompleted = true;
        isHolding = false;
        Debug.Log("Hold Complete!");
        Deactivate();
    }

    private void ApplyTailLength(float length)
    {
        if (holdTail == null) return;

        float safeLength = Mathf.Max(length, 0f);

        Vector3 s = holdTail.localScale;
        s.z = safeLength;
        holdTail.localScale = s;

        if (safeLength <= 0f)
        {
            holdTail.gameObject.SetActive(false);
            return;
        }

        holdTail.localPosition = backDirLocal * (initialTailLength - safeLength * 0.5f);
    }

    private void UpdateHoldTailVisual()
    {
        if (holdTail == null) return;

        float laneLength = Vector3.Distance(startPos, endPos);
        if (laneLength <= 0f) return;

        float holdBeats = holdDuration / secPerBeat;
        float spawnWindowBeats = targetBeat - spawnBeat;
        float holdRatio = Mathf.Max(holdBeats / spawnWindowBeats);
        initialTailLength = holdRatio * laneLength;

        Vector3 backDirWorld = (startPos - endPos).normalized;
        backDirLocal = transform.InverseTransformDirection(backDirWorld);

        ApplyTailLength(initialTailLength);
    }

    private void UpdateHoldTailConsumed()
    {
        if (holdTail == null || initialTailLength <= 0f) return;

        float progress = Mathf.Clamp01(holdElapsed / holdDuration);
        float remaining = initialTailLength * (1f - progress);

        ApplyTailLength(remaining);
    }


    public void Deactivate()
    {
        gameObject.SetActive(false);
        isHit = true;
    }
}