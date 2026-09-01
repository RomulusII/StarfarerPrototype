using UnityEngine;

/// <summary>
/// Bir dalga tanımı: bütçe aralığı, izin verilen düşman tipleri ve giriş yönü.
/// Dalganın TÜM gemileri aynı anda doğar; "spawn aralığı" diye bir şey yoktur.
/// FormationTemplate atanmazsa ChapterManager içerik rollerine göre otomatik seçer.
/// </summary>
[System.Serializable]
public class WaveData
{
    [Tooltip("Bu wave için harcanan tehdit puanı aralığı.")]
    public int budgetMin = 4;
    public int budgetMax = 6;

    [Tooltip("Bu wave'de spawn olabilecek tipler. Boşsa chapter'ın genel havuzu kullanılır.")]
    public EnemyTypeData[] allowedTypes;

    [Tooltip("Null ise içeriğe göre otomatik seçilir.")]
    public FormationTemplate formation;

    [Tooltip("Right dışında bir yön zorlamak için. Random = ağırlıklı seçim.")]
    public SpawnSide spawnSide = SpawnSide.Right;

    [Tooltip("Set edilirse bu boss gemi bu wave'de spawn edilir. allowedTypes ile birlikte kullanılabilir (escort).")]
    public BossShipData bossType;

    [Tooltip("Bütçeye sığmasa bile bu dalgada EN AZ BİR tane bulunması garanti " +
             "edilen tip. Bölümün kimliğini taşıyan tip için kullanılır.")]
    public EnemyTypeData guaranteedType;
}
