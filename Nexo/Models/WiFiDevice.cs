using System;

namespace Nexo.Models
{
    public class WiFiDevice
    {
        public string Name { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public bool IsConnected { get; set; }
        public DeviceRole Role { get; set; }
        public string DeviceId { get; set; }
    }

    public enum DeviceRole
    {
        AccessPoint,  // Phone acting as hotspot
        Client        // Phone connecting to hotspot
    }

    public class ConnectionSettings
    {
        public string HotspotName { get; set; } = "FileShare_";
        public string Password { get; set; } = "fileshare123";
        public int ServerPort { get; set; } = 8080;
        public bool IsAccessPoint { get; set; }
        public string DeviceName { get; set; }
    }
}