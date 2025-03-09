using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Data;
using ParkIRC.Models;
using ParkIRC.Services.Interfaces;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Linq;

namespace ParkIRC.Controllers
{
    [Authorize]
    public class GateController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<GateController> _logger;
        private readonly IPrinterService _printerService;
        private readonly IIPCameraService _cameraService;

        public GateController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<GateController> logger,
            IPrinterService printerService,
            IIPCameraService cameraService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
            _printerService = printerService;
            _cameraService = cameraService;
        }

        // GET: Gate/Entry
        public IActionResult Entry()
        {
            return View();
        }

        // POST: Gate/DetectVehicle
        [HttpPost]
        public async Task<IActionResult> DetectVehicle()
        {
            try
            {
                bool isDetected = await _cameraService.IsVehiclePresent();
                if (!isDetected)
                {
                    return Json(new { detected = false });
                }

                string? imagePath = await _cameraService.CaptureImage();
                return Json(new { 
                    detected = true,
                    imagePath = imagePath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting vehicle");
                return Json(new { detected = false, error = ex.Message });
            }
        }

        // POST: Gate/ProcessEntry
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessEntry([FromForm] string vehicleNumber, [FromForm] string vehicleType)
        {
            try
            {
                // Capture vehicle image using IP camera
                string? imagePath = await _cameraService.CaptureImage();
                if (string.IsNullOrEmpty(imagePath))
                {
                    return Json(new { success = false, message = "Gagal mengambil foto kendaraan" });
                }

                // Get current shift
                var currentShift = await GetCurrentShiftAsync();
                if (currentShift == null)
                {
                    return Json(new { success = false, message = "Tidak ada shift aktif saat ini" });
                }

                // Find optimal parking space
                var parkingSpace = await FindOptimalParkingSpace(vehicleType);
                if (parkingSpace == null)
                {
                    return Json(new { success = false, message = "Tidak ada tempat parkir yang tersedia" });
                }

                // Create vehicle record
                var vehicle = new Vehicle
                {
                    VehicleNumber = vehicleNumber.ToUpper(),
                    VehicleType = vehicleType,
                    EntryTime = DateTime.Now,
                    EntryPhotoPath = imagePath,
                    EntryCCTVPhotoPath = imagePath,
                    IsParked = true,
                    ParkingSpace = parkingSpace
                };

                // Generate barcode data and image
                string barcodeData = GenerateBarcodeData(vehicle);
                string barcodeImagePath = await GenerateAndSaveQRCode(barcodeData);

                if (string.IsNullOrEmpty(barcodeImagePath))
                {
                    return Json(new { success = false, message = "Gagal generate barcode" });
                }

                // Create parking ticket
                var ticket = new ParkingTicket
                {
                    TicketNumber = GenerateTicketNumber(),
                    BarcodeData = barcodeData,
                    BarcodeImagePath = barcodeImagePath,
                    IssueTime = DateTime.Now,
                    Vehicle = vehicle,
                    OperatorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    ShiftId = currentShift.Id
                };

                // Create parking transaction
                var transaction = new ParkingTransaction
                {
                    Vehicle = vehicle,
                    ParkingSpace = parkingSpace,
                    EntryTime = DateTime.Now,
                    TransactionNumber = GenerateTransactionNumber(),
                    Status = "Active"
                };

                await _context.Vehicles.AddAsync(vehicle);
                await _context.ParkingTickets.AddAsync(ticket);
                await _context.ParkingTransactions.AddAsync(transaction);
                await _context.SaveChangesAsync();

                // Print ticket
                bool printSuccess = await _printerService.PrintEntryTicket(
                    ticket.TicketNumber,
                    vehicle.VehicleNumber,
                    ticket.IssueTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    ticket.BarcodeData
                );
                if (!printSuccess)
                {
                    _logger.LogWarning("Failed to print ticket {TicketNumber}", ticket.TicketNumber);
                }

                // Open gate
                await OpenGateAsync();

                return Json(new { 
                    success = true, 
                    message = "Kendaraan berhasil diproses" + (!printSuccess ? " (Gagal mencetak tiket)" : ""),
                    ticketNumber = ticket.TicketNumber,
                    entryTime = vehicle.EntryTime,
                    barcodeImageUrl = $"/images/barcodes/{Path.GetFileName(barcodeImagePath)}",
                    printStatus = printSuccess,
                    imagePath = imagePath
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing vehicle entry");
                return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
            }
        }

        // GET: Gate/Exit
        public IActionResult Exit()
        {
            return View();
        }

        // POST: Gate/ProcessExit
        [HttpPost]
        [Route("ProcessExit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessExit([FromForm] string ticketNumber)
        {
            try
            {
                var ticket = await _context.ParkingTickets
                    .Include(t => t.Vehicle)
                    .FirstOrDefaultAsync(t => t.TicketNumber == ticketNumber);

                if (ticket == null)
                {
                    return NotFound(new { error = "Tiket tidak ditemukan" });
                }

                if (ticket.IsUsed)
                {
                    return BadRequest(new { error = "Tiket sudah digunakan" });
                }

                // Get vehicle data
                var vehicle = ticket.Vehicle;
                if (vehicle == null)
                {
                    return BadRequest(new { error = "Data kendaraan tidak ditemukan" });
                }

                // Calculate duration and cost
                var duration = DateTime.Now - vehicle.EntryTime;
                var cost = CalculateParkingCost(duration.TotalMinutes, vehicle.VehicleType);

                // Generate exit ticket number
                string exitTicketNumber = GenerateExitTicketNumber();
                
                // Generate exit barcode
                string exitBarcodeData = $"EXIT_{exitTicketNumber}_{DateTime.Now:yyyyMMddHHmmss}";
                string exitBarcodeImagePath = await GenerateAndSaveQRCode(exitBarcodeData);

                // Create exit ticket record
                var exitTicket = new ExitTicket
                {
                    TicketNumber = ticketNumber,
                    ExitTicketNumber = exitTicketNumber,
                    OriginalTicketNumber = ticketNumber,
                    Barcode = exitBarcodeData,
                    BarcodeData = exitBarcodeData,
                    BarcodeImagePath = exitBarcodeImagePath,
                    ExitTime = DateTime.Now,
                    IssueTime = DateTime.Now,
                    ExpiryTime = DateTime.Now.AddMinutes(30), // Ticket valid for 30 minutes
                    ParkingCost = cost,
                    Cost = cost,
                    Duration = duration,
                    IsUsed = false,
                    ValidUntil = DateTime.Now.AddMinutes(30),
                    ParkingTicketId = ticket.Id,
                    VehicleId = vehicle.Id
                };

                await _context.ExitTickets.AddAsync(exitTicket);

                // Mark original ticket as processed
                ticket.IsProcessed = true;
                ticket.ProcessTime = DateTime.Now;

                await _context.SaveChangesAsync();

                // Print exit ticket
                bool printSuccess = await _printerService.PrintExitTicket(exitTicket, vehicle);
                if (!printSuccess)
                {
                    _logger.LogWarning("Failed to print exit ticket {TicketNumber}", exitTicketNumber);
                }

                return Json(new { 
                    success = true, 
                    message = "Tiket keluar berhasil diproses" + (!printSuccess ? " (Gagal mencetak tiket)" : ""),
                    exitTicketNumber = exitTicket.ExitTicketNumber,
                    cost = cost,
                    duration = duration.TotalMinutes,
                    barcodeImageUrl = $"/images/barcodes/{Path.GetFileName(exitBarcodeImagePath)}",
                    printStatus = printSuccess
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing exit ticket");
                return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
            }
        }

        [HttpPost]
        [Route("ValidateExitTicket")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidateExitTicket([FromForm] string exitTicketNumber)
        {
            try
            {
                var exitTicket = await _context.ExitTickets
                    .Include(t => t.Vehicle)
                    .FirstOrDefaultAsync(t => t.ExitTicketNumber == exitTicketNumber);

                if (exitTicket == null)
                {
                    return NotFound(new { success = false, message = "Tiket keluar tidak ditemukan" });
                }

                if (exitTicket.IsUsed || exitTicket.UseTime.HasValue)
                {
                    return BadRequest(new { success = false, message = "Tiket keluar sudah digunakan" });
                }

                if (DateTime.Now > exitTicket.ExpiryTime)
                {
                    return BadRequest(new { success = false, message = "Tiket keluar sudah kadaluarsa" });
                }

                // Mark exit ticket as used
                exitTicket.IsUsed = true;
                exitTicket.UseTime = DateTime.Now;

                // Update vehicle status
                var vehicle = exitTicket.Vehicle;
                if (vehicle != null)
                {
                    vehicle.IsParked = false;
                    vehicle.ExitTime = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // Open exit gate
                await OpenGateAsync();

                return Json(new
                {
                    success = true,
                    message = "Validasi berhasil, gate dibuka",
                    vehicleNumber = vehicle?.VehicleNumber,
                    exitTime = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating exit ticket");
                return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
            }
        }

        [HttpPost]
        [Route("AutoEntry")]
        public async Task<IActionResult> AutoEntry()
        {
            try
            {
                // 1. Deteksi kendaraan menggunakan kamera
                bool isDetected = await _cameraService.IsVehicleDetected();
                if (!isDetected)
                {
                    return Json(new { success = false, message = "Tidak ada kendaraan terdeteksi" });
                }

                // 2. Ambil foto kendaraan dari kedua kamera
                string? mainImagePath = await _cameraService.CaptureVehicleImage();
                string? cctvImagePath = await _cameraService.CaptureCCTVImage();

                if (string.IsNullOrEmpty(mainImagePath))
                {
                    return Json(new { success = false, message = "Gagal mengambil foto kendaraan dari kamera utama" });
                }

                // 3. Generate nomor tiket dan barcode
                string ticketNumber = GenerateTicketNumber();
                string barcodeData = $"{ticketNumber}|{DateTime.Now:yyyyMMddHHmmss}";
                string barcodeImagePath = await GenerateAndSaveQRCode(barcodeData);

                if (string.IsNullOrEmpty(barcodeImagePath))
                {
                    return Json(new { success = false, message = "Gagal generate barcode" });
                }

                // 4. Dapatkan shift aktif
                var currentShift = await GetCurrentShiftAsync();
                if (currentShift == null)
                {
                    return Json(new { success = false, message = "Tidak ada shift aktif" });
                }

                // 5. Cari parking space yang tersedia
                var parkingSpace = await FindOptimalParkingSpace("Unknown");
                if (parkingSpace == null)
                {
                    return Json(new { success = false, message = "Tidak ada tempat parkir tersedia" });
                }

                // 6. Buat record kendaraan dengan kedua foto
                var vehicle = new Vehicle
                {
                    VehicleNumber = "UNKNOWN",
                    VehicleType = "Unknown",
                    EntryTime = DateTime.Now,
                    EntryPhotoPath = mainImagePath,
                    EntryCCTVPhotoPath = cctvImagePath,
                    IsParked = true,
                    ParkingSpace = parkingSpace
                };

                // 7. Buat tiket parkir
                var ticket = new ParkingTicket
                {
                    TicketNumber = ticketNumber,
                    BarcodeData = barcodeData,
                    BarcodeImagePath = barcodeImagePath,
                    IssueTime = DateTime.Now,
                    Vehicle = vehicle,
                    OperatorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    ShiftId = currentShift.Id
                };

                // 8. Buat transaksi parkir
                var transaction = new ParkingTransaction
                {
                    Vehicle = vehicle,
                    ParkingSpace = parkingSpace,
                    EntryTime = DateTime.Now,
                    TransactionNumber = GenerateTransactionNumber(),
                    Status = "Active"
                };

                // 9. Simpan ke database
                await _context.Vehicles.AddAsync(vehicle);
                await _context.ParkingTickets.AddAsync(ticket);
                await _context.ParkingTransactions.AddAsync(transaction);
                await _context.SaveChangesAsync();

                // 10. Print tiket
                bool printSuccess = await _printerService.PrintTicket(ticket);
                
                // 11. Buka gate
                if (printSuccess)
                {
                    await OpenGateAsync();
                }

                // 12. Kirim response
                return Json(new
                {
                    success = true,
                    message = "Kendaraan berhasil masuk" + (!printSuccess ? " (Gagal mencetak tiket)" : ""),
                    ticketNumber = ticket.TicketNumber,
                    entryTime = vehicle.EntryTime,
                    barcodeImageUrl = $"/images/barcodes/{Path.GetFileName(barcodeImagePath)}",
                    printStatus = printSuccess,
                    imagePath = mainImagePath,
                    vehicleId = vehicle.Id // Untuk keperluan update data nanti
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto entry process");
                return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
            }
        }

        [HttpPost]
        [Route("UpdateVehicleInfo")]
        public async Task<IActionResult> UpdateVehicleInfo([FromBody] UpdateVehicleInfoModel model)
        {
            try
            {
                var vehicle = await _context.Vehicles.FindAsync(model.VehicleId);
                if (vehicle == null)
                {
                    return NotFound(new { success = false, message = "Kendaraan tidak ditemukan" });
                }

                vehicle.VehicleNumber = model.VehicleNumber.ToUpper();
                vehicle.VehicleType = model.VehicleType;
                
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Data kendaraan berhasil diupdate" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating vehicle info");
                return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
            }
        }

        public class UpdateVehicleInfoModel
        {
            public int VehicleId { get; set; }
            public string VehicleNumber { get; set; } = string.Empty;
            public string VehicleType { get; set; } = string.Empty;
        }

        private async Task<string> SaveBase64Image(string base64Image, string folder)
        {
            try
            {
                if (string.IsNullOrEmpty(base64Image) || !base64Image.Contains(","))
                {
                    _logger.LogWarning("Invalid base64 image format");
                    return string.Empty;
                }

                var base64Data = base64Image.Split(',')[1];
                if (string.IsNullOrEmpty(base64Data))
                {
                    _logger.LogWarning("Empty base64 image data");
                    return string.Empty;
                }

                var bytes = Convert.FromBase64String(base64Data);
                var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.jpg";
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", folder);
                
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, bytes);
                
                return $"/uploads/{folder}/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save base64 image");
                return string.Empty;
            }
        }

        private string GenerateBarcodeData(Vehicle vehicle)
        {
            return $"{vehicle.VehicleNumber}|{vehicle.EntryTime:yyyyMMddHHmmss}";
        }

        private string GenerateTicketNumber()
        {
            return $"TKT{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
        }

        private string GenerateTransactionNumber()
        {
            return $"TRN-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }

        private async Task<string> GenerateAndSaveQRCode(string data)
        {
            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new BitmapByteQRCode(qrCodeData);
                var qrCodeBytes = qrCode.GetGraphic(20);
                
                var fileName = $"qr_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}.png";
                var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", "barcodes", fileName);
                
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                await System.IO.File.WriteAllBytesAsync(filePath, qrCodeBytes);
                
                return $"/images/barcodes/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QR code");
                return string.Empty;
            }
        }

        private string GenerateExitTicketNumber()
        {
            return $"EXIT{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
        }

        private decimal CalculateParkingCost(double durationMinutes, string vehicleType)
        {
            // Implement your parking cost calculation logic here
            decimal baseRate = GetBaseRate(vehicleType);
            int hours = (int)Math.Ceiling(durationMinutes / 60.0);
            return baseRate * hours;
        }

        private decimal GetBaseRate(string vehicleType)
        {
            switch (vehicleType.ToUpper())
            {
                case "MOTORCYCLE":
                    return 2000M;
                case "CAR":
                    return 5000M;
                case "TRUCK":
                    return 10000M;
                default:
                    return 5000M;
            }
        }

        private async Task<Shift?> GetCurrentShiftAsync()
        {
            try
            {
                var now = DateTime.Now;
                // First get all active shifts
                var activeShifts = await _context.Shifts
                    .Where(s => s.IsActive)
                    .ToListAsync();
                
                // Then check time in memory
                return activeShifts.FirstOrDefault(s => s.IsTimeInShift(now));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current shift");
                return null;
            }
        }

        private async Task<ParkingSpace?> FindOptimalParkingSpace(string vehicleType)
        {
            try
            {
                // Get all unoccupied parking spaces for the vehicle type
                var availableSpaces = await _context.ParkingSpaces
                    .Where(p => !p.IsOccupied && p.SpaceType == vehicleType && p.IsActive)
                    .OrderBy(p => p.LastOccupiedTime)
                    .FirstOrDefaultAsync();

                if (availableSpaces == null)
                {
                    _logger.LogWarning($"No available parking space found for vehicle type: {vehicleType}");
                    return null;
                }

                return availableSpaces;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error finding optimal parking space for vehicle type: {vehicleType}");
                return null;
            }
        }

        private async Task OpenGateAsync()
        {
            try
            {
                // TODO: Implement actual gate control logic
                await Task.Delay(1000); // Simulate gate operation
                _logger.LogInformation("Gate opened successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening gate");
                throw;
            }
        }

        [HttpGet]
        [Route("GetCCTVFrame")]
        public async Task<IActionResult> GetCCTVFrame()
        {
            try
            {
                var frame = await _cameraService.GetCCTVFrame();
                if (frame == null)
                {
                    return NotFound();
                }
                return File(frame, "image/jpeg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting CCTV frame");
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("CaptureCCTV")]
        public async Task<IActionResult> CaptureCCTV()
        {
            try
            {
                string? cctvImagePath = await _cameraService.CaptureCCTVImage();
                if (string.IsNullOrEmpty(cctvImagePath))
                {
                    return Json(new { success = false, message = "Gagal mengambil foto dari CCTV" });
                }

                return Json(new { 
                    success = true, 
                    cctvImagePath = cctvImagePath,
                    message = "Berhasil mengambil foto dari CCTV"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing CCTV image");
                return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
            }
        }
    }
} 