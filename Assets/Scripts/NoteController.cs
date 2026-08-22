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

    [Header("Hold Tail Consume Visual")]
    [Tooltip("Material holdTail HARUS pakai shader Custom/HoldNoteConsume (atau shader lain yang punya property _ConsumeAmount) supaya efek ini jalan.")]
    [SerializeField, Range(0.001f, 0.5f)] private float fadeWidth = 0.06f;
    [Tooltip("1 = konsumsi dari UV.y 0->1, -1 = arah sebaliknya. Kalau arah fade kebalik saat di-test, tinggal flip ini.")]
    [SerializeField] private float consumeDirection = 1f;
    [Tooltip("Kecepatan hilangnya tail. Semakin KECIL nilainya, semakin PERLAHAN tail menghilang (butuh waktu lebih lama dari holdDuration aslinya untuk full transparent). Semakin BESAR, semakin CEPAT hilang. 1 = pas sinkron dengan holdDuration.")]
    public float consumeSpeed = 1f;

    private float initialTailLength = 0f;
    private Vector3 backDirLocal;

    private float holdEndBeat;
    private float secPerBeat;

    private float holdElapsed = 0f;

    // Visual consumption dengan transparency (spatial wipe via shader)
    private Renderer holdTailRenderer;
    private MaterialPropertyBlock propBlock;
    private static readonly int ConsumeAmountId = Shader.PropertyToID("_ConsumeAmount");
    private static readonly int FadeWidthId = Shader.PropertyToID("_FadeWidth");
    private static readonly int ConsumeDirectionId = Shader.PropertyToID("_ConsumeDirection");

    private float lastAppliedConsumeAmount = -1f; // -1 supaya apply pertama selalu jalan

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

            // Initialize material property block untuk shader-based consume wipe
            if (holdTail != null)
            {
                holdTailRenderer = holdTail.GetComponent<Renderer>();
                if (holdTailRenderer != null)
                {
                    propBlock = new MaterialPropertyBlock();

                    // fadeWidth & consumeDirection jarang berubah, cukup di-set sekali di sini
                    holdTailRenderer.GetPropertyBlock(propBlock);
                    propBlock.SetFloat(FadeWidthId, fadeWidth);
                    propBlock.SetFloat(ConsumeDirectionId, consumeDirection);
                    holdTailRenderer.SetPropertyBlock(propBlock);
                }
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

        // Set size awal (ukuran mesh tail) di sumbu Y - tidak diubah lagi selama consume berjalan.
        Vector3 s = holdTail.localScale;
        s.y = initialTailLength;
        holdTail.localScale = s;

        // Pivot mesh HoldTail ada di TENGAH -> scaling Y memanjang ke DUA arah sekaligus.
        // Supaya ujung DEPAN tail tetap nempel pas di titik head (local origin, 0,0,0)
        // dan cuma memanjang ke arah backDirLocal (menjauhi head), kita geser posisinya
        // sejauh setengah panjang mesh (sudah dikali scale) ke arah backDirLocal.
        //
        // Asumsi: head berada tepat di local origin (0,0,0) note ini. Kalau head kamu
        // punya local offset sendiri, tambahkan offset itu ke perhitungan di bawah.
        float meshHalfHeight = 0.5f; // fallback kalau MeshFilter/sharedMesh tidak ada
        MeshFilter tailMeshFilter = holdTail.GetComponent<MeshFilter>();
        if (tailMeshFilter != null && tailMeshFilter.sharedMesh != null)
        {
            meshHalfHeight = tailMeshFilter.sharedMesh.bounds.extents.y;
        }

        holdTail.localPosition = backDirLocal * (meshHalfHeight * s.y);

        // Reset consume amount ke 0 (belum ada yang terkonsumsi / full visible)
        SetConsumeAmount(0f, forceApply: true);

        if (holdTail != null && !holdTail.gameObject.activeSelf)
            holdTail.gameObject.SetActive(true);
    }

    private void UpdateHoldTailConsumed()
    {
        // consumeSpeed mengatur seberapa cepat holdElapsed "terasa" bagi fade:
        // < 1 = fade lebih lambat dari holdDuration asli, > 1 = lebih cepat.
        float progress = Mathf.Clamp01((holdElapsed / holdDuration) * consumeSpeed);
        SetConsumeAmount(progress);

        // Sembunyikan tail kalau sudah full terkonsumsi (hemat render, bukan lagi ganti transform)
        if (progress >= 0.99f && holdTail != null && holdTail.gameObject.activeSelf)
        {
            holdTail.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Update progress konsumsi visual tail lewat MaterialPropertyBlock.
    /// Tidak menyentuh transform/scale sama sekali - hanya 1 float ke GPU.
    /// </summary>
    private void SetConsumeAmount(float normalizedAmount, bool forceApply = false)
    {
        if (holdTailRenderer == null || propBlock == null) return;

        normalizedAmount = Mathf.Clamp01(normalizedAmount);

        if (!forceApply && Mathf.Approximately(normalizedAmount, lastAppliedConsumeAmount))
            return;

        lastAppliedConsumeAmount = normalizedAmount;

        holdTailRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(ConsumeAmountId, normalizedAmount);
        holdTailRenderer.SetPropertyBlock(propBlock);
    }

    public void Deactivate()
    {
        gameObject.SetActive(false);
        isHit = true;
    }
}