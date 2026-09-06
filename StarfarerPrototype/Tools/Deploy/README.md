# Dağıtım

Tarayıcı build'ini üretip `https://akinayan.de/starfarer/game/` adresinde
yayına alır. Tek komut:

```
Tools\Build\deploy-web.cmd
```

| Dosya | İşi |
|---|---|
| `Tools/Build/deploy-web.cmd` | Build numarasını artırır, web build'ini alır, APK'yı yanına kopyalar, yükler. |
| `Tools/Deploy/upload.js` | Dosyaları HTTPS ile sunucuya taşır ve takası tetikler. |
| `Tools/Deploy/server/deploy.php` | Sunucudaki uç. `httpdocs/Starfarer/deploy.php` olarak durur. |
| `Tools/Deploy/deploy.config.json` | Uç adresi ve TOKEN. `.gitignore` kapsamında, repoya girmez. |
| `Tools/Deploy/build-number.txt` | Artan build sayacı. Üretilebilir, repoya girmez. |

---

## Neden FTP değil

Kurumsal ağdaki Zscaler, FTP'yi proxy'liyor ve şifrelemeden **önce** giriş
bilgisi istiyor:

```
220 Zscaler/6.2: USER expected (Unix syntax)
AUTH TLS -> 503 Login with USER first
```

Sunucu (IIS FTP) `AUTH TLS`, `PROT P` destekliyor — sorun sunucuda değil,
aradaki proxy'de. Implicit FTPS (port 990) da kapalı. O yoldan dağıtmak, FTP
parolasını proxy'ye ve aradaki her durağa açık metin vermek demekti.

Bu uç 443'ten geçer. Ağ TLS'i açıp yeniden imzalasa bile taşınan şey parola
değil, **yalnızca bu dizine dosya yazabilen** tek amaçlı bir token.

FTP bilgileri `deploy.config.json` içinde `ftp` altında duruyor: başka bir
ağdan (telefon hotspot'u) dağıtım gerekirse hazır.

---

## Kurulum (bir kez)

1. **Token üret:**
   ```
   node -e "console.log(require('crypto').randomBytes(24).toString('base64url'))"
   ```
2. `Tools/Deploy/server/deploy.php` içindeki `DEPLOY_TOKEN`'a yaz.
3. Dosyayı sunucuya koy: `httpdocs\Starfarer\deploy.php`
   — oyunun **üst** klasörüne. Oyunun kendi klasörüne konsaydı, her dağıtımda
   `game` takas edildiği için kendi altındaki zemini çekerdi.
4. Aynı token'ı `deploy.config.json` içindeki `deployToken` alanına yaz.
5. Doğrula — `pong` görüyorsan dosya parse edilmiş demektir:
   ```
   curl -s "https://akinayan.de/starfarer/deploy.php?ping=1"
   ```

---

## Akış

```
reset  →  game-next/ temizlenir
dosya  →  parça parça game-next/ altına yazılır
swap   →  game → game-YYYYAAGG-SSDDSS,  game-next → game
```

**Neden takas:** yükleme sürerken kimse yarım build'e düşmesin. Takas anına
kadar yayındaki sürüm eskisidir; yayına alma tek bir `rename`.

**Geri dönüş:** son iki sürüm `game-YYYYAAGG-SSDDSS` adıyla duruyor. Geri
almak, sunucuda iki rename (Plesk dosya yöneticisi yeter).

**Neden parçalı yükleme:** PHP'nin `post_max_size`'ı (Plesk'te çoğu zaman
8 MB) tek parça `.data` dosyasını reddederdi ve hata "çok büyük" diye değil
**boş gövde** olarak görünürdü. İstemci sınırı `?ping=1`'den okuyup parçayı
ona göre seçiyor.

---

## Güvenlik sınırı

Sınır **token**. Token'ı olan bu klasöre dosya yazabilir; amaç zaten bu.
`deploy.php`'deki kuralların işi, token sızarsa bunun **çalıştırılabilir koda**
dönüşmesini engellemek:

| Kural | Ne engeller |
|---|---|
| Uzantı beyaz listesi | `.php`, `.aspx`, `.exe` yazılamaz — listede yoksa yok |
| `web.config` yalnızca tam bu adla | Token sızıntısı sunucu yapılandırması yetkisine dönüşmesin |
| Segment süzgeci + derinlik 4 | `..` ve dizin ayıracıyla klasör dışına çıkma |
| Silme yalnızca kalıba uyan dizinde | `realpath` ile `BASE_DIR` altı doğrulanır |
| Ofset = mevcut boyut | Karışmış parçalar sessizce bozuk dosya üretmesin |

Sınanmış (sahte sunucuya karşı, aynı kurallarla): `evil.php` → 400,
`../../evil.html` → 400, `..%2Fevil.html` → 400, `sub/other.config` → 400,
5 seviye derin yol → 400, `ok.js` → 200, token'sız → 403. 13 MB'lık gerçek
build çok parçalı yüklendi ve sha256'ları kaynakla birebir aynı çıktı.

---

## Sorun çıkarsa

| Belirti | Sebep |
|---|---|
| `?ping=1` boş sayfa / 500 | PHP parse hatası — sunucunun kendi hata günlüğüne bak |
| `403 nope` | Token uyuşmuyor (`deploy.php` ↔ `deploy.config.json`) |
| `409 ofset uyusmuyor` | Yarım kalmış yükleme. `node Tools/Deploy/upload.js` baştan çalıştır (reset ile başlar) |
| `400 game-next icinde index.html yok` | Yükleme eksik kaldı; takas kasten yapılmadı |
| `500 eski surum tasinamadi` | `game` klasöründe dosya kilidi. Genelde geçici, tekrar dene |
| Testçi eski sürümü görüyor | Build numarası artmamış. `deploy-web.cmd` bunu kendisi yapar; elle build alındıysa `-sfBuild N` geç |
