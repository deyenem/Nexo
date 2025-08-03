using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Android.Content;
using Android.Net.Wifi;
using Nexo.Droid;
using Nexo.Models;
using Nexo.Services;
using Xamarin.Essentials;
using Xamarin.Forms;
using Newtonsoft.Json;

[assembly: Dependency(typeof(WiFiSharingService))]
namespace Nexo.Droid
{
    public class WiFiSharingService : IWiFiSharingService
    {
        private Context context;
        private WifiManager wifiManager;
        private ConnectionSettings settings;
        private HttpListener httpListener;
        private bool isServerRunning = false;
        private bool isAccessPointActive = false;

        public bool IsAccessPointActive => isAccessPointActive;
        public bool IsServerRunning => isServerRunning;

        public WiFiSharingService()
        {
            context = Xamarin.Essentials.Platform.CurrentActivity ?? Android.App.Application.Context;
            wifiManager = context.GetSystemService(Context.WifiService) as WifiManager;
            LoadSettings();
        }

        private async void LoadSettings()
        {
            try
            {
                var hotspotName = await SecureStorage.GetAsync("wifi_hotspot_name") ?? $"FileShare_{DeviceInfo.Name}";
                var password = await SecureStorage.GetAsync("wifi_password") ?? "fileshare123";
                var portString = await SecureStorage.GetAsync("wifi_port") ?? "8080";
                var deviceName = await SecureStorage.GetAsync("device_name") ?? DeviceInfo.Name;

                settings = new ConnectionSettings
                {
                    HotspotName = hotspotName,
                    Password = password,
                    ServerPort = int.TryParse(portString, out var port) ? port : 8080,
                    DeviceName = deviceName
                };
            }
            catch
            {
                settings = new ConnectionSettings
                {
                    HotspotName = $"FileShare_{DeviceInfo.Name}",
                    Password = "fileshare123",
                    ServerPort = 8080,
                    DeviceName = DeviceInfo.Name
                };
            }
        }

        public async Task<bool> StartAccessPointAsync(string hotspotName, string password)
        {
            try
            {
                // Note: Creating WiFi hotspot programmatically is restricted in Android 10+
                // This is a simplified implementation that would need additional permissions
                // and potentially root access on newer Android versions

                isAccessPointActive = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartAccessPointAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StopAccessPointAsync()
        {
            try
            {
                isAccessPointActive = false;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StopAccessPointAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ConnectToDeviceAsync(string ssid, string password)
        {
            try
            {
                // This would require WiFi connection logic
                // Implementation would depend on Android version and permissions
                await Task.Delay(1000); // Simulate connection attempt
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConnectToDeviceAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StartFileServerAsync(int port = 8080)
        {
            try
            {
                if (isServerRunning)
                    return true;

                // For Android, we would implement a simple HTTP server
                // This is a simplified implementation
                isServerRunning = true;

                // In a real implementation, you would start an HTTP server here
                await Task.Run(() => StartHttpServer(port));

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartFileServerAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task StartHttpServer(int port)
        {
            // This is a simplified HTTP server implementation
            // In a production app, you would use a proper HTTP server library
            try
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();

                while (isServerRunning)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClient(client));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTP Server Error: {ex.Message}");
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                using (var writer = new StreamWriter(stream))
                {
                    var request = await reader.ReadLineAsync();

                    // Simple HTTP response
                    await writer.WriteLineAsync("HTTP/1.1 200 OK");
                    await writer.WriteLineAsync("Content-Type: application/json");
                    await writer.WriteLineAsync();
                    await writer.WriteLineAsync(JsonConvert.SerializeObject(new { device = settings.DeviceName }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleClient Error: {ex.Message}");
            }
            finally
            {
                client?.Close();
            }
        }

        public async Task<bool> StopFileServerAsync()
        {
            try
            {
                isServerRunning = false;
                httpListener?.Stop();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StopFileServerAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendFileAsync(string filePath, string targetIp, int port)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                using var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await httpClient.PostAsync($"http://{targetIp}:{port}/upload", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SendFileAsync Error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<WiFiDevice>> ScanForDevicesAsync()
        {
            var devices = new List<WiFiDevice>();

            try
            {
                // Scan for WiFi networks that match our naming pattern
                var wifiScanResults = wifiManager.ScanResults;

                foreach (var result in wifiScanResults)
                {
                    if (result.Ssid.StartsWith("FileShare_"))
                    {
                        devices.Add(new WiFiDevice
                        {
                            Name = result.Ssid,
                            IpAddress = "192.168.43.1", // Default hotspot IP
                            Port = 8080,
                            IsConnected = false,
                            Role = DeviceRole.AccessPoint,
                            DeviceId = result.Bssid
                        });
                    }
                }

                // Also check for active connections on common IPs
                var commonIps = new[] { "192.168.43.1", "192.168.1.1", "192.168.0.1" };

                foreach (var ip in commonIps)
                {
                    if (await TestConnection(ip, 8080))
                    {
                        // Try to get device info
                        try
                        {
                            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                            var response = await httpClient.GetStringAsync($"http://{ip}:8080/");
                            var deviceInfo = JsonConvert.DeserializeObject<dynamic>(response);

                            devices.Add(new WiFiDevice
                            {
                                Name = deviceInfo.device ?? "Unknown Device",
                                IpAddress = ip,
                                Port = 8080,
                                IsConnected = true,
                                Role = DeviceRole.AccessPoint
                            });
                        }
                        catch
                        {
                            devices.Add(new WiFiDevice
                            {
                                Name = $"File Server ({ip})",
                                IpAddress = ip,
                                Port = 8080,
                                IsConnected = true,
                                Role = DeviceRole.AccessPoint
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScanForDevices Error: {ex.Message}");
            }

            return devices;
        }

        private async Task<bool> TestConnection(string ip, int port)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                var timeoutTask = Task.Delay(3000);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                return completedTask == connectTask && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        public ConnectionSettings GetConnectionSettings()
        {
            return settings ?? new ConnectionSettings();
        }

        public void UpdateConnectionSettings(ConnectionSettings newSettings)
        {
            settings = newSettings;
        }
    }
}