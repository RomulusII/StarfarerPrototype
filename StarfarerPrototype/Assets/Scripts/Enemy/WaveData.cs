using UnityEngine;

/// <summary>
/// Bir dalga tanımı: bütçe aralığı, izin verilen düşman tipleri ve giriş yönü.
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

    [Tooltip("Wave içindeki spawn aralığı (saniye). 0 = chapter varsayılanı kullanılır.")]
    public float spawnInterval = 0f;
}
