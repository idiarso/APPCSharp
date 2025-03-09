using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;

namespace ParkIRC.Services
{
    public interface IIPCameraService
    {
        Task<bool> IsVehicleDetected();
        Task<string?> CaptureVehicleImage();
        Task<bool> ConnectCamera();
        Task DisconnectCamera();
        bool IsConnected { get; }
    }

    public class IPCameraService : IIPCameraService, IDisposable
    {
        private readonly ILogger<IPCameraService> _logger;
        private readonly IConfiguration _configuration;
        private VideoCapture? _capture;
        private bool _isConnected;
        private readonly string _cameraUrl;
        private readonly string _username;
        private readonly string _password;

        public bool IsConnected => _isConnected;

        public IPCameraService(ILogger<IPCameraService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            // Get camera configuration
            _cameraUrl = _configuration["IPCamera:Url"] ?? "";
            _username = _configuration["IPCamera:Username"] ?? "";
            _password = _configuration["IPCamera:Password"] ?? "";
        }

        public async Task<bool> ConnectCamera()
        {
            try
            {
                if (_capture != null)
                {
                    await DisconnectCamera();
                }

                // Create URL with authentication if provided
                string url = _cameraUrl;
                if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
                {
                    var uri = new Uri(_cameraUrl);
                    url = $"{uri.Scheme}://{WebUtility.UrlEncode(_username)}:{WebUtility.UrlEncode(_password)}@{uri.Host}:{uri.Port}{uri.PathAndQuery}";
                }

                _capture = new VideoCapture(url);
                if (!_capture.IsOpened)
                {
                    _logger.LogError("Failed to connect to IP camera at {Url}", _cameraUrl);
                    return false;
                }

                _isConnected = true;
                _logger.LogInformation("Successfully connected to IP camera at {Url}", _cameraUrl);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to IP camera");
                return false;
            }
        }

        public async Task DisconnectCamera()
        {
            try
            {
                if (_capture != null)
                {
                    _capture.Dispose();
                    _capture = null;
                }
                _isConnected = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from IP camera");
            }
        }

        public async Task<bool> IsVehicleDetected()
        {
            try
            {
                if (!_isConnected || _capture == null)
                {
                    await ConnectCamera();
                }

                if (_capture == null || !_capture.IsOpened)
                {
                    return false;
                }

                using var frame = new Mat();
                if (!_capture.Read(frame))
                {
                    return false;
                }

                // Convert to grayscale for processing
                using var gray = new Mat();
                CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);

                // Apply background subtraction or motion detection
                using var blur = new Mat();
                CvInvoke.GaussianBlur(gray, blur, new System.Drawing.Size(21, 21), 0);

                // Use adaptive thresholding
                using var thresh = new Mat();
                CvInvoke.AdaptiveThreshold(blur, thresh, 255,
                    AdaptiveThresholdType.GaussianC,
                    ThresholdType.Binary, 11, 2);

                // Find contours
                using var contours = new VectorOfVectorOfPoint();
                CvInvoke.FindContours(thresh, contours, null,
                    RetrType.External,
                    ChainApproxMethod.ChainApproxSimple);

                // Check if any contour is large enough to be a vehicle
                double minVehicleArea = frame.Width * frame.Height * 0.1; // 10% of frame
                for (int i = 0; i < contours.Size; i++)
                {
                    double area = CvInvoke.ContourArea(contours[i]);
                    if (area > minVehicleArea)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting vehicle");
                return false;
            }
        }

        public async Task<string?> CaptureVehicleImage()
        {
            try
            {
                if (!_isConnected || _capture == null)
                {
                    await ConnectCamera();
                }

                if (_capture == null || !_capture.IsOpened)
                {
                    return null;
                }

                using var frame = new Mat();
                if (!_capture.Read(frame))
                {
                    return null;
                }

                // Generate filename
                string fileName = $"vehicle_{DateTime.Now:yyyyMMddHHmmss}.jpg";
                string filePath = Path.Combine("wwwroot", "uploads", "vehicles", fileName);

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                // Save image
                frame.Save(filePath);

                return $"/uploads/vehicles/{fileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing vehicle image");
                return null;
            }
        }

        public void Dispose()
        {
            _capture?.Dispose();
        }
    }
} 