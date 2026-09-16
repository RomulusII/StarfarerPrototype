// Unity -> tarayıcı sayfası köprüsü. Karşı tarafı: Assets/Scripts/UI/WebChrome.cs
// ve Assets/WebGLTemplates/Starfarer/index.html (window.starfarerSetChrome).
//
// Sayfa fonksiyonu tanımlamadıysa (başka bir şablonla alınmış build) sessizce
// hiçbir şey yapmaz: düğmesi olmayan bir sayfada gizlenecek düğme de yoktur.
mergeInto(LibraryManager.library, {
  Starfarer_SetWebChrome: function (visible) {
    if (typeof window.starfarerSetChrome === "function")
      window.starfarerSetChrome(visible !== 0);
  }
});
