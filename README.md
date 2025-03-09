# ParkIRC - Sistem Manajemen Parkir

Sistem manajemen parkir modern dengan fitur deteksi kendaraan otomatis menggunakan kamera IP, pencetakan tiket, dan manajemen parkir yang komprehensif.

## Fitur Utama

- Deteksi kendaraan otomatis menggunakan kamera IP
- Pencetakan tiket dan struk otomatis
- Manajemen parkir real-time
- Dashboard monitoring
- Sistem gate otomatis
- Manajemen operator dan shift
- Laporan dan analitik

## Teknologi

- ASP.NET Core 6.0
- Entity Framework Core
- SQLite Database
- EmguCV untuk pemrosesan gambar
- SignalR untuk real-time updates
- Bootstrap 5 untuk UI

## Persyaratan Sistem

- Windows 64-bit
- .NET 6.0 SDK
- Kamera IP yang kompatibel
- Printer thermal
- Minimal RAM 4GB
- Processor Intel i3/AMD Ryzen 3 atau lebih tinggi

## Instalasi

1. Clone repository:
```bash
git clone https://github.com/idiarso/APPCSharp.git
cd APPCSharp
```

2. Install dependencies:
```bash
dotnet restore
```

3. Update database:
```bash
dotnet ef database update
```

4. Jalankan aplikasi:
```bash
dotnet run
```

## Konfigurasi

1. Sesuaikan `appsettings.json` untuk:
   - Koneksi database
   - Pengaturan kamera IP
   - Konfigurasi printer
   - Pengaturan email

2. Default login:
   - Email: admin@parkingsystem.com
   - Password: Admin@123

## Penggunaan

1. Akses aplikasi di `http://localhost:5126`
2. Login menggunakan kredensial default
3. Konfigurasi pengaturan sistem
4. Mulai operasi parkir

## Lisensi

Copyright © 2024 ParkIRC. All rights reserved.
