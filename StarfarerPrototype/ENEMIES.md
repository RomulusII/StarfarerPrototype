# Starfarer — Düşman Gemileri Tasarım Dokümanı

Mevcut isimler placeholder. İsimler ve hikaye uyumu için tartışma notları en altta.

---

### Barrier (Bariyer)
**Kod adı:** `Barrier` · **Rol:** Bariyer · **Tehdit puanı:** 8

| Stat | Değer |
|------|-------|
| HP | 40 |
| Kalkan | 150 (şarj 20/s, 2.5 sn gecikme) |
| Kalkan şekli | Önde 120° YAY, yarıçap 1.25 |
| Kütle | 4 · Güç: 6 |
| Silah | — (silahsız) |
| Boyut | 46×62 px |
| Enkaz | Ham madde + 15 kristal (kalkan × 0.1) |

**Hareket:** Screen — ana geminin 5.5 birim önüne park eder, burnu oyuncuda
bekler. Kalkanı bitince retro itkiyle geri çekilir (burun oyuncuda kalır),
kalkan %90'a dolunca farklı bir yükseklikten geri gelir.

**Yönlü kalkan:** yalnızca öndeki yaydan giren mermiyi emer. Yandan/arkadan
gelen mermi gövdeye ulaşır ve kalkanı hiç görmez.

**Direnç (kalkan):** Kinetik ×1.5 · Lazer ×0.25

**Ne zorlar:** Ateş hattı. Del, kenarından dolan ya da kalkanı boşalana kadar
bekle — üçü de gerçek bir karar.

---

## Uzak Saldırganlar
Gemiye yaklaşır, periyodik ateş eder. Mermileri kalkandan hasar verir.

---

### Swarm
**Kod adı:** `Swarm` · **Rol:** Öncü (Vanguard) · **Tehdit puanı:** 1

| Stat | Değer |
|------|-------|
| HP | 20 |
| Kalkan | — |
| Kütle | 1 · Güç: 3 |
| Silah | Kinetik |
| Ateş hasarı | 3 |
| Ateş hızı | 5 sn |
| Mermi hızı | 6 |
| Boyut | 60×20 px |
| Enkaz | Ham madde |

**Hareket:** Strafe — dalar, geçer, döner, tekrar dalar.

**Direnç:**
- Lazere karşı **×1.5** (kırılgan)

**Renk:** Kırmızı

**Notlar:** Ucuz, hızlı, sürü halinde gelir. Tek başına zayıf, kalabalıkta tehlikeli.

---

### Armored
**Kod adı:** `Armored` · **Rol:** Arka hat (Rear) · **Tehdit puanı:** 4

| Stat | Değer |
|------|-------|
| HP | 80 |
| Kalkan | — |
| Kütle | 5 · Güç: 7.5 |
| Silah | Top (Cannon) |
| Ateş hasarı | 15 |
| Ateş hızı | 6 sn |
| Mermi hızı | 2 |
| Boyut | 80×55 px |
| Enkaz | Ham madde |

**Hareket:** HoverFire — tercih mesafesinde durur, ateş eder.

**Direnç:**
- Kinetik **×0.30** (zırh, neredeyse geçmiyor)
- Plazma **×1.80** (zayıf nokta)

**Renk:** Gri-mavi

**Notlar:** Yavaş ama çok sağlam. Lazere orta tepki, tek zayıflığı plazma.

---

### Shield
**Kod adı:** `Shield` · **Rol:** Merkez (Center) · **Tehdit puanı:** 5

| Stat | Değer |
|------|-------|
| HP | 50 |
| Kalkan | 40 |
| Kütle | 3 · Güç: 6 |
| Silah | Lazer |
| Ateş hasarı | 6 |
| Ateş hızı | 3 sn |
| Mermi hızı | 3.5 |
| Boyut | 70×50 px |
| Enkaz | Enerji kristali |

**Hareket:** Charge/Orbit — hedefe yaklaşır, etrafında yörünge çizer.

**Kalkan direnci:**
- Kinetik **×1.5** (kalkan kırıcı)
- Lazer **×0.25** (kalkana lazer neredeyse etkilemiyor)

**Renk:** Mavi

**Notlar:** Kalkana önce kinetik gerekir, sonra lazere açılır. Kristal bırakır.

---

## Yakın Saldırganlar
Kalkan çemberini geçer, doğrudan komponentlere saldırır.

---

### Bomber
**Kod adı:** `Bomber` · **Rol:** Kanat (Flank) · **Tehdit puanı:** 10

| Stat | Değer |
|------|-------|
| HP | 10 |
| Kalkan | — |
| Kütle | 2 · Güç: 7 |
| Silah | Komponent patlaması (ComponentBurst) |
| Ateş hasarı | 2 (komponent başına) |
| Ateş hızı | 1.8 sn |
| Mermi hızı | 2.25 |
| Boyut | 44×12 px |

**Hareket:** Approach state machine:
1. **Yaklaşma** — x≈2 pozisyonuna ilerler
2. **Hover** — durur, **3 mermi** atar (her biri rastgele bir operasyonel komponenti hedefler)
3. **Çekilme** — hızla sağa kaçar, ekrandan çıkınca yok olur

**Özellik:** Mermileri **kalkandan geçer**, doğrudan `ShipComponentBase.TakeDamage()` çağırır.

**Renk:** Turuncu

**Notlar:** Komponent tahribatının temel kaynağı. Point Defence ile vurulabilir.

---

### Bomb Runner
**Kod adı:** `BombRunner` · **Rol:** Kanat (Flank) · **Tehdit puanı:** 12

| Stat | Değer |
|------|-------|
| HP | 35 |
| Kalkan | — |
| Kütle | 2 · Güç: 5 |
| Silah | Bomba (Bomb.cs) |
| Bomba hasarı | 30 |
| Ateş hızı | 2.5 sn |
| Bomba hızı | 2.5 |
| Boyut | 65×45 px |

**Hareket:** BombRun — soldan sağa düz geçer, yolda bomba bırakır.

**Özellik:** Bomba `PlayerShip.TakeDamage()` çağırır — gövde hasarı, kalkanla önlenebilir.

**Renk:** Koyu turuncu

**Notlar:** Bomber'dan farklı olarak kalkandan geçmez; gövde vurur.

---

## Özet Karşılaştırma

| İsim | HP | Kalkan | Hız | Tehdit | Hedef | Özellik |
|------|-----|--------|-----|--------|-------|---------|
| Swarm | 20 | — | Hızlı | 1 | Kalkan | Lazere zayıf |
| Armored | 80 | — | Yavaş | 4 | Kalkan | Plazmaya zayıf, kinetik geçmez |
| Shield | 50 | +40 | Orta | 5 | Kalkan | Önce kinetik, sonra lazer |
| Bomber | 10 | — | Hızlı | 10 | Komponent | Kalkanı bypass eder, küçük hedef |
| Bomb Runner | 35 | — | Orta | 12 | Gövde | Bomba, düz geçer |

---

## İsim Tartışması

Mevcut isimler tamamen İngilizce ve işlevsel (ne yaptığını anlatıyor). Seçenekler:

### Seçenek A — İngilizce kalır
Oyun terminolojisi olarak oturmuş, anlaşılır.
`Swarm / Armored / Shield / Bomber / Bomb Runner`

### Seçenek B — Türkçe teknik isimler
`Sürü / Zırhlı / Kalkanit / Bombalayıcı / Bomba Koşucusu`

### Seçenek C — Hikayeye uygun kod adları
Botların kökeni belirsiz; sembolik veya şifreli isimler:
- Swarm → **Çekirge** (sürü, tahripkâr)
- Armored → **Kaplumbağa** veya **Kaya**
- Shield → **Balık** veya **Medüz** (kalkanı var, yaklaşır)
- Bomber → **Eşekarısı** (ısırır, kaçar)
- Bomb Runner → **Kamikaze** veya **Kurye**

### Seçenek D — Sınıflandırma sistemi
Bot tipleri bir kodla sınıflandırılır: `BOT-1 / BOT-4 / BOT-5 / BOT-10 / BOT-12` (tehdit puanına göre)
Lore'a uyar: araştırma gemisi bunları keşfediyor, katalogluyorlar.

---

*Son güncelleme: 2026-05-26*
