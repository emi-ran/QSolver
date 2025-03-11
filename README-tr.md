# QSolver

Türkçe | [English](README.md)

QSolver, ekranınızdaki soruları yakalamak ve işlemek için tasarlanmış bir Windows uygulamasıdır. Sistem tepsisinde durarak ihtiyaç duyduğunuzda hemen kullanıma hazırdır.

Sürüm: 1.2.0

## Özellikler

- **Ekran Yakalama**: Basit bir tıkla ve sürükle arayüzü ile ekranınızın istediğiniz bölgesini kolayca yakalayın
- **Akıllı İşleme**: Yakalanan soruların hızlı analizi ve anında sonuç
- **Sistem Tepsisi Entegrasyonu**: Her zaman erişilebilir ama asla yolunuza çıkmaz
- **Modern Arayüz**: Temiz ve sezgisel arayüz, akıcı animasyonlar ve koyu tema
- **Soru Düzenleme**: Metin formatını koruyan dahili soru düzenleyici
- **Çözüm Adımları**: Her soru için detaylı çözüm adımları
- **Geçici Depolama**: Yakalamaları otomatik olarak geçici bir klasörde saklar
- **Özel Tema**: Yuvarlak köşeli ve yumuşak geçişli modern koyu tema
- **API Anahtarı Yönetimi**: Farklı servisler için API anahtarlarınızı kolayca yönetin

## Gereksinimler

- Windows İşletim Sistemi
- .NET 8.0 veya üzeri
- Visual Studio 2022 (geliştirme için)
- Yapay zeka servisleri için internet bağlantısı

## Kurulum

1. Releases sayfasından en son sürümü indirin
2. Dosyaları istediğiniz konuma çıkartın
3. `QSolver.exe`'yi çalıştırın
4. Sistem tepsisi menüsünden API anahtarlarınızı yapılandırın

## Kullanım

1. Sistem tepsisindeki QSolver simgesine tıklayın
2. "Soru Seç" seçeneğini seçin
3. Sorunun bulunduğu bölgeyi seçmek için tıklayıp sürükleyin
4. İşleme animasyonunun tamamlanmasını bekleyin
5. Sonucu ve çözüm adımlarını görüntüleyin
6. Gerekirse dahili düzenleyici ile soruyu düzenleyin
7. İşiniz bittiğinde "Onayla" butonuna tıklayın

## Geliştirme

Projeyi derlemek için:

```bash
dotnet restore
dotnet build
```

Geliştirme modunda çalıştırmak için:

```bash
dotnet run
```

## Katkıda Bulunma

Katkılarınızı bekliyoruz! Dilediğiniz zaman Pull Request gönderebilirsiniz.

## Lisans

Bu proje MIT Lisansı ile lisanslanmıştır - detaylar için LICENSE dosyasına bakınız.

## Özel Teşekkür

Bu projenin fikrini oluşturan ve var olma sebebi olan Bayazıt S.'ye özel teşekkürlerimi sunarım. O olmasaydı böyle bir programı yapmak benim aklıma gelmeyecekti bile. Kendisinin fikri bulmasıyla bu projeyi yapmaya başladım.
