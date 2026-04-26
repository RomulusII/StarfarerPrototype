# Starfarer — Claude Project Instructions

## Genel Bakış
Bu proje Unity 6.3 LTS ile geliştirilen 2D uzay oyunudur. Oyun hem PC hem mobil (Android) hedeflidir. Geliştirici C# konusunda uzmandır, Unity'yi orta seviyede bilmektedir. Claude Code (terminal tabanlı) ile birlikte geliştirme yapılmaktadır.

**GitHub:** https://github.com/RomulusII/StarfarerPrototype

---

## Hikaye
Uzak bir gezegenden dönen araştırma gemisi, FTL bubble'dan erken çıkmak zorunda kalır ve Oort bulutu bölgesinde, Dünya'ya 1 ışık yılı uzakta mahsur kalır. Yola devam etmek için kaynak toplamak gerekir. Kaynak toplarken küçük botların saldırısına uğrarlar — botlar ilerledikçe büyür ve tehlikeli hale gelir. Ekip Dünya ile iletişim kurmaya çalışırken botların kaynağını araştırır.

**Gizem:** Botların kökeni kasıtlı olarak belirsiz bırakılır. Her bölümden sonra çelişkili ipuçları ortaya çıkar — tanıdık semboller, hiç görülmemiş teknolojiler, hibrit yapılar. Araştırma gemisinin giderken bir şeyi tetiklemiş olabileceği ima edilir. Hikaye kesin bir cevap vermez.

**Anlatım:** Bölüm aralarında pilotların kısa diyalogları ile aktarılır. Hikayeye dayanmak yerine oynanış ön plandadır.

---

## Oyun Tasarımı

### Tür
Incremental Space Shooter with Base Management. Referans: FTL, Into the Breach ruhu.

### Temel Mekanikler
- **Ana gemi:** Devasa, sabit pozisyonda, mermilerden kaçamaz
- **Silah:** Mouse/touch ile nişan alınır, sol tık/dokunma ile ateş edilir
- **Silah tipleri:** Lazer (kalkana etkili, sürekli enerji tüketir), Plazma (şarj edip burst atar), Kinetik (mermili, enerji yemez)
- **Enerji sistemi:** Jeneratör ürettiği enerjiyi kalkan/silah/itici arasında dağıtır, oyuncu manuel ayarlar
- **Toplayıcı gemiler:** Tuşa basarak asteroid/enkaz bölgesine gönderilir, geri çağrılabilir
- **Kaynak:** 2 tip — Ham madde (fiziksel sistemler için), Enerji kristali (enerji sistemleri için)
- **Upgrade sistemi:** Anlık, bölüm içinde. Komponent satılıp başka şey alınabilir
- **Point defence:** Otomatik kısa menzilli sistemler, küçük hızlı hedeflere karşı

### Gemi Upgrade Slotları
Motor, Enerji Jeneratörü, Kalkan, Ana Silah (slot 5), Otomatik Turretler, İkincil Silahlar (point defence dahil)

### Ana Silah (Slot 5)
Oyun başında Lazer Mk1 kurulu gelir. Her silah tipi (Lazer/Kinetik/Plazma) bağımsız Mk1→Mk2→Mk3 zincirine sahip; hepsi aynı anda farklı tier'larda olabilir. Switch butonu ile aktif tip seçilir. Yeni tip satın alınınca `_unlockedWeapons` dict'e eklenir; slot mekanizması bypass edilir.

### Otomatik Turretler
Slot'a kurulunca belirli bir aralıkta en yakın düşmana otomatik ateş açar.
Oyuncunun müdahalesi gerekmez. Enerji tüketir.
Point defence turretleri küçük/hızlı hedeflere odaklanır.

### Düşman Çeşitliliği
- Küçük hızlı botlar (sürü)
- Zırhlı botlar (kinetike karşı dirençli)
- Kalkan botlar (lazere karşı dirençli)
- Avcı/Bomber: Kalkandan geçip direkt komponentlere saldırır
- Boss: Büyük taşıyıcı gemi, kendi avcılarını üretir, aşamalı yok edilir

### Bölüm Yapısı
8-10 bölüm, ortalama 2-3 dakika. Çeşitlilik:
- Asteroid/enkaz bölgesi (kaynak + küçük botlar)
- Sürü bölümü (çok sayıda hızlı düşman)
- Pusu bölümü (toplayıcılar tuzağa düşer)
- Taşıyıcı boss (uzun, aşamalı)
- Karanlık bölüm (sensörler çalışmıyor)

---

## Teknik Kararlar

### Ortam
- **Unity 6.3 LTS** (6000.3.11f1)
- **Template:** Universal 2D
- **Input:** New Input System (UnityEngine.InputSystem) — eski Input sistemi KULLANILMAZ
- **Render:** URP 2D
- **Platform:** PC + Android + WebGL

### Kod Kuralları
- `FindObjectOfType` yerine `FindFirstObjectByType` kullan (Unity 6 uyumu)
- Mouse input: `Mouse.current.position.ReadValue()`
- Touch input: `Touchscreen.current.primaryTouch.position.ReadValue()` — null check ile
- Tüm objelerin scale'i **uniform** olmalı (X=Y=Z). Şekil ve proporsiyon bilgisi sprite texture boyutundan gelir
- Prefablar: `Assets/Prefabs/` klasöründe
- Scriptler: `Assets/Scripts/` klasöründe

### Sahne Hiyerarşisi Kuralları
- Health/Shield barları her zaman ilgili objenin **child'ı** olmalı
- WeaponMount, PlayerShip'in child'ı
- Body (gövde sprite), PlayerShip'in child'ı
- Barlar world space'de her zaman dik ve yatay kalır (`rotation = Quaternion.identity`)

---

## Mevcut Scriptler

| Script | Görev |
|--------|-------|
| PlayerShip.cs | Ana gemi, sabit pozisyon |
| WeaponMount.cs | Mouse'a dönen silah noktası |
| WeaponController.cs | Ateş etme, New Input System |
| Bullet.cs | Mermi hareketi, trigger collision, 3sn sonra yok olur |
| StarField.cs | 400 yıldız, -15/+15 birim arası random pozisyon |
| CameraController.cs | Parallax kayma + zoom, power curve (t^2), kayma %80'den, zoom %90'dan başlar |
| HealthBar.cs | Can/kalkan barı, SpriteRenderer tabanlı (Canvas değil), child olarak eklenir |
| EnemyBot.cs | Kırmızı küçük bot, sağdan sola hareket, PlayerShip'e çarpınca hasar |
| EnemySpawner.cs | Her 3sn bir EnemyBot spawn eder |
| GameManager.cs | HP sıfırlanınca Game Over, Restart, Time.timeScale yönetimi |

---

## Kamera Sistemi
- **Temel pozisyon:** (0, 0, -10), Orthographic Size: 5
- **Kayma:** Mouse ekranın %80'inden sonra başlar, maksimum 8 birim, power curve t^2
- **Zoom:** Mouse ekranın %90'ından sonra başlar, Size 5→7
- **Formül:**
```csharp
float t = Mathf.Clamp01(delta.magnitude);
float moveT = Mathf.Clamp01((t - 0.8f) / 0.2f);
float curvedMoveT = Mathf.Pow(moveT, 2f);
float zoomT = Mathf.Clamp01((t - 0.9f) / 0.1f);
```

---

## HealthBar Sistemi
- SpriteRenderer tabanlı, Canvas kullanılmaz
- **Kalkan barı:** Mavi, maxShield > 0 ise her zaman görünür
- **Can barı:** Hasar alınca görünür, 3sn sonra gizlenir, timer hasar alınca sıfırlanır
- `TakeDamage(float amount)` metodu ile hasar verilir
- Barlar parent döndüğünde `rotation = Quaternion.identity` ile dik kalır

---

# Starfarer — Güncel Yapılacaklar Listesi

## Tamamlananlar

- [x] Enerji sistemi — EnergyBus, CoreGenerator
- [x] Kalkan sistemi — recharge delay, ShieldGeneratorComponent
- [x] Upgrade sistemi — ShipLoadout, ComponentDefinition (hardcoded)
- [x] Upgrade UI — layout, slot tıklama, hover detay
- [x] Kamera zoom — upgrade açılınca gemiye zoom, Tab'la geri
- [x] World slot daireleri — SlotVisual, renk göstergesi
- [x] Temel silah, mermi, düşman, spawner, GameManager

---

## Tamamlananlar (devam)

- [x] [Kur] / [Sat] / [Upgrade] butonları — kaynak yetersizse inaktif, maliyet kırmızı
- [x] ResourceInventory HUD — ham madde + kristal GeneralPanel'de, her işlemde güncelleniyor
- [x] Silah tipleri — Kinetik / Lazer / Plazma, her biri bağımsız Mk1→Mk2→Mk3 zinciri
- [x] Ana silah switch mekanizması — slot 5'te per-tip Satın Al / Seç / Upgrade butonları
- [x] Boost sistemi — Kalkan/Silah boost toggle, BoostHUD, çarpan efektleri

---

## Sıradaki Adımlar (öncelik sırasıyla)

- [ ] Düşman çeşitleri — zırhlı (kinetike dirençli), kalkan botu (lazere dirençli), avcı/bomber
- [ ] Otomatik turretler — slot'a kurulunca en yakın düşmana ateş, enerji tüketir
- [ ] Toplayıcı gemiler + kaynak toplama sistemi
- [ ] Stat upgrade sistemi — komponent başına %'lik stat artışları (damage, HP, fire rate vb.), birden fazla komponent tipine uygulanabilir genel yapı
- [ ] Bölüm sistemi (8–10 bölüm, wave yapısı, bölüm arası geçiş)
- [ ] Boss taşıyıcı gemi
- [ ] Point defence turretleri — küçük/hızlı hedeflere odaklı otomatik turret
- [ ] Mobil UI
- [ ] Ses efektleri
- [ ] Gerçek sprite'lar — görsel iyileştirme