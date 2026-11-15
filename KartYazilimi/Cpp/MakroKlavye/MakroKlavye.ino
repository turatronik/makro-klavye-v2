// =========================================================
// MAKRO KLAVYE v2 - ana program (C++, Arduino, RP2040-Zero)
// =========================================================
// CircuitPython 1.24 sürümünün C++'a çevrilmiş hâli: aynı özellikler, aynı ekran, aynı ışıklar,
// MakroKeyboardDisplay uygulamasıyla aynı haberleşme (uygulamada değişiklik gerekmez).
// Modlar ve ayarlar CircuitPython'un kalıcı belleğiyle aynı yerde durur (geçişte kaybolmaz).
//
// Sağ tekerlek : çevir = mod değiştir (ya da modun tekerlek görevi), bas = ekran sayfası, uzun bas = ışık aç/kapa
// Sol tekerlek : çevir = ses, bas = sessiz, uzun bas = oynat / duraklat, basılı tutup çevir = sonraki / önceki şarkı
// İki tekerleğe birlikte 1 sn bas: tuş kilidi
//
// Derleme (Arduino IDE): Kart "Waveshare RP2040 Zero" (Earle Philhower arduino-pico), USB Stack "Pico SDK",
// Flash Size "2MB (no FS)". Kütüphaneler: Adafruit NeoPixel, ArduinoJson (7.x).

// Programın tamamı ana.cpp ve diğer .cpp dosyalarında (Arduino IDE bu dosyayı ve hepsini birlikte derler).
