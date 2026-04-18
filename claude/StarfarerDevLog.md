# Starfarer — Geliştirme Durumu (Dev Log)

## Bu Konuşmada Tamamlananlar

### Enerji Sistemi
- `EnergyBus` sahnede aktif, singleton
- `PlayerShip.Awake()`'te gizli `CoreGenerator` yaratılıyor (`productionAmount = 8f`)
- `EnergyBar` HUD — Canvas Screen Space Overlay, sol üst köşe, zoom'dan etkilenmiyor

### Kalkan Sistemi
- `ShieldGeneratorComponent` çalışıyor
- Kalkan tamamen bitince 10 saniyelik recharge delay (`rechargeDelayAfterDepletion = 10f`)
- Kalkan kısmen doluyken delay yok, normal şarj devam eder

### Upgrade Sistemi — Aşama 1 (Veri + Mantık)
- `ComponentType.cs` — enum: Generator, Shield, RepairUnit, Weapon
- `ResourceType.cs` — enum: RawMaterial, EnergyCrystal
- `ComponentDefinition.cs` — ScriptableObject, tier/cost/stats/upgradeTo
- `ShipLoadout.cs` — 10 slot, InstallComponent/SellComponent/UpgradeComponent
  - Slot 5 (OrtaSağ) = weapon slot, satılamaz
  - `InstallComponent(def, slotIndex, deductCost = true)`
  - `SellComponent(slotIndex, returnResources = true)`
- `ResourceInventory.cs` — TrySpend/Add/Get metodları eklendi

### Upgrade Sistemi — Aşama 2 (UI)
- `UpgradeUI.cs` — Canvas tabanlı, Tab ile aç/kapat
- Açılınca `Time.timeScale = 0`, `UpgradeUI.IsPaused = true`
- `UpgradeUI.Instance` singleton mevcut
- `WeaponController`, `WeaponMount`, `EnemySpawner`, `EnemyBot` — `IsPaused` kontrolü var

### World Space Slot Sistemi
- `SlotVisual.cs` — her slot geminin child'ı, daire sprite
- 10 slot pozisyonları:
  - Üst sıra (y=0.8f): (-1.5, 0.8), (0, 0.8), (1.5, 0.8)
  - Orta sıra (y=0f): (-1.5, 0), (-0.5, 0), (0.5, 0), (1.5, 0)
  - Alt sıra (y=-0.8f): (-1.5, -0.8), (0, -0.8), (1.5, -0.8)
- Boş slot = gri, dolu = yeşil, weapon slot = sarı
- `OnSlotClicked(slotIndex)` → şimdilik Debug.Log

### Kamera Zoom (Son yapılan — test edilmedi)
- `CameraController`'a `ZoomToShip()` ve `RestoreFromUpgrade()` eklendi
- `Time.unscaledDeltaTime` kullanılıyor (timeScale=0 uyumu)
- Upgrade açılınca gemiye smooth zoom, kapanınca geri dön

---

## Sıradaki Adımlar

1. **Kamera zoom testi** — çalışıyor mu kontrol et
2. **Slot tıklama popup'ı** — boşsa "build" seçenekleri, doluysa "upgrade/sat"
3. **ComponentDefinition asset'leri** — Inspector'da ScriptableObject'ler oluştur
4. **ResourceInventory UI** — ham madde + enerji kristali HUD'a ekle
5. **Silah tipleri** — Lazer/Plazma/Kinetik, enerji tüketimi

## Sonraki Büyük Sistemler
- Toplayıcı gemiler + kaynak toplama
- Düşman çeşitlendirme (zırhlı, kalkan, avcı/bomber)
- Bölüm sistemi
- Boss taşıyıcı gemi
- Mobil UI
- Ses efektleri
- Gerçek sprite'lar

---

## Önemli Kod Kuralları
- `FindFirstObjectByType` kullan (`FindObjectOfType` değil)
- New Input System (`UnityEngine.InputSystem`)
- Uniform scale (X=Y=Z)
- HUD elementleri → Canvas (Screen Space Overlay)
- World space obje barları → SpriteRenderer tabanlı
- `Time.unscaledDeltaTime` → pause sırasında çalışması gereken şeyler için