using UnityEngine;

/// <summary>
/// Ana silahın namlusundan çıkan silik nişan çizgisi.
///
/// NE GÖSTERİR: merminin gideceği YÖN. Ne gösterMEZ: düşmanı yakalayacağı
/// nokta. Bu ayrım kasıtlı ve oyunun asıl becerisi orada — mermi hızıyla
/// düşman hızını oyuncu kendisi hesaplar. Öngörü (lead) göstergesi ya da
/// hedef kilidi, o hesabı ortadan kaldırıp oyunu ucuzlatırdı.
///
/// ASIL FAYDASI GÖRÜNENDEN BÜYÜK: silah gövdenin merkezinden değil, yuvasından
/// ateş ediyor (bkz. <see cref="WeaponMount.BarrelLength"/>). Yani mermi
/// parmağın olduğu yerden değil, birkaç birim yandaki namludan çıkıyor. Yakında
/// fark küçük, uzakta belirgin: ölçülen %52'lik isabet oranının bir kısmının
/// "nereye nişan aldığımı sanıyorum" ile "namlu gerçekte nereye bakıyor"
/// arasındaki bu boşluktan gelmesi bekleniyor. Çizgi oyuncuya beceri
/// kazandırmıyor, YANILGIYI kaldırıyor.
///
/// Yalnızca nişan alınırken görünür. Sürekli duran bir çizgi hem ekranı
/// kalabalıklaştırır hem de uzayın karanlığını bozardı; ayrıca dokunmatikte
/// parmak zaten nişanın kendisi, yani "nişan alınıyor" ile "ekrana dokunuluyor"
/// aynı an.
///
/// Kurulumu <see cref="WeaponController"/> yapar (runtime kurulum deseni,
/// sahne değişikliği gerekmez).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class AimLine : MonoBehaviour
{
    /// <summary>
    /// Çizginin dünya cinsinden uzunluğu. Ekranı her zaman aşacak kadar uzun:
    /// nerede biteceği görünmesin, sönerek kaybolsun. Nereye kadar gittiğini
    /// gösteren keskin bir uç, menzil varmış gibi yanlış bir izlenim verirdi —
    /// mermilerin menzili yok, ömrü var.
    /// </summary>
    const float Uzunluk = 25f;

    /// <summary>Namlu dibindeki opaklık. Silik olması şart: bu bir yardımcı, oyunun kendisi değil.</summary>
    const float Opaklik = 0.22f;

    LineRenderer _lr;

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();

        _lr.useWorldSpace    = false;   // silahla birlikte dönsün
        _lr.positionCount    = 2;
        _lr.numCapVertices   = 0;
        _lr.material         = new Material(Shader.Find("Sprites/Default"));
        _lr.sortingOrder     = 15;      // mermilerin (20) altında

        // Renk namludan uca doğru söner. Tek renkli uzun bir çizgi, ekranın
        // yarısını kesen bir cetvel gibi görünüyordu.
        var mavi = new Color(0.55f, 0.75f, 1f);
        _lr.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(mavi, 0f), new GradientColorKey(mavi, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(Opaklik, 0f), new GradientAlphaKey(0f, 1f) },
        };

        _lr.enabled = false;
    }

    void LateUpdate()
    {
        // LateUpdate: silahın dönüşü Update'te hesaplanıyor (WeaponMount).
        // Aynı karede önce çizseydik çizgi namludan bir kare geride kalırdı ve
        // hızlı nişan değişiminde gözle görülür biçimde kayardı.
        bool nisanAliniyor = !PointerInput.Locked && PointerInput.TryPosition(out _);
        _lr.enabled = nisanAliniyor;
        if (!nisanAliniyor) return;

        // Kalınlık kadrajla ölçeklenir: sabit dünya kalınlığı, zoom-out'ta
        // incelip kaybolur, telefonun dar kadrajında kalın bir şerit olurdu.
        var cam = Camera.main;
        float kalinlik = (cam != null ? cam.orthographicSize : 5f) * 0.010f;
        _lr.startWidth = kalinlik;
        _lr.endWidth   = kalinlik;

        // Yerel uzayda +y namlu yönü: mermiler de tam buradan, bu yönde çıkıyor
        // (bkz. WeaponController.SpawnBullet). Aynı iki sayıdan türemesi şart —
        // ayrı hesaplansaydı çizgi ile merminin yolu sessizce ayrışabilirdi.
        _lr.SetPosition(0, new Vector3(0f, WeaponMount.BarrelLength, 0f));
        _lr.SetPosition(1, new Vector3(0f, WeaponMount.BarrelLength + Uzunluk, 0f));
    }
}
