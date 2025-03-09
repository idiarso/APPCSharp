using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Drawing.Printing;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using ParkIRC.Data;
using ParkIRC.Services.Interfaces;

namespace ParkIRC.Services
{
    public class DeviceMonitoringService : BackgroundService, IDeviceMonitoringService
    {
        private readonly ILogger<DeviceMonitoringService> _logger;
        private readonly IIPCameraService _cameraService;
        private readonly IPrinterService _printerService;
        private readonly ApplicationDbContext _context;
        private readonly ConcurrentDictionary<string, DeviceStatus> _deviceStatus;

        public DeviceMonitoringService(
            ILogger<DeviceMonitoringService> logger,
            IIPCameraService cameraService,
            IPrinterService printerService,
            ApplicationDbContext context)
        {
            _logger = logger;
            _cameraService = cameraService;
            _printerService = printerService;
            _context = context;
            _deviceStatus = new ConcurrentDictionary<string, DeviceStatus>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckDevices();
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking devices");
                }
            }
        }

        private async Task CheckDevices()
        {
            // Check Cameras
            await CheckCamera("Entrance");
            await CheckCamera("Exit");

            // Check Printers
            CheckPrinter("Entrance");
            CheckPrinter("Exit");

            // Check Gates
            await CheckGate("Entrance");
            await CheckGate("Exit");

            // Check Database and Server
            await CheckDatabase();
            await CheckServer();
        }

        private async Task CheckCamera(string location)
        {
            try
            {
                bool isWorking;
                if (location == "Entrance")
                {
                    isWorking = await _cameraService.ConnectCamera();
                }
                else
                {
                    isWorking = true; // Assume exit camera is working for now
                }
                UpdateDeviceStatus($"Camera_{location}", isWorking);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking camera at {Location}", location);
                UpdateDeviceStatus($"Camera_{location}", false);
            }
        }

        private void CheckPrinter(string location)
        {
            try
            {
                var printers = PrinterSettings.InstalledPrinters;
                var printerName = location == "Entrance" ? "EntryPrinter" : "ExitPrinter";
                bool printerExists = false;

                if (location == "Entrance" || location == "Exit")
                {
                    printerExists = _printerService.CheckPrinterStatus();
                }

                UpdateDeviceStatus($"Printer_{location}", printerExists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking printer at {Location}", location);
                UpdateDeviceStatus($"Printer_{location}", false);
            }
        }

        private async Task CheckGate(string location)
        {
            try
            {
                // TODO: Implement actual gate check
                UpdateDeviceStatus($"Gate_{location}", true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking gate at {Location}", location);
                UpdateDeviceStatus($"Gate_{location}", false);
            }
        }

        private void UpdateDeviceStatus(string deviceId, bool isWorking)
        {
            var status = _deviceStatus.AddOrUpdate(
                deviceId,
                new DeviceStatus { IsWorking = isWorking, LastChecked = DateTime.Now },
                (key, existing) => new DeviceStatus { IsWorking = isWorking, LastChecked = DateTime.Now }
            );

            if (!status.IsWorking)
            {
                _logger.LogWarning("Device {DeviceId} is not working", deviceId);
            }
        }

        public DeviceStatus GetDeviceStatus(string deviceId)
        {
            return _deviceStatus.GetValueOrDefault(deviceId, new DeviceStatus { IsWorking = false, LastChecked = DateTime.MinValue });
        }
        
        public async Task<bool> CheckEntranceGate()
        {
            var status = GetDeviceStatus("Gate_Entrance");
            return status.IsWorking;
        }

        public async Task<bool> CheckExitGate()
        {
            var status = GetDeviceStatus("Gate_Exit");
            return status.IsWorking;
        }

        public async Task<bool> CheckEntranceCamera()
        {
            var status = GetDeviceStatus("Camera_Entrance");
            return status.IsWorking;
        }

        public async Task<bool> CheckExitCamera()
        {
            var status = GetDeviceStatus("Camera_Exit");
            return status.IsWorking;
        }

        public async Task<bool> CheckEntrancePrinter()
        {
            var status = GetDeviceStatus("Printer_Entrance");
            return status.IsWorking;
        }

        public async Task<bool> CheckExitPrinter()
        {
            var status = GetDeviceStatus("Printer_Exit");
            return status.IsWorking;
        }

        public async Task<bool> CheckServer()
        {
            try
            {
                // Basic check - if we can execute this, server is running
                UpdateDeviceStatus("Server", true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking server status");
                UpdateDeviceStatus("Server", false);
                return false;
            }
        }

        public async Task<bool> CheckDatabase()
        {
            try
            {
                // Try to execute simple query to check database connection
                await _context.Database.CanConnectAsync();
                UpdateDeviceStatus("Database", true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database status");
                UpdateDeviceStatus("Database", false);
                return false;
            }
        }
    }

    public class DeviceStatus
    {
        public bool IsWorking { get; set; }
        public DateTime LastChecked { get; set; }
    }
} 