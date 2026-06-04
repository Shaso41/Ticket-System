# 🎫 Ticket (Destek Talebi) Yönetim Sistemi

Bu proje, ASP.NET Core MVC framework'ü kullanılarak geliştirilmiş modern ve responsive bir **Destek Talebi (Ticket) Yönetim Sistemi** uygulamasıdır. Proje veri saklama katmanı olarak Entity Framework Core ve SQLite kullanmaktadır.

---

## 🚀 Özellikler

- **🔐 Kullanıcı Kimlik Doğrulama & Yetkilendirme:** 
  - Kayıt olma, Giriş ve Çıkış yapma işlemleri.
  - Rol tabanlı yetki kontrolü (`Admin`, `Support`, `User`).
- **🎫 Bilet (Ticket) Yönetimi:**
  - Bilet oluşturma, detayları görüntüleme ve durum güncelleme (Açık, İşlemde, Çözüldü, Kapatıldı).
  - Öncelik seviyeleri (`Low`, `Medium`, `High`, `Critical`) ve kategoriler.
  - Dosya eki yükleme desteği (Görseller, PDF'ler vb. için güvenli dosya saklama).
- **💬 Gerçek Zamanlı Mesajlaşma (Bilet İçi Sohbet):**
  - Müşteri ile destek ekibi arasında bilet üzerinden mesajlaşma.
  - Dosya eki paylaşabilme özelliği.
- **🕒 Otomatik Kapatma Servisi (Background Worker):**
  - Çözüldü (`Resolved`) durumunda olan ve 24 saattir işlem görmeyen biletleri arka planda otomatik olarak kapatan servis.
- **🛡️ Güvenlik:**
  - Şifreler PBKDF2 (SHA256) algoritması ile güvenli bir şekilde hash'lenerek saklanır.
  - XSS saldırılarına karşı HTML Sanitizer entegrasyonu mevcuttur.

---

## 🛠️ Teknolojiler ve Altyapı

- **Backend:** .NET 9.0 (ASP.NET Core MVC)
- **Veritabanı Katmanı (ORM):** Entity Framework Core (EF Core)
- **Veritabanı:** SQLite (`ticket.db`)
- **Arayüz Tasarımı:** TailwindCSS (CDN) & Vanilla CSS
- **Arka Plan İşleri:** IHostedService (BackgroundService)

---

## ⚙️ Kurulum ve Çalıştırma

### 1. Gereksinimler
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) bilgisayarınızda kurulu olmalıdır.

### 2. Uygulamayı Çalıştırma
Proje dizininde terminali açıp aşağıdaki komutu çalıştırarak uygulamayı başlatabilirsiniz:

```bash
dotnet run
```

Uygulama varsayılan olarak `http://localhost:5000` (veya HTTPS için `https://localhost:5001`) adresinde çalışacaktır.

### 3. Veritabanı Güncellemeleri (EF Core Migrations)
Veritabanı şemasında bir değişiklik yaptığınızda migration oluşturmak ve uygulamak için:

```bash
# Yeni bir migration oluşturma
dotnet ef migrations add <MigrationAdi>

# Veritabanını güncelleme
dotnet ef database update
```

---

## 👥 Kullanıcı Rolleri ve Yönetimi

Sistemde 3 temel rol tanımlıdır:
1. **User (Müşteri):** Sadece kendi biletlerini oluşturabilir, görüntüleyebilir ve kendi biletlerine mesaj yazabilir.
2. **Support (Destek Ekibi):** Tüm biletleri görüntüleyebilir, biletlerin durumunu/atanan temsilcisini güncelleyebilir ve mesaj yazabilir.
3. **Admin (Yönetici):** Sistemdeki tüm yetkilere sahiptir.

### 🔑 İlk Kullanıcının Rolünü Admin Yapma
Güvenlik nedeniyle kayıt sayfasından oluşturulan tüm hesaplar varsayılan olarak `User` rolüyle başlar. Kendinizi `Admin` yapmak için uygulamanız kapalıyken terminalden şu SQLite komutunu çalıştırabilirsiniz:

```bash
sqlite3 ticket.db "UPDATE Users SET Role = 'Admin' WHERE Username = 'KULLANICI_ADINIZ';"
```

---

## 📂 Proje Klasör Yapısı

- 📁 **Controllers**: İstekleri karşılayan ve iş mantığını yöneten denetleyiciler.
- 📁 **Data**: EF Core `AppDbContext` sınıfı ve veritabanı konfigürasyonları.
- 📁 **Models**: Veritabanı tablolarına karşılık gelen veri modelleri ve DTO'lar.
- 📁 **Views**: Razor tabanlı kullanıcı arayüzü sayfaları.
- 📁 **Jobs**: Otomatik bilet kapatma gibi arka plan servisleri.
- 📁 **Utils**: Şifre hash'leme ve güvenlik temizleyicileri gibi yardımcı araçlar.
- 📁 **wwwroot**: CSS, Javascript ve yüklenen bilet eklerinin (`uploads/`) tutulduğu statik klasör.
