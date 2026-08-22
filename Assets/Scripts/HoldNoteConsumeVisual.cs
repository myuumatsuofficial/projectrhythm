using UnityEngine;

/// <summary>
/// Menggantikan mekanisme lama yang mengecilkan size + mengubah posisi hold note
/// tiap frame. Sekarang transform note TIDAK disentuh sama sekali — hanya alpha
/// visual yang di-update lewat MaterialPropertyBlock, yang jauh lebih murah karena:
///
/// 1. Tidak ada penulisan ulang matrix transform tiap frame (localScale/position).
/// 2. Tidak ada recalculation bounds/collider akibat perubahan transform.
/// 3. Tidak membuat instance Material baru (renderer.material membuat instance
///    baru & clone tiap kali diakses) -> tidak ada garbage collection spike.
/// 4. MaterialPropertyBlock tidak merusak SRP Batcher / GPU instancing selama
///    semua note memakai Material dasar yang sama.
///
/// Pasang script ini di GameObject yang punya MeshRenderer capsule/cylinder
/// hold note, dengan material yang memakai shader "Custom/HoldNoteConsume".
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class HoldNoteConsumeVisual : MonoBehaviour
{
    [Tooltip("Renderer mesh visual hold note. Auto-diisi dari GetComponent kalau kosong.")]
    [SerializeField] private MeshRenderer _renderer;

    [Tooltip("Lebar soft-edge fade di batas konsumsi (0.001 - 0.5). Samakan feel dengan _FadeWidth di shader.")]
    [SerializeField, Range(0.001f, 0.5f)] private float _fadeWidth = 0.06f;

    [Tooltip("1 = note terkonsumsi dari UV.y=0 -> 1, -1 = arah sebaliknya. Sesuaikan dengan orientasi mesh kamu.")]
    [SerializeField] private float _consumeDirection = 1f;

    private static readonly int ConsumeAmountId = Shader.PropertyToID("_ConsumeAmount");
    private static readonly int FadeWidthId = Shader.PropertyToID("_FadeWidth");
    private static readonly int ConsumeDirectionId = Shader.PropertyToID("_ConsumeDirection");

    private MaterialPropertyBlock _propBlock;
    private float _currentConsumeAmount = -1f; // -1 supaya set pertama selalu apply

    private void Awake()
    {
        if (_renderer == null)
            _renderer = GetComponent<MeshRenderer>();

        _propBlock = new MaterialPropertyBlock();

        // Set nilai awal (fadeWidth & direction jarang berubah per-frame, jadi
        // cukup di-set sekali di Awake, bukan tiap frame).
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(FadeWidthId, _fadeWidth);
        _propBlock.SetFloat(ConsumeDirectionId, _consumeDirection);
        _propBlock.SetFloat(ConsumeAmountId, 0f);
        _renderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>
    /// Panggil ini tiap kali progress konsumsi hold note berubah
    /// (misalnya tiap frame selama note sedang di-hold), dengan nilai 0-1.
    /// 0 = belum ada bagian yang terkonsumsi, 1 = seluruh note sudah terkonsumsi.
    /// </summary>
    public void SetConsumeAmount(float normalizedAmount)
    {
        normalizedAmount = Mathf.Clamp01(normalizedAmount);

        // Skip kalau nilainya nggak berubah signifikan -> hindari SetPropertyBlock
        // yang tidak perlu (masih murah, tapi kenapa tidak sekalian dihemat).
        if (Mathf.Approximately(normalizedAmount, _currentConsumeAmount))
            return;

        _currentConsumeAmount = normalizedAmount;

        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(ConsumeAmountId, normalizedAmount);
        _renderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>
    /// Reset visual ke kondisi belum terkonsumsi. Panggil saat note di-spawn/di-reuse
    /// dari object pool.
    /// </summary>
    public void ResetVisual()
    {
        SetConsumeAmount(0f);
        // Paksa apply walau nilai sebelumnya juga 0 (misal habis pooling)
        _currentConsumeAmount = -1f;
        SetConsumeAmount(0f);
    }
}
