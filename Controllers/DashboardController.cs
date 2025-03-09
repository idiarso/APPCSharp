using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParkIRC.Data;
using ParkIRC.Models;
using ParkIRC.Models.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ParkIRC.Services;

namespace ParkIRC.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IDeviceMonitoringService _deviceMonitoring;
        private readonly ApplicationDbContext _context;

        public DashboardController(IDeviceMonitoringService deviceMonitoring, ApplicationDbContext context)
        {
            _deviceMonitoring = deviceMonitoring;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel
            {
                TotalVehicles = await _context.Vehicles.CountAsync(),
                TotalIncome = await _context.ParkingTransactions.SumAsync(t => t.Amount),
                AvailableSpaces = await _context.ParkingSpaces.CountAsync(s => !s.IsOccupied),
                ActiveOperators = await _context.Operators.CountAsync(o => o.IsActive),
                CurrentShift = await _context.Shifts.FirstOrDefaultAsync(s => s.StartTime <= DateTime.Now && s.EndTime >= DateTime.Now),
                DeviceStatus = await GetDeviceStatusModel()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetOccupancyStats()
        {
            var stats = await _context.ParkingSpaces
                .GroupBy(p => p.SpaceType)
                .Select(g => new
                {
                    VehicleType = g.Key,
                    Total = g.Count(),
                    Occupied = g.Count(p => p.IsOccupied),
                    Available = g.Count(p => !p.IsOccupied && p.IsActive)
                })
                .ToListAsync();

            return Json(stats);
        }

        [HttpGet]
        public async Task<IActionResult> GetHourlyStats()
        {
            var today = DateTime.Today;
            var stats = await _context.ParkingTransactions
                .Where(t => t.EntryTime.Date == today)
                .GroupBy(t => t.EntryTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    Count = g.Count(),
                    Income = g.Sum(t => t.TotalAmount)
                })
                .OrderBy(x => x.Hour)
                .ToListAsync();

            return Json(stats);
        }

        [HttpGet]
        public async Task<IActionResult> GetDeviceStatus()
        {
            var status = await GetDeviceStatusModel();
            return Json(status);
        }

        private async Task<DeviceStatusViewModel> GetDeviceStatusModel()
        {
            return new DeviceStatusViewModel
            {
                EntranceGate = await _deviceMonitoring.CheckEntranceGate(),
                ExitGate = await _deviceMonitoring.CheckExitGate(),
                EntranceCamera = await _deviceMonitoring.CheckEntranceCamera(),
                ExitCamera = await _deviceMonitoring.CheckExitCamera(),
                EntrancePrinter = await _deviceMonitoring.CheckEntrancePrinter(),
                ExitPrinter = await _deviceMonitoring.CheckExitPrinter(),
                Server = await _deviceMonitoring.CheckServer(),
                Database = await _deviceMonitoring.CheckDatabase()
            };
        }

        public async Task<IActionResult> GetDeviceStatusPartial()
        {
            var status = await GetDeviceStatusModel();
            return PartialView("_DeviceStatus", status);
        }

        public async Task<IActionResult> GetStatisticsPartial()
        {
            var model = new DashboardViewModel
            {
                TotalVehicles = await _context.Vehicles.CountAsync(),
                TotalIncome = await _context.ParkingTransactions.SumAsync(t => t.Amount),
                AvailableSpaces = await _context.ParkingSpaces.CountAsync(s => !s.IsOccupied),
                ActiveOperators = await _context.Operators.CountAsync(o => o.IsActive)
            };

            return PartialView("_Statistics", model);
        }

        public async Task<IActionResult> GetShiftInfoPartial()
        {
            var currentShift = await _context.Shifts
                .FirstOrDefaultAsync(s => s.StartTime <= DateTime.Now && s.EndTime >= DateTime.Now);

            return PartialView("_ShiftInfo", currentShift);
        }
    }
} 