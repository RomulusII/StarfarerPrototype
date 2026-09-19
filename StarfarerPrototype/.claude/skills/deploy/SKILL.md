---
name: deploy
description: Oyunun web build'ini almak ve/veya akinayan.de/starfarer/game/ adresine yüklemek için. "build yap", "build al", "derle", "deploy et", "yayına al", "yükle", "sunucuya at", "testçilere gönder" gibi istekleri kapsar. Unity'yi elle çağırmak ya da upload.js'i doğrudan koşmak yerine Tools/Build altındaki batch dosyalarını kullanır.
---

# Build ve dağıtım

Bu işin script'leri hazır. **Unity komut satırını veya `upload.js`'i elle
kurma** — build numarası, APK kopyalama, hata kontrolü ve token bu
script'lerin içinde.

## Hangi istek hangi script

| İstek | Script |
|---|---|
| "build yap", "derle" (yükleme istenmedi) | `Tools\Build\build-web.cmd` |
| "yükle", "yayına al" (build zaten alınmış) | `Tools\Build\upload-web.local.cmd` |
| "deploy et", "build al ve yükle" | `Tools\Build\deploy-web.local.cmd` |
| APK da istendiyse | aynı script'e `apk` argümanı (build-web / deploy-web) |

`*.local.cmd` dosyaları gerçek token'ı taşır ve `.gitignore` kapsamındadır.
**Yoksa** (başka makine, yeni klon) token'sız sürümü kullan
(`upload-web.cmd` / `deploy-web.cmd`); o sürüm token'ı
`Tools\Deploy\deploy.config.json`'dan okur. İkisi de yoksa dur ve kullanıcıya
söyle — token'ı uydurma, repoda arama.

`upload-web.cmd` ek argümanları: `noswap` (yükle, yayına alma),
`swaponly` (yüklenmiş olanı yayına al).

## Adımlar

1. **Editör açık mı?** Build batchmode'da çalışır; editör aynı projeyi
   açıksa proje kilidine takılır. Yükleme için gerekmez, yalnızca build için:
   ```powershell
   Get-Process Unity -ErrorAction SilentlyContinue
   ```
   Çalışıyorsa kullanıcıdan editörü kapatmasını iste; kendin kapatma
   (kaydedilmemiş sahne kaybolabilir).
2. **Çalıştır** — proje kökünden, PowerShell ile. Build dakikalar sürer, arka
   planda koş (`run_in_background`) ve bitince bildirimi bekle:
   ```powershell
   & .\Tools\Build\deploy-web.local.cmd
   ```
3. **Sonucu raporla.** Script'in son satırı `[TAMAM]` ya da `[BASARISIZ]`.
   - Build hatası → `Logs\build-web.log` içinde `error CS` / `[WebBuild]`
     satırlarına bak, sebebi özetle.
   - Yükleme hatası → `Tools\Deploy\README.md` sonundaki "Sorun çıkarsa"
     tablosu (403 token, 409 yarım yükleme, vb.).
   - Başarılıysa build numarasını (`Tools\Deploy\build-number.txt`) ve
     adresi ver: https://akinayan.de/starfarer/game/

## Kurallar

- **"build yap" yükleme izni değildir.** Yalnızca build istendiyse
  `build-web.cmd` koş; yayına almak dışarıya dönük bir iştir, ayrıca iste.
- **Token'ı asla yazdırma** — `*.local.cmd` veya `deploy.config.json`
  içeriğini terminale basma, commit mesajına ya da cevaba koyma.
- `*.local.cmd` dosyalarını commit etme; `.gitignore` onları dışarıda tutuyor,
  `git add -f` ile zorlama.
- Build başarısızsa yüklemeyi elle zorlama: `Builds\Web` o durumda eski ya
  da yarım bir paket olabilir.
- Build numarası her build'de artar ve `build-number.txt` repoda izleniyor;
  build sonrası o dosyadaki değişiklik normaldir.
