/// <summary>
/// Oyunun sürüm kimliği. Kayıtların hangi build'den geldiğini söyler.
///
/// Neden iki ayrı sayı var: bunlar farklı sorulara cevap verir.
///
///   Surum — oyuncunun gördüğü sürüm. Mağazaya giden numara budur ve
///           build script'leri PlayerSettings.bundleVersion'a bunu yazar,
///           yani ProjectSettings ile kod arasında sapma OLAMAZ.
///
///   Denge — DENGE REVİZYONU. Bir formül, bir sabit, bir düşman değeri
///           değiştiğinde artar. Sürüm numarası bunun için yetmez: aynı
///           1.0.0 altında onlarca ayar denemesi yapılır ve sonrasında
///           gelen kayıtlar öncekilerle karıştırılamaz. Log'da ayrı alan
///           olarak yazılır, analiz bu alana göre gruplar.
///
/// KURAL: dengeyi etkileyen her değişiklikte <see cref="Denge"/> artırılır.
/// Artırılmazsa iki farklı ayarın verisi tek havuzda toplanır ve ölçüm
/// sessizce anlamsızlaşır — kaybı fark etmek de mümkün olmaz.
///
/// Bu alanların HİÇ OLMADIĞI kayıtlar 2026-09-05 öncesine aittir; o
/// dosyalarda serbest mod bütçesi saatte %10 bileşik büyüyordu.
/// </summary>
public static class GameVersion
{
    /// <summary>Oyuncunun gördüğü sürüm (mağaza numarası).</summary>
    public const string Surum = "1.0.0";

    /// <summary>
    /// Denge revizyonu.
    ///   1 — serbest mod dalga bütçesi saatten KAYNAĞA taşındı,
    ///       Swarm hızı 2.4 -> 2.76.
    ///   2 — nişan çizgisi eklendi (bkz. AimLine). Mekanik değişmedi ama
    ///       oyuncunun gördüğü BİLGİ değişti: namlunun gerçek yönü artık
    ///       görünüyor. İsabet oranının bundan etkilenmesi bekleniyor, yani
    ///       1 numaralı revizyonun %52'siyle bu revizyonun sayısı aynı
    ///       havuzda toplanamaz.
    ///   3 — zorluk düşman HP'si ve hasarını çarpıyor (Kolay ×0.8, Zor ×1.2).
    ///       Normal'in sayıları değişmedi, ama bu revizyondan önceki Kolay/Zor
    ///       kayıtları Normal ile birebir aynı oyundu. Kayıtlar artık
    ///       `zorluk` alanını taşıyor; analiz ona göre ayırmalı.
    ///   4 — ÇÖP GEMİLERİN STATI DÜZLEŞTİ. HP, hasar ve zırh artık levelle
    ///       büyümüyor (boss hariç); kaybolan büyüme dalga bütçesine devredildi
    ///       (budgetGrowth 1.027 -> 1.05093) ve drop tehdit başına SABİT oldu
    ///       (dropGrowth 1.0, taban 2.1 -> 1.8972). Bir levele gelen toplam
    ///       efektif HP ve kampanya geliri birebir korundu; değişen şey aynı
    ///       işin uzun HP barları yerine DAHA ÇOK GEMİ olarak gelmesi — Lv100'de
    ///       level başına ~11 gemi yerine ~139.
    ///
    ///       Bu revizyonun kayıtları öncekilerle KARIŞTIRILAMAZ: tehdit puanı
    ///       3'te ve öncesinde levelle şişen bir birimdi (Lv100'de gerçek değeri
    ///       ~39 katıydı), 4'te dürüst bir sabittir. Tehdit doğrulama
    ///       regresyonu ancak 4 ve sonrasının verisiyle koşulabilir.
    ///
    ///       Yan etki, kasıtlı: gelen DPS Lv100'de 56× yerine 137×, yani geç
    ///       oyun ~2.4 kat ölümcül. EHP'yi korumak tercih edildi çünkü ölçüm
    ///       hem level süresinin kısa (0.84 dk) hem ölümün sıfır (8/8 koşu)
    ///       olduğunu söylüyordu.
    ///
    ///       Kompozisyon da değişti: dalga kadrosu artık tehdit^(-0.5) ağırlıklı
    ///       seçiliyor (eskiden düzgün rastgele).
    ///   5 — KALABALIĞIN CEVABI. Oyuncuya ilk kez alan hasarı geldi:
    ///       güdümlü roket çarpınca patlar (yarıçap 1.2), iki yeni turret
    ///       uzmanlaşması — Flak (yarıçap 1.8) ve Nükleer Başlık (yarıçap 3.2,
    ///       yarı ateş hızı). Point Defence üçüncü kademe kazandı: mühimmat ve
    ///       küçük hedef yoksa menzildeki her gemiye ateş eder.
    ///
    ///       Log'da patlama TEK isabet yazar, yakaladığı hedef sayısı ayrı
    ///       alandadır (`yakalanan`) — isabet oranı %100'ü aşmasın diye.
    ///   6 — DALGA TEMPOSU. Ara dalgalar tehditlerinin %90'ı yok edilince
    ///       geçer; kalanlar savaşmaya devam eder, yeni dalga üstlerine biner.
    ///       Levelin son dalgası hâlâ tam temizlenme bekler. Her bölümün 5.
    ///       levelinde orta dalga CÜMBÜŞ olur (bütçenin %55'i, alfa 2). 12
    ///       gemiden büyük dalgalar 1.5 sn arayla kollar hâlinde gelir.
    ///       Level süresi ve üst üste binen tehdit bu revizyondan önceki
    ///       kayıtlarla kıyaslanamaz. `wave` olayı `cumbus` alanını taşır.
    ///   7 — İKİ ZIRHLI TİP. Tabya (tehdit 55, zırh 20, 400 HP) 8. bölümden,
    ///       Muhafız (tehdit 10, çevresine +6 zırh aurası) 6. bölümden itibaren
    ///       havuzlarda. Levelden gelen taban zırh kaldırıldıktan sonra zırh
    ///       eşiğini geç bölümlerde yaşatan onlar; Tabya ayrıca kompozisyon
    ///       alfasının ağır ucunu açıyor. 6. bölümden sonraki dalga kadroları
    ///       önceki revizyonlarla kıyaslanamaz.
    ///   8 — KAMPANYA ZORLAŞTI. Level bütçesi tabanı 7 -> 9, büyümesi
    ///       1.05093 -> 1.053: Lv10 ×1.33, Lv50 ×1.42, Lv100 ×1.56 tehdit.
    ///       Drop tehdit başına sabit, yani gelir de aynı oranda arttı.
    ///       Lazer turreti döngünün yarısında yanıyor (1.5 sn / 3 sn), ışın
    ///       DPS'i 26 -> 8.67 — ortalama DPS aynı, zırha karşı daha zayıf.
    ///       Nükleer başlık hızı 2.5 -> 1.25 (menzil korunarak).
    ///   9 — DESTEK GEMİLERİ. Muhafız silahsız bir destek gemisine döndü
    ///       (tehdit 10 -> 8): en yakın gemiye bağlanıp onun arkasında durur,
    ///       yalnız kalınca uzaklaşır. Yeni tip Besleyici (tehdit 8): 2 sn'de bir
    ///       yaydığı dalga kalkanlı gemileri maks. kalkanlarının %20'si kadar
    ///       doldurur. 6, 7, 8 ve 10. bölüm havuzlarında. Kalkanlı gemilerin
    ///       dayanıklılığı ve Muhafız'lı dalgaların DPS'i önceki revizyonlarla
    ///       kıyaslanamaz.
    ///  10 — ANA GEMİYE ZIRH. Onarım biriminin "Enerji Verimi" izi kaldırıldı,
    ///       yerine Zırh geldi: gövdeye gelen her isabetten seviye × 0.5 düşer
    ///       (düşmanlardaki eşik formülü; ışında oran). Komponentlere işlemez,
    ///       birden fazla birimde en yükseği geçerli ve yalnızca biri taşıyabilir.
    ///       Onarım birimi boştayken artık enerji çekmiyor (eskiden sürekli
    ///       1/sn). `player_damage` olayı `zirh` alanını taşır.
    ///  11 — FLAK ŞARAPNEL ATAR. Alan hasarı yerine 30 kıymık, kıymık başına
    ///       3 hasar, menzil 4 (ana geminin boyu); zırh kıymık başına ısırır.
    ///       Mermi nişan aldığı buluşma noktasında ya da menzil sonunda havada
    ///       patlar. Doğrudan isabette %40 hedefe, kalanı 100°'lik koniyle
    ///       arkasına. Havada patlayıp hiçbir şeye değmeyen mermi `shot_hit`
    ///       yazmaz (ıska); `shot_hit` yeni `kiymik` alanını taşır. Flak'in
    ///       isabet oranı ve hasarı önceki revizyonlarla kıyaslanamaz.
    /// </summary>
    public const int Denge = 11;
}
