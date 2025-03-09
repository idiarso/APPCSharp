using System;

namespace ParkIRC.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalVehicles { get; set; }
        public decimal TotalIncome { get; set; }
        public int AvailableSpaces { get; set; }
        public int ActiveOperators { get; set; }
        public Shift? CurrentShift { get; set; }
        public DeviceStatusViewModel DeviceStatus { get; set; } = new();
    }

    public class DeviceStatusViewModel
    {
        public bool EntranceGate { get; set; }
        public bool ExitGate { get; set; }
        public bool EntranceCamera { get; set; }
        public bool ExitCamera { get; set; }
        public bool EntrancePrinter { get; set; }
        public bool ExitPrinter { get; set; }
        public bool Server { get; set; }
        public bool Database { get; set; }
    }
} 