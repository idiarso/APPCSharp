using System;
using System.Collections.Generic;

namespace ParkIRC.Models.ViewModels
{
    public class ParkingActivity
    {
        public string VehicleType { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public decimal Fee { get; set; }
        public string ParkingType { get; set; } = string.Empty;
    }

    public class OccupancyData
    {
        public string Hour { get; set; } = string.Empty;
        public int Count { get; set; }
        public double OccupancyPercentage { get; set; }
    }
    
    public class OccupancyDataComparer : IEqualityComparer<OccupancyData>
    {
        public bool Equals(OccupancyData x, OccupancyData y)
        {
            return x.Hour == y.Hour;
        }

        public int GetHashCode(OccupancyData obj)
        {
            return obj.Hour.GetHashCode();
        }
    }

    public class VehicleDistributionData
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class DashboardStatisticsViewModel
    {
        public int TotalSpaces { get; set; }
        public int AvailableSpaces { get; set; }
        public decimal DailyRevenue { get; set; }
        public decimal WeeklyRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public List<ParkingActivity> RecentActivity { get; set; } = new();
        public List<OccupancyData> HourlyOccupancy { get; set; } = new();
        public List<VehicleDistributionData> VehicleDistribution { get; set; } = new();
    }
} 