using System.Threading.Tasks;

namespace ParkIRC.Services
{
    public interface IDeviceMonitoringService
    {
        Task<bool> CheckEntranceGate();
        Task<bool> CheckExitGate();
        Task<bool> CheckEntranceCamera();
        Task<bool> CheckExitCamera();
        Task<bool> CheckEntrancePrinter();
        Task<bool> CheckExitPrinter();
        Task<bool> CheckServer();
        Task<bool> CheckDatabase();
    }
} 