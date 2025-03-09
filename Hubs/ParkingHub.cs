using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using ParkIRC.Models;
using ParkIRC.Models.ViewModels;

namespace ParkIRC.Hubs
{
    public class ParkingHub : Hub
    {
        /// <summary>
        /// Called by clients to update dashboard data for all clients
        /// </summary>
        /// <param name="data">Dashboard data to be broadcasted</param>
        public async Task UpdateDashboard(DashboardViewModel data)
        {
            await Clients.All.SendAsync("ReceiveDashboardUpdate", data);
        }

        /// <summary>
        /// Called by clients to notify about new vehicle entry
        /// </summary>
        /// <param name="vehicleNumber">The vehicle number that entered</param>
        public async Task NotifyVehicleEntry(string vehicleNumber)
        {
            await Clients.All.SendAsync("VehicleEntryNotification", vehicleNumber);
        }

        /// <summary>
        /// Called by clients to notify about vehicle exit
        /// </summary>
        /// <param name="vehicleNumber">The vehicle number that exited</param>
        public async Task NotifyVehicleExit(string vehicleNumber)
        {
            await Clients.All.SendAsync("VehicleExitNotification", vehicleNumber);
        }
        
        /// <summary>
        /// Called by ANPR system to broadcast plate detection result
        /// </summary>
        /// <param name="licensePlate">The detected license plate number</param>
        /// <param name="isSuccessful">Whether the detection was successful</param>
        public async Task NotifyPlateDetection(string licensePlate, bool isSuccessful)
        {
            await Clients.All.SendAsync("PlateDetectionResult", new { 
                licensePlate, 
                isSuccessful 
            });
        }
        
        /// <summary>
        /// Called by systems to notify when barrier opens or closes
        /// </summary>
        /// <param name="isEntry">True if entry barrier, false if exit barrier</param>
        /// <param name="isOpen">True if opened, false if closed</param>
        public async Task NotifyBarrierStatus(bool isEntry, bool isOpen)
        {
            await Clients.All.SendAsync("BarrierStatusChanged", new { 
                isEntry, 
                isOpen,
                barrierType = isEntry ? "Entry" : "Exit",
                status = isOpen ? "Open" : "Closed"
            });
        }
    }
} 