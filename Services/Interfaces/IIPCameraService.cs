using System.Threading.Tasks;

namespace ParkIRC.Services.Interfaces
{
    public interface IIPCameraService
    {
        Task<bool> StartDetection();
        Task<bool> StopDetection();
        Task<string> CaptureImage();
        Task<bool> IsVehiclePresent();
        Task<bool> IsVehicleDetected();
        Task<string> CaptureVehicleImage();
        Task<string> CaptureCCTVImage();
        Task<byte[]> GetCCTVFrame();
    }
} 