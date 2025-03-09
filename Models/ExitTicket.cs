using System;

namespace ParkIRC.Models
{
    public class ExitTicket
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; }
        public string ExitTicketNumber { get; set; }
        public string OriginalTicketNumber { get; set; }
        public string Barcode { get; set; }
        public string BarcodeData { get; set; }
        public string BarcodeImagePath { get; set; }
        public DateTime ExitTime { get; set; }
        public DateTime IssueTime { get; set; }
        public DateTime ExpiryTime { get; set; }
        public decimal ParkingCost { get; set; }
        public decimal Cost { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsUsed { get; set; }
        public DateTime ValidUntil { get; set; }
        public DateTime? UseTime { get; set; }
        
        public int ParkingTicketId { get; set; }
        public virtual ParkingTicket ParkingTicket { get; set; }
        
        public int VehicleId { get; set; }
        public virtual Vehicle Vehicle { get; set; }
    }
} 