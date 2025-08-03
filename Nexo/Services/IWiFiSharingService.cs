using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nexo.Models;

namespace Nexo.Services
{
    public interface IWiFiSharingService
    {
        Task<bool> StartAccessPointAsync(string hotspotName, string password);
        Task<bool> StopAccessPointAsync();
        Task<bool> ConnectToDeviceAsync(string ssid, string password);
        Task<bool> StartFileServerAsync(int port = 8080);
        Task<bool> StopFileServerAsync();
        Task<bool> SendFileAsync(string filePath, string targetIp, int port);
        Task<List<WiFiDevice>> ScanForDevicesAsync();
        bool IsAccessPointActive { get; }
        bool IsServerRunning { get; }
        ConnectionSettings GetConnectionSettings();
        void UpdateConnectionSettings(ConnectionSettings settings);
    }
}