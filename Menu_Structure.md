# Struktur Menu Aplikasi Parkir

## 1. Menu Utama (Dashboard)
- **Statistik Real-time**
  - Total Kendaraan Terparkir
  - Slot Tersedia
  - Pendapatan Hari Ini
  - Grafik Okupansi
  - Status Gate (Masuk/Keluar)

## 2. Menu Gate Masuk
- **Kendaraan Masuk**
  - Tombol Manual Gate
  - Input Plat Nomor
  - Pilih Jenis Kendaraan
  - Capture Foto
  - Cetak Tiket
  - Preview CCTV

- **Status Gate Masuk**
  - Indikator Gate (Terbuka/Tertutup)
  - Status Printer
  - Status Kamera

## 3. Menu Kasir
- **Pembayaran & Exit**
  - Scan Barcode/QR
    * Auto-detect scanner
    * Fallback ke input manual
    * Preview hasil scan
    * Validasi tiket
  - Input Manual Nomor Tiket
    * Untuk tiket yang rusak/tidak terbaca
    * Verifikasi dengan foto masuk
  - Tampil Info Kendaraan
    * Plat nomor
    * Jenis kendaraan
    * Waktu masuk
    * Foto kendaraan saat masuk
    * Durasi parkir
  - Hitung Biaya
    * Tarif dasar
    * Tarif per jam
    * Diskon (jika ada)
    * Total biaya
  - Proses Pembayaran
    * Input jumlah bayar
    * Hitung kembalian
    * Konfirmasi pembayaran
  - Cetak Struk Keluar
    * Struk pembayaran
    * Barcode keluar
    * Info durasi & biaya
  - Kontrol Gate
    * Auto-open setelah scan struk keluar
    * Timeout gate (30 detik)
    * Manual override

- **Transaksi Aktif**
  - Daftar Transaksi Hari Ini
    * Status pembayaran
    * Status keluar
    * Filter & Pencarian
  - Monitoring Gate Keluar
    * Status gate
    * Antrian keluar
    * Alert jika macet
  - Validasi Tiket
    * Cek keaslian tiket
    * Cek status pembayaran
    * Cek foto kendaraan

- **Quick Actions**
  - Scan Cepat (F2)
  - Buka Gate Manual (F3)
  - Tutup Gate Manual (F4)
  - Print Ulang Struk (F5)
  - Cancel Transaksi (F6)

## 4. Menu Laporan
- **Laporan Harian**
  - Total Kendaraan
  - Total Pendapatan
  - Breakdown per Jenis
  - Export PDF/Excel

- **Laporan Keuangan**
  - Pendapatan per Periode
  - Grafik Trend
  - Analisis Pendapatan
  - Export Data

- **Log Aktivitas**
  - Log Gate
  - Log Operator
  - Log Sistem

## 5. Menu Pengaturan
- **Manajemen User**
  - Tambah/Edit User
  - Set Role & Hak Akses
  - Reset Password

- **Konfigurasi Sistem**
  - Setting Printer
  - Setting Kamera
  - Setting Gate
  - Tarif Parkir
  - Jam Operasional

- **Database**
  - Backup Database
  - Restore Database
  - Hapus Data Lama

## 6. Menu Bantuan
- **Panduan**
  - Manual Penggunaan
  - Troubleshooting
  - FAQ

- **Informasi**
  - Tentang Aplikasi
  - Kontak Support
  - Lisensi

## 7. Menu Monitoring
- **CCTV**
  - Live View Gate Masuk
  - Live View Gate Keluar
  - Capture Image
  - Playback Recording

- **Status Perangkat**
  - Status Printer
  - Status Kamera
  - Status Gate
  - Status Server

## 8. Menu Shift & Operator
- **Manajemen Shift**
  - Set Jadwal Shift
  - Rotasi Operator
  - Absensi

- **Laporan Operator**
  - Kinerja Operator
  - Log Aktivitas
  - Rekap Shift

## 9. Menu Member
- **Manajemen Member**
  - Daftar Member
  - Kartu Member
  - Perpanjangan
  - Histori Member

- **Tarif Khusus**
  - Setting Tarif Member
  - Diskon & Promo
  - Voucher

## 10. Menu Utilitas
- **Tools**
  - Test Printer
  - Test Gate
  - Test Kamera
  - Kalibrasi Scanner

- **Maintenance**
  - Cleanup Database
  - Reset Counter
  - Update Sistem
  - Backup Settings 