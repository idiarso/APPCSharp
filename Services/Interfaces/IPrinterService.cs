using ParkIRC.Models;
using System.Threading.Tasks;

namespace ParkIRC.Services.Interfaces
{
    public interface IPrinterService
    {
        Task<bool> PrintTicket(ParkingTicket ticket);
        Task<bool> PrintReceipt(ParkingTransaction transaction);
        bool CheckPrinterStatus();
        string GetDefaultPrinter();
        List<string> GetAvailablePrinters();
        Task<bool> PrintExitTicket(ExitTicket exitTicket, Vehicle vehicle);
    }
} 