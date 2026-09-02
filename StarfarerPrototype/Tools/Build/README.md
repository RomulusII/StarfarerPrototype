# Build araçları

Android build'i bu makinede Unity'nin kendi yolundan **çalışmaz**. Sebep projede
değil, makinede; scriptler tek bir ortam değişkenini düzeltmek için var.

| Dosya | İşi |
|---|---|
| `_env.cmd` | Ortak kurulum — proje kökü, Unity yolu, TEMP düzeltmesi. Doğrudan çalıştırılmaz. |
| `build-android.cmd` | Editör kapalıyken APK üretir. Argümansız release, `dev` argümanıyla development. |
| `unity-editor.cmd` | Editörü düzeltme uygulanmış halde açar — Build & Run'ın çalışması için. |

Unity sürümü `ProjectSettings/ProjectVersion.txt`'ten okunur, yani Unity
yükseltilince scriptleri güncellemek gerekmez. Unity başka bir yerdeyse
`UNITY_EXE` ortam değişkeniyle gösterilebilir.

---

## Sorun: "Unable to establish loopback connection"

Gradle adımı bu hatayla düşer. IL2CPP derlemesi başarıyla biter, `libil2cpp.so`
üretilir; iş yalnızca paketlemede kalır.

Mesaj yanıltıcı. Gerçek sebep sarmalanmış istisnadadır:

```
Caused by: java.net.SocketException: Invalid argument: connect
    at sun.nio.ch.UnixDomainSockets.connect0(Native Method)
    at sun.nio.ch.PipeImpl$Initializer$LoopbackConnector.run(PipeImpl.java:133)
```

JDK 17 Windows'ta `Pipe.open()` ve `Selector.open()` için **AF_UNIX** soketi
kullanır ve socket dosyasını geçici dizinde açar. Bu makinede
`AppData\Local\Temp` altında AF_UNIX soketi **açılamıyor**. Gradle istemcisi
daemon'a bağlanırken non-blocking IO'ya geçtiği için tam orada patlar.

### Nasıl daraltıldı

| Test | Sonuç |
|---|---|
| Blocking `ServerSocket` + `Socket` loopback | **çalışıyor** — genel bir firewall engeli yok |
| Winsock katalogu (`netsh winsock show catalog`) | temiz, hepsi Microsoft `mswsock.dll` — üçüncü parti LSP yok |
| `PipeImpl` el sıkışmasının NIO ile elle taklidi | **çalışıyor** — bind/connect/accept/secret hepsi tamam |
| `Pipe.open()` / `Selector.open()` | **başarısız** |
| `-Djava.net.preferIPv4Stack=true` | etkisiz |
| `--no-daemon` | etkisiz (`org.gradle.jvmargs` yüzünden yine fork ediyor) |
| `TEMP` başka bir dizine alınmış | **çalışıyor** |

Kısa 8.3 yol formu (`AKIN~1.AYA`) suçlu değil: aynı dizin uzun formda da
başarısız, başka bir dizin her iki formda da başarılı.

### Neden `JAVA_TOOL_OPTIONS` değil

`-Djdk.net.unixdomain.tmpdir=...` sorunu çözer ama **Unity bu değişkeni
java'ya geçirmeden eler.** Build logundaki 188 değişkenlik ortam dökümünde
`JAVA_TOOL_OPTIONS` yoktur; aynı dökümde `TEMP` ve kabuktan gelen diğer
değişkenler vardır — yani ortam sonuna kadar taşınır, spesifik olarak o
değişken süzülür. Muhtemelen JVM'in stderr'e bastığı `Picked up
JAVA_TOOL_OPTIONS:` satırı Gradle çıktı ayrıştırmasını bozduğu için.

Bu yüzden düzeltme `TEMP`/`TMP` üzerinden yapılır: AF_UNIX'in varsayılan dizini
`java.io.tmpdir`'dir, o da `TEMP`'ten gelir. Hiçbir JVM bayrağı gerekmez ve
Unity'nin elemesine takılmaz.

---

## İkinci sorun: kurumsal TLS (Zscaler)

Gradle bağımlılık indirirken:

```
PKIX path building failed: unable to find valid certification path to requested target
```

Ağ TLS'i açıp yeniden imzalıyor:

```
issuer: CN=Zscaler Intermediate Root CA (zscalerthree.net)
```

JDK kendi CA deposunu kullandığı için Zscaler kökünü tanımaz. Çözüm, JDK'yı
Windows'un zaten güvendiği depoya yönlendirmektir — doğrulamayı **kapatmaz**:

```
-Djavax.net.ssl.trustStoreType=Windows-ROOT
```

Bu scriptlerde **yok**, çünkü yalnızca bağımlılıklar önbellekte değilken gerekir
(`~/.gradle/caches`) ve Windows'a özgüdür. Gerekirse Gradle'ı elle çalıştırırken
`-Dorg.gradle.jvmargs` içine eklenir.

**Oyunu da ilgilendirir:** bu ağdaki bir PC build'i `BalanceUploader` ile log
gönderirken aynı duvara çarpabilir. Çarparsa veri kaybolmaz — gönderim
başarısız olur, dosya diskte kalır ve `BalanceUploader.UploadPending()` onu bir
sonraki açılışta gönderir.
