using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Models;
using ParkIRC.Models.ViewModels;
using ParkIRC.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using ParkIRC.Hubs;
using ParkIRC.Services;
using Microsoft.AspNetCore.Authorization;
using ParkIRC.Extensions;
using System.Text.Json;
using System.IO.Compression;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ParkIRC.Controllers
{
    [Authorize]
    public class ParkingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ParkingController> _logger;
        private readonly IHubContext<ParkingHub> _hubContext;
        private readonly IParkingService _parkingService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ParkingController(
            ApplicationDbContext context,
            ILogger<ParkingController> logger,
            IParkingService parkingService,
            IHubContext<ParkingHub> hubContext,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _logger = logger;
            _parkingService = parkingService;
            _hubContext = hubContext;
            _webHostEnvironment = webHostEnvironment;
        }
        
        [ResponseCache(Duration = 60)]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var dashboardData = await GetDashboardData();
                var statisticsData = new DashboardStatisticsViewModel
                {
                    TotalSpaces = await _context.ParkingSpaces.CountAsync(),
                    AvailableSpaces = await _context.ParkingSpaces.CountAsync(s => !s.IsOccupied),
                    DailyRevenue = await GetDailyRevenue(),
                    WeeklyRevenue = await GetWeeklyRevenue(),
                    MonthlyRevenue = await GetMonthlyRevenue(),
                    RecentActivity = await GetRecentActivity(),
                    HourlyOccupancy = await GetHourlyOccupancyData(),
                    VehicleDistribution = await GetVehicleTypeDistribution()
                };
                
                ViewBag.Statistics = statisticsData;
                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard");
                return View("Error", new ParkIRC.Models.ErrorViewModel 
                { 
                    Message = "Terjadi kesalahan saat memuat dashboard. Silakan coba lagi nanti.",
                    RequestId = HttpContext.TraceIdentifier
                });
            }
        }

        private async Task<decimal> GetDailyRevenue()
        {
            var today = DateTime.Today;
            return await _context.ParkingTransactions
                .Where(t => t.PaymentTime.Date == today)
                .SumAsync(t => t.TotalAmount);
        }

        private async Task<decimal> GetWeeklyRevenue()
        {
            var today = DateTime.Today;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            return await _context.ParkingTransactions
                .Where(t => t.PaymentTime.Date >= weekStart && t.PaymentTime.Date <= today)
                .SumAsync(t => t.TotalAmount);
        }

        private async Task<decimal> GetMonthlyRevenue()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            return await _context.ParkingTransactions
                .Where(t => t.PaymentTime.Date >= monthStart && t.PaymentTime.Date <= today)
                .SumAsync(t => t.TotalAmount);
        }

        private async Task<List<ParkingActivity>> GetRecentActivity()
        {
            return await _context.ParkingTransactions
                .Include(t => t.Vehicle)
                .Include(t => t.ParkingSpace)
                .Where(t => t.Vehicle != null)
                    .OrderByDescending(t => t.EntryTime)
                    .Take(10)
                    .Select(t => new ParkingActivity
                    {
                    VehicleType = t.Vehicle.VehicleType ?? "Unknown",
                    LicensePlate = t.Vehicle.VehicleNumber ?? "Unknown",
                        Timestamp = t.EntryTime,
                        ActionType = t.ExitTime != default(DateTime) ? "Exit" : "Entry",
                        Fee = t.TotalAmount,
                    ParkingType = t.ParkingSpace != null ? t.ParkingSpace.SpaceType ?? "Unknown" : "Unknown"
                    })
                    .ToListAsync();
        }

        private async Task<List<OccupancyData>> GetHourlyOccupancyData()
        {
            var today = DateTime.Today;
            var totalSpaces = await _context.ParkingSpaces.CountAsync();
            
            var hourlyData = await _context.ParkingTransactions
                    .Where(t => t.EntryTime.Date == today)
                    .GroupBy(t => t.EntryTime.Hour)
                    .Select(g => new OccupancyData
                    {
                    Hour = $"{g.Key:D2}:00",
                    Count = g.Count(),
                        OccupancyPercentage = totalSpaces > 0 ? (double)g.Count() / totalSpaces * 100 : 0
                    })
                .ToListAsync();

            // Fill in missing hours with zero values
            var allHours = Enumerable.Range(0, 24)
                .Select(h => new OccupancyData
                {
                    Hour = $"{h:D2}:00",
                    Count = 0,
                    OccupancyPercentage = 0
                });

            return allHours.Union(hourlyData, new OccupancyDataComparer())
                    .OrderBy(x => x.Hour)
                    .ToList();
        }

        private async Task<List<VehicleDistributionData>> GetVehicleTypeDistribution()
        {
            try
            {
                return await _context.Vehicles
                    .Where(v => v.IsParked)
                    .GroupBy(v => v.VehicleType ?? "Unknown")
                    .Select(g => new VehicleDistributionData
                    {
                        Type = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting vehicle type distribution");
                return new List<VehicleDistributionData>();
            }
        }

        public IActionResult VehicleEntry()
        {
            return View();
        }

        private async Task<ParkingSpace?> FindOptimalParkingSpace(string vehicleType)
        {
            try
            {
                // Get all available spaces that match the vehicle type
                var availableSpaces = await _context.ParkingSpaces
                    .Where(s => !s.IsOccupied && s.SpaceType == vehicleType)
                    .ToListAsync();

                if (!availableSpaces.Any())
                {
                    _logger.LogWarning("No available parking spaces for vehicle type: {VehicleType}", vehicleType);
                    return null;
                }

                // For now, we'll use a simple strategy: pick the first available space
                // TODO: Implement more sophisticated space selection based on:
                // 1. Proximity to entrance/exit
                // 2. Space size optimization
                // 3. Traffic flow optimization
                return availableSpaces.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding optimal parking space for vehicle type: {VehicleType}", vehicleType);
                return null;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordEntry([FromBody] VehicleEntryModel entryModel)
        {
            if (entryModel == null)
            {
                _logger.LogWarning("Vehicle entry model is null");
                return BadRequest(new { error = "Data kendaraan tidak valid" });
            }

            try
            {
                _logger.LogInformation("Processing vehicle entry for {VehicleNumber}, Type: {VehicleType}", 
                    entryModel.VehicleNumber, entryModel.VehicleType);
                    
                // Validate the model
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => kvp.Value != null && kvp.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                        );
                    
                    _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors.Values.SelectMany(v => v)));
                    return BadRequest(new { errors });
                }

                // Normalize vehicle number format
                entryModel.VehicleNumber = entryModel.VehicleNumber.ToUpper().Trim();
                
                // Check vehicle format separately
                var vehicleNumberRegex = new System.Text.RegularExpressions.Regex(@"^[A-Z]{1,2}\s\d{1,4}\s[A-Z]{1,3}$");
                if (!vehicleNumberRegex.IsMatch(entryModel.VehicleNumber))
                {
                    _logger.LogWarning("Invalid vehicle number format: {VehicleNumber}", entryModel.VehicleNumber);
                    return BadRequest(new { error = "Format nomor kendaraan tidak valid. Contoh: B 1234 ABC" });
                }

                // Check if vehicle already exists and is parked
                var existingVehicle = await _context.Vehicles
                    .Include(v => v.ParkingSpace)
                    .FirstOrDefaultAsync(v => v.VehicleNumber == entryModel.VehicleNumber);
                
                if (existingVehicle != null && existingVehicle.IsParked)
                {
                    _logger.LogWarning("Vehicle {VehicleNumber} is already parked", entryModel.VehicleNumber);
                    return BadRequest(new { error = "Kendaraan sudah terparkir" });
                }

                // Find optimal parking space automatically
                var optimalSpace = await FindOptimalParkingSpace(entryModel.VehicleType);
                if (optimalSpace == null)
                {
                    _logger.LogWarning("No available parking space for vehicle type: {VehicleType}", entryModel.VehicleType);
                    return BadRequest(new { error = $"Tidak ada ruang parkir tersedia untuk kendaraan tipe {GetVehicleTypeName(entryModel.VehicleType)}" });
                }

                // Create or update vehicle record
                if (existingVehicle == null)
                {
                    existingVehicle = new Vehicle
                    {
                        VehicleNumber = entryModel.VehicleNumber,
                        VehicleType = entryModel.VehicleType,
                        DriverName = entryModel.DriverName,
                        PhoneNumber = entryModel.PhoneNumber,
                        IsParked = true,
                        ParkingSpace = optimalSpace
                    };
                    _context.Vehicles.Add(existingVehicle);
                }
                else
                {
                    existingVehicle.VehicleType = entryModel.VehicleType;
                    existingVehicle.DriverName = entryModel.DriverName;
                    existingVehicle.PhoneNumber = entryModel.PhoneNumber;
                    existingVehicle.IsParked = true;
                    existingVehicle.ParkingSpace = optimalSpace;
                }

                // Create parking transaction
                var transaction = new ParkingTransaction
                {
                    Vehicle = existingVehicle,
                    EntryTime = DateTime.Now,
                        TransactionNumber = GenerateTransactionNumber(),
                    Status = "Active"
                    };
                _context.ParkingTransactions.Add(transaction);

                // Update parking space status
                optimalSpace.IsOccupied = true;
                optimalSpace.LastOccupiedTime = DateTime.Now;

                    await _context.SaveChangesAsync();
                    
                // Notify clients about the update via SignalR
                    if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("UpdateParkingStatus", new
                    {
                        Action = "Entry",
                        VehicleNumber = entryModel.VehicleNumber,
                        SpaceNumber = optimalSpace.SpaceNumber,
                        SpaceType = optimalSpace.SpaceType,
                        Timestamp = DateTime.Now
                    });
                }

                _logger.LogInformation("Vehicle {VehicleNumber} entered the parking lot.", entryModel.VehicleNumber);

                return Ok(new
                {
                    message = "Kendaraan berhasil masuk",
                    spaceNumber = optimalSpace.SpaceNumber,
                    spaceType = optimalSpace.SpaceType,
                    transactionNumber = transaction.TransactionNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing vehicle entry for {VehicleNumber}", entryModel.VehicleNumber);
                return StatusCode(500, new { error = "Terjadi kesalahan saat memproses masuk kendaraan" });
            }
        }

        private string GetVehicleTypeName(string type)
        {
            return type.ToLower() switch
            {
                "car" => "mobil",
                "motorcycle" => "motor",
                "truck" => "truk",
                _ => type
            };
        }

        private async Task<object> GetHourlyOccupancy()
        {
            var today = DateTime.Today;
            var hourlyData = await _context.ParkingTransactions
                .Where(t => t.EntryTime.Date == today)
                .GroupBy(t => t.EntryTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Hour)
                .ToListAsync();

            return new
            {
                Labels = hourlyData.Select(x => $"{x.Hour}:00").ToList(),
                Data = hourlyData.Select(x => x.Count).ToList()
            };
        }

        private async Task<object> GetVehicleDistributionForDashboard()
        {
            var distribution = await GetVehicleTypeDistribution();
            return new
            {
                Labels = distribution.Select(x => x.Type).ToList(),
                Data = distribution.Select(x => x.Count).ToList()
            };
        }

        private async Task<List<object>> GetRecentParkingActivity()
        {
            var activities = await _context.ParkingTransactions
                .Where(t => t.Vehicle != null)
                .OrderByDescending(t => t.EntryTime)
                .Take(10)
                .Select(t => new ParkingActivityViewModel
                {
                    VehicleNumber = t.Vehicle != null ? t.Vehicle.VehicleNumber : "Unknown",
                    EntryTime = t.EntryTime,
                    ExitTime = t.ExitTime,
                    Duration = t.ExitTime != default(DateTime) ? 
                        (t.ExitTime - t.EntryTime).TotalHours.ToString("F1") + " hours" : 
                        "In Progress",
                    Amount = t.TotalAmount,
                    Status = t.ExitTime != default(DateTime) ? "Completed" : "In Progress"
                })
                .ToListAsync();
                
            return activities.Cast<object>().ToList();
        }

        public IActionResult VehicleExit()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessExit([FromBody] string vehicleNumber)
        {
            if (string.IsNullOrEmpty(vehicleNumber))
                return BadRequest(new { error = "Nomor kendaraan harus diisi." });

            try
            {
                // Normalize vehicle number
                vehicleNumber = vehicleNumber.ToUpper().Trim();

                // Find the vehicle and include necessary related data
                var vehicle = await _context.Vehicles
                    .Include(v => v.ParkingSpace)
                    .FirstOrDefaultAsync(v => v.VehicleNumber == vehicleNumber && v.IsParked);

                if (vehicle == null)
                    return NotFound(new { error = "Kendaraan tidak ditemukan atau sudah keluar dari parkir." });

                // Find active transaction
                var transaction = await _context.ParkingTransactions
                    .Where(t => t.VehicleId == vehicle.Id && t.ExitTime == default(DateTime))
                    .OrderByDescending(t => t.EntryTime)
                    .FirstOrDefaultAsync();

                if (transaction == null)
                    return NotFound(new { error = "Tidak ditemukan transaksi parkir yang aktif untuk kendaraan ini." });

                // Get applicable parking rate
                var parkingRate = await _context.Set<ParkingRateConfiguration>()
                    .Where(r => r.VehicleType == vehicle.VehicleType 
                            && r.IsActive 
                            && r.EffectiveFrom <= DateTime.Now 
                            && (!r.EffectiveTo.HasValue || r.EffectiveTo >= DateTime.Now))
                    .OrderByDescending(r => r.EffectiveFrom)
                    .FirstOrDefaultAsync();

                if (parkingRate == null)
                    return BadRequest(new { error = "Tidak dapat menemukan tarif parkir yang sesuai." });

                // Calculate duration and fee
                var exitTime = DateTime.Now;
                var duration = exitTime - transaction.EntryTime;
                var hours = Math.Ceiling(duration.TotalHours);
                
                decimal totalAmount;
                if (hours <= 1)
                {
                    totalAmount = parkingRate.BaseRate;
                }
                else if (hours <= 24)
                {
                    totalAmount = parkingRate.BaseRate + (parkingRate.HourlyRate * (decimal)(hours - 1));
                }
                else if (hours <= 168) // 7 days
                {
                    var days = Math.Ceiling(hours / 24);
                    totalAmount = parkingRate.DailyRate * (decimal)days;
                }
                else // more than 7 days
                {
                    var weeks = Math.Ceiling(hours / 168);
                    totalAmount = parkingRate.WeeklyRate * (decimal)weeks;
                }

                // Update transaction
                transaction.ExitTime = exitTime;
                transaction.TotalAmount = totalAmount;
                transaction.Status = "Completed";
                _context.ParkingTransactions.Update(transaction);

                // Update vehicle and parking space
                vehicle.ExitTime = exitTime;
                vehicle.IsParked = false;
                if (vehicle.ParkingSpace != null)
                {
                    vehicle.ParkingSpace.IsOccupied = false;
                    vehicle.ParkingSpace.LastOccupiedTime = exitTime;
                }
                _context.Vehicles.Update(vehicle);

                await _context.SaveChangesAsync();
                
                // Create response data
                var response = new
                {
                    message = "Kendaraan berhasil keluar",
                    vehicleNumber = vehicle.VehicleNumber,
                    entryTime = transaction.EntryTime,
                    exitTime = exitTime,
                    duration = $"{hours:0} jam",
                    totalAmount = totalAmount,
                    spaceNumber = vehicle.ParkingSpace?.SpaceNumber
                };

                // Notify clients if SignalR hub is available
                if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("UpdateParkingStatus", new
                    {
                        Action = "Exit",
                        VehicleNumber = vehicle.VehicleNumber,
                        SpaceNumber = vehicle.ParkingSpace?.SpaceNumber,
                        Timestamp = exitTime
                    });

                    var dashboardData = await GetDashboardData();
                    await _hubContext.Clients.All.SendAsync("ReceiveDashboardUpdate", dashboardData);
                }

                _logger.LogInformation("Vehicle {VehicleNumber} exited the parking lot.", vehicleNumber);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing vehicle exit for {VehicleNumber}", vehicleNumber);
                return StatusCode(500, new { error = "Terjadi kesalahan saat memproses keluar kendaraan." });
            }
        }

        private async Task<ParkingSpace?> GetAvailableParkingSpace(string type)
        {
            return await _context.ParkingSpaces
                .Where(ps => !ps.IsOccupied && ps.SpaceType.ToLower() == type.ToLower())
                .FirstOrDefaultAsync();
        }

        private static decimal CalculateParkingFee(DateTime entryTime, DateTime exitTime, decimal hourlyRate)
        {
            var duration = exitTime - entryTime;
            var hours = (decimal)Math.Ceiling(duration.TotalHours);
            return hours * hourlyRate;
        }

        private static string GenerateTransactionNumber()
        {
            return $"TRX-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}".ToUpper();
        }

        // Get available parking spaces by vehicle type
        [HttpGet]
        public async Task<IActionResult> GetAvailableSpaces()
        {
            try
            {
                var spaces = await _context.ParkingSpaces
                    .Where(ps => !ps.IsOccupied)
                    .GroupBy(ps => ps.SpaceType.ToLower())
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Type, x => x.Count);

                return Json(new
                {
                    car = spaces.GetValueOrDefault("car", 0),
                    motorcycle = spaces.GetValueOrDefault("motorcycle", 0),
                    truck = spaces.GetValueOrDefault("truck", 0)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available parking spaces");
                return StatusCode(500, new { error = "Gagal mengambil data slot parkir" });
            }
        }

        public IActionResult Reports()
        {
            return View();
        }

        public IActionResult Settings()
        {
            return View();
        }

        // New method for exporting dashboard data
        public async Task<IActionResult> ExportDashboardData()
        {
            try
            {
                var dashboardData = await GetDashboardData();
                // In a real implementation, you would use a library like EPPlus to create an Excel file
                // For now, we'll return a CSV
                var csv = "TotalSpaces,AvailableSpaces,DailyRevenue,WeeklyRevenue,MonthlyRevenue\n";
                csv += $"{dashboardData.TotalSpaces},{dashboardData.AvailableSpaces},{dashboardData.DailyRevenue},{dashboardData.WeeklyRevenue},{dashboardData.MonthlyRevenue}";
                
                return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "dashboard-report.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting dashboard data");
                return StatusCode(500, "Error exporting dashboard data");
            }
        }

        public async Task<IActionResult> GetParkingTransactions(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.ParkingTransactions.AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(t => t.EntryTime >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(t => t.ExitTime <= endDate.Value);
            }

            var transactions = await query.ToListAsync();
            return Json(transactions);
        }

        public async Task<IActionResult> Index(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.ParkingSpaces.Include(p => p.CurrentVehicle).AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(p => p.CurrentVehicle != null && p.CurrentVehicle.EntryTime >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(p => p.CurrentVehicle != null && p.CurrentVehicle.EntryTime <= endDate.Value);
            }

            var spaces = await query.ToListAsync();
            return View(spaces);
        }

        [HttpPost]
        public async Task<IActionResult> CheckOut(int id)
        {
            var vehicle = await _context.Vehicles
                .Include(v => v.ParkingSpace)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (vehicle == null)
            {
                return NotFound();
            }

            var transaction = await _parkingService.ProcessCheckout(vehicle);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> CheckVehicleAvailability(string vehicleNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vehicleNumber))
                {
                    return BadRequest(new { error = "Nomor kendaraan tidak valid" });
                }

                vehicleNumber = vehicleNumber.ToUpper().Trim();
                
                var existingVehicle = await _context.Vehicles
                    .FirstOrDefaultAsync(v => v.VehicleNumber == vehicleNumber && v.IsParked);
                    
                return Ok(new { 
                    isAvailable = existingVehicle == null,
                    message = existingVehicle != null 
                        ? "Kendaraan sudah terparkir di fasilitas" 
                        : "Nomor kendaraan tersedia untuk digunakan"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking vehicle availability for {VehicleNumber}", vehicleNumber);
                return StatusCode(500, new { error = "Terjadi kesalahan saat memeriksa ketersediaan kendaraan." });
            }
        }

        public async Task<IActionResult> Transaction()
        {
            var currentShift = await GetCurrentShiftAsync();
            ViewBag.CurrentShift = currentShift;
            
            var parkingRates = await _context.ParkingRates
                .Where(r => r.IsActive)
                .OrderBy(r => r.VehicleType)
                .ToListAsync();
            ViewBag.ParkingRates = parkingRates;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessTransaction([FromBody] TransactionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var vehicle = await _context.Vehicles
                    .Include(v => v.ParkingSpace)
                    .FirstOrDefaultAsync(v => v.VehicleNumber == request.VehicleNumber && v.IsParked);

                if (vehicle == null)
                {
                    return NotFound("Vehicle not found or not currently parked");
                }

                var parkingSpace = vehicle.ParkingSpace;
                if (parkingSpace == null)
                {
                    return NotFound("Parking space not found");
                }

                var rate = await _context.ParkingRates
                    .FirstOrDefaultAsync(r => r.VehicleType == vehicle.VehicleType && r.IsActive);

                if (rate == null)
                {
                    return NotFound("Parking rate not found for this vehicle type");
                }

                // Calculate duration and amount
                var duration = DateTime.Now - vehicle.EntryTime;
                var amount = CalculateParkingFee(vehicle.EntryTime, DateTime.Now, rate.HourlyRate);

                // Create transaction
                var transaction = new ParkingTransaction
                {
                    VehicleId = vehicle.Id,
                    ParkingSpaceId = parkingSpace.Id,
                    TransactionNumber = GenerateTransactionNumber(),
                    EntryTime = vehicle.EntryTime,
                    ExitTime = DateTime.Now,
                    HourlyRate = rate.HourlyRate,
                    Amount = amount,
                    TotalAmount = amount,
                    PaymentStatus = "Completed",
                    PaymentMethod = request.PaymentMethod,
                    PaymentTime = DateTime.Now,
                    Status = "Completed"
                };

                _context.ParkingTransactions.Add(transaction);

                // Update vehicle and parking space
                vehicle.ExitTime = DateTime.Now;
                vehicle.IsParked = false;
                parkingSpace.IsOccupied = false;
                parkingSpace.CurrentVehicle = null;

                await _context.SaveChangesAsync();

                // Notify clients of the update
                await _hubContext.Clients.All.SendAsync("UpdateParkingStatus");

                return Json(new
                {
                    success = true,
                    transactionNumber = transaction.TransactionNumber,
                    vehicleNumber = vehicle.VehicleNumber,
                    entryTime = vehicle.EntryTime,
                    exitTime = transaction.ExitTime,
                    duration = duration.ToString(@"hh\:mm"),
                    amount = transaction.TotalAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing parking transaction");
                return StatusCode(500, "An error occurred while processing the transaction");
            }
        }

        private async Task<Shift> GetCurrentShiftAsync()
        {
            var now = DateTime.Now;
            // First get all active shifts
            var activeShifts = await _context.Shifts
                .Where(s => s.IsActive)
                .ToListAsync();
            
            // Use IsTimeInShift method to find current shift
            var currentShift = activeShifts
                .FirstOrDefault(s => s.IsTimeInShift(now));
        
            if (currentShift == null)
                throw new InvalidOperationException("No active shift found");
        
            return currentShift;
        }

        private async Task<DashboardViewModel> GetDashboardData()
        {
            var today = DateTime.Today;
            
            return new DashboardViewModel
            {
                TotalVehicles = await _context.Vehicles.CountAsync(v => v.IsParked),
                TotalIncome = await _context.ParkingTransactions.SumAsync(t => t.TotalAmount),
                AvailableSpaces = await _context.ParkingSpaces.CountAsync(s => !s.IsOccupied),
                ActiveOperators = await _context.Operators.CountAsync(o => o.IsActive),
                CurrentShift = await _context.Shifts.FirstOrDefaultAsync(s => s.StartTime <= DateTime.Now && s.EndTime >= DateTime.Now),
                DeviceStatus = new DeviceStatusViewModel
                {
                    EntranceGate = true,
                    ExitGate = true,
                    EntranceCamera = true,
                    ExitCamera = true,
                    EntrancePrinter = true,
                    ExitPrinter = true,
                    Server = true,
                    Database = true
                }
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BackupData(string backupPeriod, DateTime? startDate, DateTime? endDate, bool includeImages = true)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);
                var backupPath = Path.Combine(tempPath, "backup");
                Directory.CreateDirectory(backupPath);

                // Determine date range
                DateTime rangeStart, rangeEnd;
                switch (backupPeriod)
                {
                    case "daily":
                        rangeStart = DateTime.Today;
                        rangeEnd = DateTime.Today.AddDays(1).AddSeconds(-1);
                        break;
                    case "monthly":
                        rangeStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                        rangeEnd = rangeStart.AddMonths(1).AddSeconds(-1);
                        break;
                    case "custom":
                        if (!startDate.HasValue || !endDate.HasValue)
                            return BadRequest("Invalid date range");
                        rangeStart = startDate.Value.Date;
                        rangeEnd = endDate.Value.Date.AddDays(1).AddSeconds(-1);
                        break;
                    default:
                        return BadRequest("Invalid backup period");
                }

                // Get data within date range
                var vehicles = await _context.Vehicles
                    .Where(v => v.EntryTime >= rangeStart && v.EntryTime <= rangeEnd)
                    .ToListAsync();

                var transactions = await _context.ParkingTransactions
                    .Where(t => t.EntryTime >= rangeStart && t.EntryTime <= rangeEnd)
                    .ToListAsync();

                var tickets = await _context.ParkingTickets
                    .Where(t => t.IssueTime >= rangeStart && t.IssueTime <= rangeEnd)
                    .ToListAsync();

                // Create backup data object
                var backupData = new
                {
                    BackupDate = DateTime.Now,
                    DateRange = new { Start = rangeStart, End = rangeEnd },
                    Vehicles = vehicles,
                    Transactions = transactions,
                    Tickets = tickets
                };

                // Save data to JSON
                var jsonPath = Path.Combine(backupPath, "data.json");
                await System.IO.File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(backupData, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));

                // Copy images if requested
                if (includeImages)
                {
                    var imagesPath = Path.Combine(backupPath, "images");
                    Directory.CreateDirectory(imagesPath);

                    // Copy entry photos
                    foreach (var vehicle in vehicles.Where(v => !string.IsNullOrEmpty(v.EntryPhotoPath)))
                    {
                        var sourcePath = Path.Combine(_webHostEnvironment.WebRootPath, vehicle.EntryPhotoPath.TrimStart('/'));
                        if (System.IO.File.Exists(sourcePath))
                        {
                            var destPath = Path.Combine(imagesPath, Path.GetFileName(vehicle.EntryPhotoPath));
                            System.IO.File.Copy(sourcePath, destPath, true);
                        }
                    }

                    // Copy exit photos
                    foreach (var vehicle in vehicles.Where(v => !string.IsNullOrEmpty(v.ExitPhotoPath)))
                    {
                        var sourcePath = Path.Combine(_webHostEnvironment.WebRootPath, vehicle.ExitPhotoPath.TrimStart('/'));
                        if (System.IO.File.Exists(sourcePath))
                        {
                            var destPath = Path.Combine(imagesPath, Path.GetFileName(vehicle.ExitPhotoPath));
                            System.IO.File.Copy(sourcePath, destPath, true);
                        }
                    }

                    // Copy barcode images
                    foreach (var ticket in tickets.Where(t => !string.IsNullOrEmpty(t.BarcodeImagePath)))
                    {
                        var sourcePath = Path.Combine(_webHostEnvironment.WebRootPath, ticket.BarcodeImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(sourcePath))
                        {
                            var destPath = Path.Combine(imagesPath, Path.GetFileName(ticket.BarcodeImagePath));
                            System.IO.File.Copy(sourcePath, destPath, true);
                        }
                    }
                }

                // Create ZIP file
                var zipPath = Path.Combine(tempPath, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                ZipFile.CreateFromDirectory(backupPath, zipPath);

                // Read ZIP file
                var zipBytes = await System.IO.File.ReadAllBytesAsync(zipPath);

                // Cleanup
                Directory.Delete(tempPath, true);

                return File(zipBytes, "application/zip", Path.GetFileName(zipPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during backup");
                return StatusCode(500, new { success = false, message = "Gagal melakukan backup: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreData(IFormFile backupFile, bool overwriteExisting = false)
        {
            try
            {
                if (backupFile == null || backupFile.Length == 0)
                {
                    return BadRequest(new { success = false, message = "File backup tidak valid" });
                }

                if (!backupFile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { success = false, message = "File harus berformat ZIP" });
                }

                var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempPath);

                try
                {
                    using (var stream = new FileStream(Path.Combine(tempPath, backupFile.FileName), FileMode.Create))
                    {
                        await backupFile.CopyToAsync(stream);
                    }

                    System.IO.Compression.ZipFile.ExtractToDirectory(
                        Path.Combine(tempPath, backupFile.FileName),
                        tempPath,
                        overwriteExisting);

                    var jsonContent = await System.IO.File.ReadAllTextAsync(Path.Combine(tempPath, "data.json"));
                    var backupData = JsonSerializer.Deserialize<BackupData>(jsonContent);

                    if (backupData == null)
                    {
                        throw new Exception("Data backup tidak valid");
                    }

                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Restore vehicles
                        foreach (var vehicle in backupData.Vehicles)
                        {
                            var existingVehicle = await _context.Vehicles
                                .FirstOrDefaultAsync(v => v.VehicleNumber == vehicle.VehicleNumber);

                            if (existingVehicle == null)
                            {
                                await _context.Vehicles.AddAsync(vehicle);
                            }
                            else if (overwriteExisting)
                            {
                                _context.Entry(existingVehicle).CurrentValues.SetValues(vehicle);
                            }
                        }

                        // Restore transactions
                        foreach (var parkingTransaction in backupData.Transactions)
                        {
                            var existingTransaction = await _context.ParkingTransactions
                                .FirstOrDefaultAsync(t => t.TransactionNumber == parkingTransaction.TransactionNumber);

                            if (existingTransaction == null)
                            {
                                await _context.ParkingTransactions.AddAsync(parkingTransaction);
                            }
                            else if (overwriteExisting)
                            {
                                _context.Entry(existingTransaction).CurrentValues.SetValues(parkingTransaction);
                            }
                        }

                        // Restore tickets with correct property name
                        foreach (var ticket in backupData.Tickets)
                        {
                            var existingTicket = await _context.ParkingTickets
                                .FirstOrDefaultAsync(t => t.TicketNumber == ticket.TicketNumber);

                            if (existingTicket == null)
                            {
                                await _context.ParkingTickets.AddAsync(ticket);
                            }
                            else if (overwriteExisting)
                            {
                                _context.Entry(existingTicket).CurrentValues.SetValues(ticket);
                            }
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return Json(new { success = true, message = "Data berhasil dipulihkan" });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw new Exception($"Gagal memulihkan data: {ex.Message}");
                    }
                }
                finally
                {
                    // Cleanup temporary files
                    if (Directory.Exists(tempPath))
                    {
                        Directory.Delete(tempPath, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring data");
                return StatusCode(500, new { success = false, message = $"Gagal memulihkan data: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearData(string[] clearOptions, string clearPeriod, DateTime? clearBeforeDate)
        {
            try
            {
                if (clearOptions == null || clearOptions.Length == 0)
                    return BadRequest(new { success = false, message = "Pilih minimal satu jenis data yang akan dihapus" });

                DateTime cutoffDate;
                switch (clearPeriod)
                {
                    case "all":
                        cutoffDate = DateTime.MaxValue;
                        break;
                    case "older_than_month":
                        cutoffDate = DateTime.Today.AddMonths(-1);
                        break;
                    case "older_than_3months":
                        cutoffDate = DateTime.Today.AddMonths(-3);
                        break;
                    case "older_than_6months":
                        cutoffDate = DateTime.Today.AddMonths(-6);
                        break;
                    case "older_than_year":
                        cutoffDate = DateTime.Today.AddYears(-1);
                        break;
                    case "custom":
                        if (!clearBeforeDate.HasValue)
                            return BadRequest(new { success = false, message = "Tanggal harus diisi untuk periode kustom" });
                        cutoffDate = clearBeforeDate.Value;
                        break;
                    default:
                        return BadRequest(new { success = false, message = "Periode tidak valid" });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    int deletedTransactions = 0;
                    int deletedVehicles = 0;
                    int deletedTickets = 0;
                    int deletedImages = 0;

                    // Delete transactions
                    if (clearOptions.Contains("transactions"))
                    {
                        var transactions = await _context.ParkingTransactions
                            .Where(t => clearPeriod == "all" || t.EntryTime < cutoffDate)
                            .ToListAsync();
                        _context.ParkingTransactions.RemoveRange(transactions);
                        deletedTransactions = transactions.Count;
                    }

                    // Delete vehicles
                    if (clearOptions.Contains("vehicles"))
                    {
                        var vehicles = await _context.Vehicles
                            .Where(v => clearPeriod == "all" || v.EntryTime < cutoffDate)
                            .ToListAsync();
                        _context.Vehicles.RemoveRange(vehicles);
                        deletedVehicles = vehicles.Count;
                    }

                    // Delete tickets
                    if (clearOptions.Contains("tickets"))
                    {
                        var tickets = await _context.ParkingTickets
                            .Where(t => clearPeriod == "all" || t.IssueTime < cutoffDate)
                            .ToListAsync();
                        _context.ParkingTickets.RemoveRange(tickets);
                        deletedTickets = tickets.Count;
                    }

                    // Delete images
                    if (clearOptions.Contains("images"))
                    {
                        var imagesToDelete = new List<string>();

                        // Get entry photos
                        if (clearOptions.Contains("vehicles"))
                        {
                            var vehicleImages = await _context.Vehicles
                                .Where(v => clearPeriod == "all" || v.EntryTime < cutoffDate)
                                .Select(v => new { v.EntryPhotoPath, v.ExitPhotoPath })
                                .ToListAsync();

                            imagesToDelete.AddRange(
                                vehicleImages
                                    .Where(v => !string.IsNullOrEmpty(v.EntryPhotoPath))
                                    .Select(v => v.EntryPhotoPath!)
                            );
                            imagesToDelete.AddRange(
                                vehicleImages
                                    .Where(v => !string.IsNullOrEmpty(v.ExitPhotoPath))
                                    .Select(v => v.ExitPhotoPath!)
                            );
                        }

                        // Get ticket barcodes
                        if (clearOptions.Contains("tickets"))
                        {
                            var ticketImages = await _context.ParkingTickets
                                .Where(t => clearPeriod == "all" || t.IssueTime < cutoffDate)
                                .Select(t => t.BarcodeImagePath)
                                .Where(path => !string.IsNullOrEmpty(path))
                                .ToListAsync();

                            imagesToDelete.AddRange(ticketImages!);
                        }

                        // Delete physical image files
                        foreach (var imagePath in imagesToDelete)
                        {
                            var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, imagePath.TrimStart('/'));
                            if (System.IO.File.Exists(fullPath))
                            {
                                System.IO.File.Delete(fullPath);
                                deletedImages++;
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Build success message
                    var deletedItems = new List<string>();
                    if (deletedTransactions > 0) deletedItems.Add($"{deletedTransactions} transaksi");
                    if (deletedVehicles > 0) deletedItems.Add($"{deletedVehicles} data kendaraan");
                    if (deletedTickets > 0) deletedItems.Add($"{deletedTickets} tiket");
                    if (deletedImages > 0) deletedItems.Add($"{deletedImages} foto");

                    var message = deletedItems.Count > 0
                        ? $"Berhasil menghapus {string.Join(", ", deletedItems)}"
                        : "Tidak ada data yang dihapus";

                    return Json(new { success = true, message = message });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new Exception("Gagal menghapus data: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing data");
                return StatusCode(500, new { success = false, message = "Gagal menghapus data: " + ex.Message });
            }
        }

        [Authorize]
        public IActionResult LiveDashboard()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var activeVehicles = await _context.Vehicles.CountAsync(v => v.IsParked);
                var totalSpaces = await _context.ParkingSpaces.CountAsync();
                var availableSpaces = totalSpaces - activeVehicles;

                var today = DateTime.Today;
                var todayTransactions = await _context.ParkingTransactions
                    .Where(t => t.EntryTime.Date == today)
                    .ToListAsync();

                var todayRevenue = todayTransactions.Sum(t => t.TotalAmount);
                var todayTransactionCount = todayTransactions.Count;

                return Json(new
                {
                    success = true,
                    activeVehicles,
                    availableSpaces,
                    todayTransactions = todayTransactionCount,
                    todayRevenue
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard stats");
                return StatusCode(500, new { success = false, message = "Gagal mengambil statistik dashboard" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentEntries()
        {
            try
            {
                var recentEntries = await _context.Vehicles
                    .Where(v => v.IsParked)
                    .OrderByDescending(v => v.EntryTime)
                    .Take(5)
                    .Select(v => new
                    {
                        timestamp = v.EntryTime.ToString("dd/MM/yyyy HH:mm"),
                        vehicleNumber = v.VehicleNumber,
                        vehicleType = v.VehicleType
                    })
                    .ToListAsync();

                return Json(recentEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent entries");
                return StatusCode(500, new { success = false, message = "Gagal memuat data kendaraan masuk terbaru" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentExits()
        {
            try
            {
                var recentExits = await _context.ParkingTransactions
                    .Where(t => t.ExitTime != default(DateTime))
                    .OrderByDescending(t => t.ExitTime)
                    .Take(5)
                    .Select(t => new
                    {
                        exitTime = t.ExitTime.ToString("dd/MM/yyyy HH:mm"),
                        vehicleNumber = t.Vehicle.VehicleNumber,
                        duration = CalculateDuration(t.EntryTime, t.ExitTime),
                        totalAmount = t.TotalAmount
                    })
                    .ToListAsync();

                return Json(recentExits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent exits");
                return StatusCode(500, new { success = false, message = "Gagal memuat data kendaraan keluar terbaru" });
            }
        }

        private string CalculateDuration(DateTime start, DateTime end)
        {
            var duration = end - start;
            var hours = Math.Floor(duration.TotalHours);
            var minutes = duration.Minutes;

            if (hours > 0)
            {
                return $"{hours} jam {minutes} menit";
            }
            return $"{minutes} menit";
        }
    }

    public class TransactionRequest
    {
        public string VehicleNumber { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "Cash";
    }
}