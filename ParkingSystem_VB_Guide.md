# Panduan Belajar Sistem Parkir dengan Visual Basic

## A. Persiapan Project
1. Buat Project Windows Form baru di Visual Studio
2. Siapkan database (bisa menggunakan MS Access untuk pembelajaran)
3. Install komponen yang diperlukan:
   - Barcode/QR Scanner SDK
   - Printer Component
   - Serial Port Component (untuk kontrol gate)

## B. Struktur Database
```sql
-- Tabel Kendaraan
CREATE TABLE Kendaraan (
    ID_Kendaraan AUTOINCREMENT PRIMARY KEY,
    No_Plat VARCHAR(20),
    Jenis_Kendaraan VARCHAR(50),
    Waktu_Masuk DATETIME,
    Waktu_Keluar DATETIME,
    Status VARCHAR(20)
)

-- Tabel Tiket
CREATE TABLE Tiket (
    ID_Tiket AUTOINCREMENT PRIMARY KEY,
    Kode_Tiket VARCHAR(50),
    ID_Kendaraan INT,
    Waktu_Cetak DATETIME,
    Status VARCHAR(20)
)

-- Tabel Transaksi
CREATE TABLE Transaksi (
    ID_Transaksi AUTOINCREMENT PRIMARY KEY,
    ID_Tiket INT,
    Waktu_Bayar DATETIME,
    Durasi INT,
    Tarif DECIMAL(10,2),
    Status_Pembayaran VARCHAR(20)
)
```

## C. Form Utama (MainForm)
```vb
Public Class MainForm
    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Inisialisasi koneksi database
        InitializeDatabase()
        
        ' Setup port untuk gate control
        SetupSerialPort()
    End Sub
End Class
```

## D. Modul Gate Masuk (EntryGate)
```vb
Public Class EntryGate
    Private WithEvents serialPort As New SerialPort
    Private printer As New PrintDocument

    ' Event untuk tombol masuk manual
    Private Sub btnManualEntry_Click(sender As Object, e As EventArgs) Handles btnManualEntry.Click
        Try
            ' 1. Buka gate
            OpenGate()
            
            ' 2. Generate dan cetak tiket
            Dim ticketNumber = GenerateTicketNumber()
            PrintTicket(ticketNumber)
            
            ' 3. Simpan ke database
            SaveEntryRecord(ticketNumber)
            
        Catch ex As Exception
            MessageBox.Show("Error: " & ex.Message)
        End Try
    End Sub

    ' Fungsi untuk membuka gate
    Private Sub OpenGate()
        If serialPort.IsOpen Then
            ' Kirim sinyal ke microcontroller
            serialPort.Write("OPEN_GATE")
            ' Tunggu beberapa detik
            Threading.Thread.Sleep(2000)
            ' Tutup gate
            serialPort.Write("CLOSE_GATE")
        End If
    End Sub

    ' Generate nomor tiket
    Private Function GenerateTicketNumber() As String
        Return "TKT" & DateTime.Now.ToString("yyyyMMddHHmmss") & _
               New Random().Next(1000, 9999).ToString()
    End Function

    ' Cetak tiket
    Private Sub PrintTicket(ticketNumber As String)
        Try
            ' Setup printer
            printer.PrinterSettings.PrinterName = "Nama_Printer"
            
            ' Event handler untuk printing
            AddHandler printer.PrintPage, AddressOf PrintTicketHandler
            
            ' Mulai print
            printer.Print()
        Catch ex As Exception
            Throw New Exception("Gagal mencetak tiket: " & ex.Message)
        End Try
    End Sub

    ' Handler untuk printing
    Private Sub PrintTicketHandler(sender As Object, e As PrintPageEventArgs)
        Try
            ' Format tiket
            Dim font As New Font("Arial", 10)
            Dim brush As New SolidBrush(Color.Black)
            Dim y As Integer = 10
            
            ' Header
            e.Graphics.DrawString("TIKET PARKIR", New Font("Arial", 12, FontStyle.Bold), brush, 10, y)
            y += 30
            
            ' Informasi tiket
            e.Graphics.DrawString("No Tiket: " & ticketNumber, font, brush, 10, y)
            y += 20
            e.Graphics.DrawString("Tanggal: " & DateTime.Now.ToString(), font, brush, 10, y)
            y += 20
            
            ' Generate dan print barcode
            Dim barcode As New BarcodeLib.Barcode()
            barcode.IncludeLabel = True
            Dim barcodeImage = barcode.Encode(BarcodeLib.TYPE.CODE128, ticketNumber)
            e.Graphics.DrawImage(barcodeImage, 10, y)
            
        Catch ex As Exception
            MessageBox.Show("Error printing: " & ex.Message)
        End Try
    End Sub
End Class
```

## E. Modul Kasir (CashierForm)
```vb
Public Class CashierForm
    Private WithEvents barcodeScanner As New SerialPort
    Private printer As New PrintDocument

    ' Event ketika barcode di-scan
    Private Sub barcodeScanner_DataReceived(sender As Object, e As SerialDataReceivedEventArgs) _
    Handles barcodeScanner.DataReceived
        Try
            ' Baca data barcode
            Dim ticketNumber = barcodeScanner.ReadLine()
            
            ' Proses pembayaran
            ProcessPayment(ticketNumber)
            
        Catch ex As Exception
            MessageBox.Show("Error scanning: " & ex.Message)
        End Try
    End Sub

    ' Proses pembayaran
    Private Sub ProcessPayment(ticketNumber As String)
        Try
            ' 1. Ambil data kendaraan
            Dim vehicleData = GetVehicleData(ticketNumber)
            
            ' 2. Hitung durasi dan biaya
            Dim duration = CalculateDuration(vehicleData.EntryTime)
            Dim cost = CalculateCost(duration, vehicleData.VehicleType)
            
            ' 3. Tampilkan di form
            DisplayPaymentInfo(duration, cost)
            
        Catch ex As Exception
            MessageBox.Show("Error processing payment: " & ex.Message)
        End Try
    End Sub

    ' Event untuk tombol bayar
    Private Sub btnPay_Click(sender As Object, e As EventArgs) Handles btnPay.Click
        Try
            ' 1. Simpan transaksi
            SaveTransaction()
            
            ' 2. Cetak struk
            PrintReceipt()
            
            ' 3. Buka gate
            OpenExitGate()
            
        Catch ex As Exception
            MessageBox.Show("Error payment: " & ex.Message)
        End Try
    End Sub

    ' Cetak struk keluar
    Private Sub PrintReceipt()
        Try
            printer.PrinterSettings.PrinterName = "Nama_Printer"
            AddHandler printer.PrintPage, AddressOf PrintReceiptHandler
            printer.Print()
        Catch ex As Exception
            Throw New Exception("Gagal mencetak struk: " & ex.Message)
        End Try
    End Sub

    ' Handler untuk printing struk
    Private Sub PrintReceiptHandler(sender As Object, e As PrintPageEventArgs)
        Try
            ' Format struk
            Dim font As New Font("Arial", 10)
            Dim brush As New SolidBrush(Color.Black)
            Dim y As Integer = 10
            
            ' Header
            e.Graphics.DrawString("STRUK PEMBAYARAN PARKIR", New Font("Arial", 12, FontStyle.Bold), brush, 10, y)
            y += 30
            
            ' Informasi pembayaran
            e.Graphics.DrawString("No Transaksi: " & transactionNumber, font, brush, 10, y)
            y += 20
            e.Graphics.DrawString("Waktu Masuk: " & vehicleData.EntryTime.ToString(), font, brush, 10, y)
            y += 20
            e.Graphics.DrawString("Waktu Keluar: " & DateTime.Now.ToString(), font, brush, 10, y)
            y += 20
            e.Graphics.DrawString("Durasi: " & duration.ToString() & " jam", font, brush, 10, y)
            y += 20
            e.Graphics.DrawString("Biaya: Rp " & cost.ToString("#,##0"), font, brush, 10, y)
            
        Catch ex As Exception
            MessageBox.Show("Error printing receipt: " & ex.Message)
        End Try
    End Sub
End Class
```

## F. Modul Kontrol Gate (GateControl)
```vb
Public Class GateControl
    Private serialPort As New SerialPort

    Public Sub New(portName As String)
        With serialPort
            .PortName = portName
            .BaudRate = 9600
            .DataBits = 8
            .Parity = Parity.None
            .StopBits = StopBits.One
        End With
    End Sub

    Public Sub OpenGate()
        Try
            If Not serialPort.IsOpen Then serialPort.Open()
            serialPort.Write("OPEN_GATE")
            Threading.Thread.Sleep(2000)
            serialPort.Write("CLOSE_GATE")
        Catch ex As Exception
            Throw New Exception("Gagal membuka gate: " & ex.Message)
        Finally
            If serialPort.IsOpen Then serialPort.Close()
        End Try
    End Sub
End Class
```

## G. Modul Perhitungan Tarif (TariffCalculator)
```vb
Public Class TariffCalculator
    ' Konstanta tarif
    Private Const MOTOR_RATE As Decimal = 2000    ' Per jam
    Private Const CAR_RATE As Decimal = 5000      ' Per jam
    Private Const TRUCK_RATE As Decimal = 10000   ' Per jam

    Public Function CalculateCost(duration As Integer, vehicleType As String) As Decimal
        Dim rate As Decimal = GetRate(vehicleType)
        Dim hours As Integer = Math.Ceiling(duration / 60.0)  ' Konversi menit ke jam
        Return rate * hours
    End Function

    Private Function GetRate(vehicleType As String) As Decimal
        Select Case vehicleType.ToUpper()
            Case "MOTOR"
                Return MOTOR_RATE
            Case "MOBIL"
                Return CAR_RATE
            Case "TRUK"
                Return TRUCK_RATE
            Case Else
                Return CAR_RATE  ' Default rate
        End Select
    End Function
End Class
```

## H. Tips Implementasi
1. **Kontrol Gate**
   - Gunakan Arduino atau microcontroller lain untuk kontrol gate
   - Komunikasi via Serial Port
   - Buat mekanisme timeout untuk keamanan

2. **Pencetakan Tiket**
   - Gunakan printer thermal untuk tiket
   - Format tiket harus jelas dan mudah dibaca
   - Pastikan barcode berkualitas baik

3. **Scanner Barcode**
   - Pilih scanner yang mendukung multiple barcode format
   - Setup sebagai keyboard wedge untuk kemudahan
   - Tambahkan prefix/suffix untuk identifikasi

4. **Database**
   - Buat backup berkala
   - Index field-field yang sering dicari
   - Buat log untuk audit trail

5. **Keamanan**
   - Validasi semua input
   - Enkripsi data sensitif
   - Batasi akses berdasarkan role

6. **Error Handling**
   - Tangani semua kemungkinan error
   - Buat log error detail
   - Tampilkan pesan error yang user-friendly

## I. Pengembangan Lanjutan
1. Integrasi dengan kamera CCTV
2. Sistem member/langganan
3. Laporan dan analisis
4. Monitoring real-time
5. Backup dan recovery system

## J. Testing
1. Unit testing untuk setiap modul
2. Integration testing
3. Stress testing untuk concurrent users
4. Testing hardware integration
5. User acceptance testing 