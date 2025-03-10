using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ParkIRC.Models;
using ParkIRC.Services.Interfaces;

namespace ParkIRC.Services
{
    public class PrinterService : IPrinterService
    {
        private readonly ILogger<PrinterService> _logger;
        private readonly string _defaultPrinter;
        private const int GENERIC_WRITE = 0x40000000;
        private const int OPEN_EXISTING = 3;
        private readonly PrintDocument _printDocument;
        private readonly bool _isWindows;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess,
            uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        public PrinterService(ILogger<PrinterService> logger)
        {
            _logger = logger;
            _isWindows = OperatingSystem.IsWindows();
            _defaultPrinter = GetDefaultPrinter();
            if (_isWindows)
            {
                _printDocument = new PrintDocument();
            }
        }

        public async Task<bool> PrintTicket(ParkingTicket ticket)
        {
            if (!_isWindows)
            {
                _logger.LogWarning("Printing is only supported on Windows");
                return false;
            }

            try
            {
                var pd = new PrintDocument();
                pd.PrinterSettings.PrinterName = _defaultPrinter;

                pd.PrintPage += (sender, e) =>
                {
                    using (var font = new Font("Arial", 10))
                    {
                        float yPos = 10;
                        e.Graphics.DrawString("TIKET PARKIR", new Font("Arial", 12, FontStyle.Bold), Brushes.Black, 10, yPos);
                        yPos += 20;

                        // Print ticket details
                        e.Graphics.DrawString($"No. Tiket: {ticket.TicketNumber}", font, Brushes.Black, 10, yPos);
                        yPos += 20;
                        e.Graphics.DrawString($"Tanggal: {ticket.IssueTime:dd/MM/yyyy HH:mm}", font, Brushes.Black, 10, yPos);
                        yPos += 20;
                        e.Graphics.DrawString($"Kendaraan: {ticket.Vehicle?.VehicleNumber ?? "-"}", font, Brushes.Black, 10, yPos);
                        yPos += 20;

                        // Print QR Code if available
                        if (!string.IsNullOrEmpty(ticket.BarcodeImagePath))
                        {
                            try
                            {
                                using (var qrImage = Image.FromFile(ticket.BarcodeImagePath))
                                {
                                    e.Graphics.DrawImage(qrImage, 10, yPos, 100, 100);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error printing QR code");
                            }
                        }
                    }
                };

                pd.Print();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing ticket");
                return false;
            }
        }

        public async Task<bool> PrintReceipt(ParkingTransaction transaction)
        {
            if (!_isWindows)
            {
                _logger.LogWarning("Printing is only supported on Windows");
                return false;
            }

            try
            {
                var pd = new PrintDocument();
                pd.PrinterSettings.PrinterName = _defaultPrinter;

                pd.PrintPage += (sender, e) =>
                {
                    using (var font = new Font("Arial", 10))
                    {
                        float yPos = 10;
                        e.Graphics.DrawString("STRUK PARKIR", new Font("Arial", 12, FontStyle.Bold), Brushes.Black, 10, yPos);
                        yPos += 20;

                        // Print receipt details
                        e.Graphics.DrawString($"No. Transaksi: {transaction.TransactionNumber}", font, Brushes.Black, 10, yPos);
                        yPos += 20;
                        e.Graphics.DrawString($"Kendaraan: {transaction.Vehicle?.VehicleNumber ?? "-"}", font, Brushes.Black, 10, yPos);
                        yPos += 20;
                        e.Graphics.DrawString($"Masuk: {transaction.EntryTime:dd/MM/yyyy HH:mm}", font, Brushes.Black, 10, yPos);
                        yPos += 20;
                        e.Graphics.DrawString($"Keluar: {transaction.ExitTime:dd/MM/yyyy HH:mm}", font, Brushes.Black, 10, yPos);
                        yPos += 20;
                        e.Graphics.DrawString($"Durasi: {(transaction.ExitTime - transaction.EntryTime):hh\\:mm}", font, Brushes.Black, 10, yPos);
                        yPos += 20;
                        e.Graphics.DrawString($"Total: Rp {transaction.TotalAmount:N0}", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, yPos);
                    }
                };

                pd.Print();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing receipt");
                return false;
            }
        }

        public bool CheckPrinterStatus()
        {
            if (!_isWindows)
            {
                _logger.LogWarning("Printer status check is only supported on Windows");
                return false;
            }

            try
            {
                var handle = CreateFile($"\\\\.\\{_defaultPrinter}", GENERIC_WRITE, 0, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (handle == IntPtr.Zero || handle.ToInt32() == -1)
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking printer status");
                return false;
            }
        }

        public string GetDefaultPrinter()
        {
            if (!_isWindows)
            {
                _logger.LogWarning("Getting default printer is only supported on Windows");
                return string.Empty;
            }

            try
            {
                var ps = new PrinterSettings();
                return ps.PrinterName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting default printer");
                return string.Empty;
            }
        }

        public List<string> GetAvailablePrinters()
        {
            if (!_isWindows)
            {
                _logger.LogWarning("Getting available printers is only supported on Windows");
                return new List<string>();
            }

            try
            {
                return PrinterSettings.InstalledPrinters.Cast<string>().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available printers");
                return new List<string>();
            }
        }

        public async Task<bool> PrintExitTicket(ExitTicket exitTicket, Vehicle vehicle)
        {
            if (!_isWindows)
            {
                _logger.LogWarning("Printing is only supported on Windows");
                return false;
            }

            try
            {
                _printDocument.PrintPage += (sender, e) => PrintExitTicketHandler(sender, e, exitTicket, vehicle);
                _printDocument.Print();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing exit ticket");
                return false;
            }
        }

        private void PrintExitTicketHandler(object sender, PrintPageEventArgs e, ExitTicket exitTicket, Vehicle vehicle)
        {
            try
            {
                // Setup fonts and brushes
                var titleFont = new Font("Arial", 12, FontStyle.Bold);
                var normalFont = new Font("Arial", 10);
                var smallFont = new Font("Arial", 8);
                var brush = new SolidBrush(Color.Black);
                int y = 10;

                // Print header
                e.Graphics.DrawString("TIKET KELUAR PARKIR", titleFont, brush, 10, y);
                y += 30;

                // Print ticket info
                e.Graphics.DrawString($"No. Tiket: {exitTicket.ExitTicketNumber}", normalFont, brush, 10, y);
                y += 20;
                e.Graphics.DrawString($"Plat Nomor: {vehicle.VehicleNumber}", normalFont, brush, 10, y);
                y += 20;
                e.Graphics.DrawString($"Jenis: {vehicle.VehicleType}", normalFont, brush, 10, y);
                y += 20;
                e.Graphics.DrawString($"Masuk: {vehicle.EntryTime:dd/MM/yyyy HH:mm}", normalFont, brush, 10, y);
                y += 20;
                e.Graphics.DrawString($"Durasi: {exitTicket.Duration.Hours} jam {exitTicket.Duration.Minutes} menit", normalFont, brush, 10, y);
                y += 20;
                e.Graphics.DrawString($"Biaya: Rp {exitTicket.Cost:#,##0}", normalFont, brush, 10, y);
                y += 20;

                // Print barcode
                if (!string.IsNullOrEmpty(exitTicket.BarcodeImagePath))
                {
                    var barcodeImage = Image.FromFile(exitTicket.BarcodeImagePath);
                    e.Graphics.DrawImage(barcodeImage, 10, y, 200, 100);
                    y += 120;
                }

                // Print footer
                e.Graphics.DrawString("Berlaku 30 menit setelah pembayaran", smallFont, brush, 10, y);
                y += 15;
                e.Graphics.DrawString("Simpan tiket ini untuk keluar", smallFont, brush, 10, y);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PrintExitTicketHandler");
                throw;
            }
        }
    }
} 