using ParkIRC.Models;
using System.Threading.Tasks;

namespace ParkIRC.Services.Interfaces
{
    public interface IPrinterService
    {
        Task<bool> PrintEntryTicket(string ticketNumber, string vehicleNumber, string entryTime, string barcode);
        Task<bool> PrintExitTicket(ExitTicket exitTicket, Vehicle vehicle);
        Task<bool> PrintTicket(ParkingTicket ticket);
    }
} 