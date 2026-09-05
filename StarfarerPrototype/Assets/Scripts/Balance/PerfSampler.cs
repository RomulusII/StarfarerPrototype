using UnityEngine;

/// <summary>
/// Kare süresi ölçümü. <see cref="BalanceLog"/> oyunun nasıl OYNANDIĞINI
/// kaydediyordu ama ne kadar akıcı ÇALIŞTIĞINI hiç kaydetmiyordu; dağıtılan
/// build'lerden "telefonda takılıyor mu" sorusunun cevabı gelmiyordu.
///
/// **Pencere başına TEK satır yazılır** (bkz. <see cref="WindowSeconds"/>).
/// Kare başına satır yazmak kaydı yüz kat büyütür ve ölçümün kendisi ölçülen
/// şeyi bozardı.
///
/// **Ortalama tek başına yanıltıcıdır.** Saniyede bir gelen 200 ms'lik takılma
/// ortalamayı 60 fps'ten ancak 55'e indirir, oysa oyuncunun şikâyet ettiği şey
/// tam olarak odur. Bu yüzden p50, p95 ve en kötü kare de yazılır.
///
/// **Zaman ölçeğinin DIŞINDA ölçülür.** Hız düğmesi (1x/3x/10x) simülasyon
/// hızını değiştirir, kare süresini değil; <c>deltaTime</c> okunsaydı 10x'te
/// ölçüm on kat bozulurdu.
///
/// Sahadaki gemi sayısı da yazılır: onsuz "35 fps" sayısı hangi yükte
/// ölçüldüğünü söylemez ve iki kayıt karşılaştırılamaz.
/// </summary>
public class PerfSampler : MonoBehaviour
{
    /// <summary>Kaç saniyede bir satır yazılır.</summary>
    public const float WindowSeconds = 10f;

    /// <summary>
    /// Pencere tamponu — 10 saniyelik 240 fps'i taşır. Sabit dizidir, kare
    /// başına hiçbir şey ayrılmaz. Taşarsa fazla kareler yüzdelik hesabına
    /// girmez; en kötü kare ayrı tutulduğu için o yine de görülür.
    /// </summary>
    const int MaxSamples = 2400;

    static PerfSampler _instance;

    readonly float[] _ms = new float[MaxSamples];
    int   _count;
    float _elapsed;
    float _worst;

    /// <summary>
    /// Örnekleyiciyi kurar. <see cref="BalanceUploader.EnsureExists"/> ile aynı
    /// desen: runtime'da doğar, ayrı sahne veya prefab gerekmez.
    /// </summary>
    public static void EnsureExists()
    {
        // Simülasyon koşusunda anlamsız: batchmode'da render yok ve koşu
        // gerçek zamandan kopuk ilerler — ölçülen sayı donanımı değil,
        // koşucunun hızını anlatırdı.
        if (_instance != null || SimRuntime.Active || !BalanceLog.Enabled) return;

        _instance = new GameObject("PerfSampler").AddComponent<PerfSampler>();
    }

    void Update()
    {
        // Açılış menüsü ve game over ekranı ölçülmez: oyuncu oynamıyor, boş
        // sahnenin kare süresi ortalamayı yukarı çeker. Upgrade ekranı ölçülür
        // — oyun duruyor ama o ekran ağır bir UI ve maliyeti gerçek.
        if (StartMenuUI.IsOpen || GameManager.IsGameOver) return;

        float dt = Time.unscaledDeltaTime;
        float ms = dt * 1000f;

        _elapsed += dt;
        if (_count < MaxSamples) _ms[_count++] = ms;
        if (ms > _worst) _worst = ms;

        if (_elapsed >= WindowSeconds) Flush();
    }

    /// <summary>Sahne yeniden yüklenirken yarım pencere kaybolmasın.</summary>
    void OnDisable()
    {
        if (_count > 0) Flush();
    }

    void Flush()
    {
        if (_count == 0) { Reset(); return; }

        // Yüzdelik için sıralama pencere başına BİR kez yapılır: 10 saniyede
        // bir birkaç bin eleman, kare bütçesinde görünmez.
        System.Array.Sort(_ms, 0, _count);

        float sum = 0f;
        for (int i = 0; i < _count; i++) sum += _ms[i];
        float mean = sum / _count;

        int p95 = Mathf.Min(_count - 1, Mathf.FloorToInt(_count * 0.95f));

        BalanceLog.Event("perf")
                  .Num("kare",    _count)
                  .Num("ms_ort",  mean)
                  .Num("ms_p50",  _ms[_count / 2])
                  .Num("ms_p95",  _ms[p95])
                  .Num("ms_max",  _worst)
                  .Num("fps_ort", mean > 0f ? 1000f / mean : 0f)
                  .Num("dusman",  CountEnemies())
                  // Profiler API'si release build'lerde kırpılır ve 0 döner;
                  // GC.GetTotalMemory her platformda çalışır ve GC baskısını
                  // görmeye yeter.
                  .Num("gc_mb",   System.GC.GetTotalMemory(false) / (1024f * 1024f))
                  .End();

        Reset();
    }

    void Reset()
    {
        _count   = 0;
        _elapsed = 0f;
        _worst   = 0f;
    }

    /// <summary>
    /// Sahne taraması pencere başına BİR kez. Kare süresini açıklayan asıl
    /// değişken sahadaki gemi sayısıdır; EnemySpawner'ın kendi taraması
    /// (0.25 sn) dışarı açık olmadığı için burada ayrıca sayılır — 10 saniyede
    /// bir yapılan bir tarama ölçülebilir bir maliyet değil.
    /// </summary>
    static int CountEnemies()
        => FindObjectsByType<EnemyBot>(FindObjectsSortMode.None).Length;
}
