using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android;
using Android.Content;
using Android.Net.Wifi;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Nexo.Droid;
using Nexo.Models;
using Nexo.Services;
using Xamarin.Essentials;
using Xamarin.Forms;
using Newtonsoft.Json;
using Java.Lang.Reflect;

[assembly: Xamarin.Forms.Dependency(typeof(WiFiSharingService))]
namespace Nexo.Droid
{
    public class WiFiSharingService : IWiFiSharingService
    {
        private Context context;
        private WifiManager wifiManager;
        private ConnectionSettings settings;
        private bool isServerRunning = false;
        private bool isAccessPointActive = false;
        private CancellationTokenSource serverCancellationToken;
        private TcpListener tcpListener;

        public bool IsAccessPointActive => isAccessPointActive;
        public bool IsServerRunning => isServerRunning;

        public WiFiSharingService()
        {
            context = Platform.CurrentActivity ?? Android.App.Application.Context;
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
                System.Diagnostics.Debug.WriteLine($"StartAccessPointAsync called with: {hotspotName}");

                // Guide user to enable hotspot manually (works for all Android versions)
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Enable Hotspot",
                        $"Please enable WiFi Hotspot manually:\n\n" +
                        $"1. Go to Settings → WiFi Hotspot\n" +
                        $"2. Set Network name: {hotspotName}\n" +
                        $"3. Set Password: {password}\n" +
                        $"4. Turn on the hotspot\n" +
                        $"5. Return to the app and start the server",
                        "OK");
                });

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
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Turn Off Hotspot",
                        "Please turn off the WiFi Hotspot manually from Settings.",
                        "OK");
                });

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
                System.Diagnostics.Debug.WriteLine($"ConnectToDeviceAsync called for: {ssid}");

                // Guide user to connect manually
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Connect to WiFi",
                        $"Please connect to WiFi manually:\n\n" +
                        $"1. Go to Settings → WiFi\n" +
                        $"2. Connect to: {ssid}\n" +
                        $"3. Password: {password}\n" +
                        $"4. Return to the app",
                        "OK");
                });

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
                {
                    System.Diagnostics.Debug.WriteLine("Server already running");
                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"Starting file server on port {port}");

                serverCancellationToken = new CancellationTokenSource();

                // Start simple HTTP server
                _ = Task.Run(() => StartSimpleHttpServer(port, serverCancellationToken.Token));

                isServerRunning = true;
                System.Diagnostics.Debug.WriteLine("File server started successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartFileServerAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task StartSimpleHttpServer(int port, CancellationToken cancellationToken)
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();

                System.Diagnostics.Debug.WriteLine($"HTTP Server listening on port {port}");

                while (!cancellationToken.IsCancellationRequested && isServerRunning)
                {
                    try
                    {
                        var tcpClient = await AcceptTcpClientAsync(tcpListener, cancellationToken);
                        if (tcpClient != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Client connected to server");
                            _ = Task.Run(() => HandleHttpRequest(tcpClient), cancellationToken);
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        System.Diagnostics.Debug.WriteLine($"HTTP Server Accept Error: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTP Server Error: {ex.Message}");
            }
            finally
            {
                tcpListener?.Stop();
                System.Diagnostics.Debug.WriteLine("HTTP Server stopped");
            }
        }

        private async Task<TcpClient> AcceptTcpClientAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                var tcpClientTask = listener.AcceptTcpClientAsync();
                var delayTask = Task.Delay(1000, cancellationToken);

                var completedTask = await Task.WhenAny(tcpClientTask, delayTask);

                if (completedTask == tcpClientTask && !cancellationToken.IsCancellationRequested)
                {
                    return await tcpClientTask;
                }

                return null;
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                    System.Diagnostics.Debug.WriteLine($"AcceptTcpClientAsync Error: {ex.Message}");
                return null;
            }
        }

        private async Task HandleHttpRequest(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    // Read HTTP request line
                    var requestLine = await reader.ReadLineAsync();
                    System.Diagnostics.Debug.WriteLine($"HTTP Request: {requestLine}");

                    // Skip headers
                    string line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        // Skip headers
                    }

                    // Create response
                    var deviceInfo = new
                    {
                        device = settings.DeviceName,
                        timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                        status = "online",
                        ip = GetLocalIpAddress()
                    };

                    var jsonResponse = JsonConvert.SerializeObject(deviceInfo, Formatting.Indented);
                    var responseBytes = Encoding.UTF8.GetBytes(jsonResponse);

                    // Send HTTP response
                    await writer.WriteLineAsync("HTTP/1.1 200 OK");
                    await writer.WriteLineAsync("Content-Type: application/json");
                    await writer.WriteLineAsync("Access-Control-Allow-Origin: *");
                    await writer.WriteLineAsync($"Content-Length: {responseBytes.Length}");
                    await writer.WriteLineAsync();
                    await writer.WriteAsync(jsonResponse);
                    await writer.FlushAsync();

                    System.Diagnostics.Debug.WriteLine($"HTTP Response sent: {jsonResponse}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleHttpRequest Error: {ex.Message}");
            }
        }

        private string GetLocalIpAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetLocalIpAddress Error: {ex.Message}");
            }
            return "127.0.0.1";
        }

        public async Task<bool> StopFileServerAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Stopping file server");
                isServerRunning = false;
                serverCancellationToken?.Cancel();
                tcpListener?.Stop();
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
                System.Diagnostics.Debug.WriteLine("=== Starting Device Scan ===");

                // Check permissions first
                if (!await HasLocationPermission())
                {
                    System.Diagnostics.Debug.WriteLine("Location permission not granted - requesting");
                    var granted = await RequestLocationPermission();
                    if (!granted)
                    {
                        System.Diagnostics.Debug.WriteLine("Location permission denied");

                        // Add a message device to show permission issue
                        devices.Add(new WiFiDevice
                        {
                            Name = "⚠️ Location Permission Required",
                            IpAddress = "0.0.0.0",
                            Port = 0,
                            IsConnected = false,
                            Role = DeviceRole.AccessPoint,
                            DeviceId = "permission_required"
                        });

                        return devices;
                    }
                }

                // Try WiFi scanning first
                await ScanWiFiNetworks(devices);

                // Scan common hotspot IPs
                await ScanCommonIPs(devices);

                // Add current device info for testing
                devices.Add(new WiFiDevice
                {
                    Name = $"📱 This Device ({settings.DeviceName})",
                    IpAddress = GetLocalIpAddress(),
                    Port = settings.ServerPort,
                    IsConnected = true,
                    Role = DeviceRole.AccessPoint,
                    DeviceId = "local_device"
                });

                System.Diagnostics.Debug.WriteLine($"=== Scan Complete: Found {devices.Count} devices ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScanForDevicesAsync Error: {ex.Message}");

                // Add error device for debugging
                devices.Add(new WiFiDevice
                {
                    Name = $"❌ Scan Error: {ex.Message}",
                    IpAddress = "0.0.0.0",
                    Port = 0,
                    IsConnected = false,
                    Role = DeviceRole.AccessPoint,
                    DeviceId = "error"
                });
            }

            return devices;
        }

        private async Task ScanWiFiNetworks(List<WiFiDevice> devices)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Scanning WiFi networks...");

                bool scanStarted = wifiManager.StartScan();
                System.Diagnostics.Debug.WriteLine($"WiFi scan started: {scanStarted}");

                if (scanStarted)
                {
                    // Wait for scan to complete
                    await Task.Delay(5000);

                    var scanResults = wifiManager.ScanResults;
                    System.Diagnostics.Debug.WriteLine($"Found {scanResults?.Count ?? 0} WiFi networks");

                    if (scanResults != null)
                    {
                        foreach (var result in scanResults)
                        {
                            try
                            {
                                string ssid = GetScanResultSsid(result);
                                if (!string.IsNullOrEmpty(ssid))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Network found: {ssid}");

                                    if (ssid.StartsWith("FileShare_") || ssid.Contains("FileShare"))
                                    {
                                        devices.Add(new WiFiDevice
                                        {
                                            Name = $"📡 {ssid}",
                                            IpAddress = "192.168.43.1", // Default hotspot IP
                                            Port = 8080,
                                            IsConnected = false,
                                            Role = DeviceRole.AccessPoint,
                                            DeviceId = GetScanResultBssid(result) ?? "unknown"
                                        });

                                        System.Diagnostics.Debug.WriteLine($"Added FileShare device: {ssid}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error processing scan result: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WiFi scan error: {ex.Message}");
            }
        }

        private async Task ScanCommonIPs(List<WiFiDevice> devices)
        {
            System.Diagnostics.Debug.WriteLine("Scanning common IPs...");

            var commonIps = new[]
            {
                "192.168.43.1",  // Android hotspot default
                "192.168.1.1",   // Common router
                "192.168.0.1",   // Common router  
                "10.0.0.1",      // Some routers
                "192.168.1.100", // Common device IP
                "192.168.1.101", // Common device IP
                "192.168.43.100" // Hotspot client IP
            };

            var tasks = new List<Task>();
            foreach (var ip in commonIps)
            {
                tasks.Add(TestIPAndAddDevice(ip, devices));
            }

            await Task.WhenAll(tasks);
        }

        private async Task TestIPAndAddDevice(string ip, List<WiFiDevice> devices)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Testing connection to {ip}:8080");

                if (await TestConnection(ip, 8080))
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Found active server at {ip}:8080");

                    try
                    {
                        // Try to get device info
                        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                        var response = await httpClient.GetStringAsync($"http://{ip}:8080/");

                        var deviceResponse = JsonConvert.DeserializeObject<DeviceInfoResponse>(response);

                        devices.Add(new WiFiDevice
                        {
                            Name = $"🔗 {deviceResponse?.Device ?? "Unknown Device"}",
                            IpAddress = ip,
                            Port = 8080,
                            IsConnected = true,
                            Role = DeviceRole.AccessPoint,
                            DeviceId = $"server_{ip}"
                        });

                        System.Diagnostics.Debug.WriteLine($"Added connected device: {deviceResponse?.Device} at {ip}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not get device info from {ip}: {ex.Message}");

                        devices.Add(new WiFiDevice
                        {
                            Name = $"🔗 File Server ({ip})",
                            IpAddress = ip,
                            Port = 8080,
                            IsConnected = true,
                            Role = DeviceRole.AccessPoint,
                            DeviceId = $"server_{ip}"
                        });
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ No response from {ip}:8080");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error testing {ip}: {ex.Message}");
            }
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

        private string GetScanResultSsid(ScanResult result)
        {
            if (result == null) return null;

            try
            {
                // Use reflection to get SSID
                var ssidField = result.Class.GetDeclaredField("SSID");
                if (ssidField != null)
                {
                    ssidField.Accessible = true;
                    var ssidValue = ssidField.Get(result)?.ToString()?.Trim('"');
                    return ssidValue;
                }
            }
            catch { }

            try
            {
                // Parse from toString
                var toString = result.ToString();
                if (toString?.Contains("SSID: ") == true)
                {
                    var start = toString.IndexOf("SSID: ") + 6;
                    var end = toString.IndexOf(",", start);
                    if (end > start)
                    {
                        return toString.Substring(start, end - start).Trim().Trim('"');
                    }
                }
            }
            catch { }

            return null;
        }

        private string GetScanResultBssid(ScanResult result)
        {
            if (result == null) return null;

            try
            {
                var bssidField = result.Class.GetDeclaredField("BSSID");
                if (bssidField != null)
                {
                    bssidField.Accessible = true;
                    return bssidField.Get(result)?.ToString();
                }
            }
            catch { }

            return "unknown";
        }

        private async Task<bool> HasLocationPermission()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            System.Diagnostics.Debug.WriteLine($"Location permission status: {status}");
            return status == PermissionStatus.Granted;
        }

        private async Task<bool> RequestLocationPermission()
        {
            try
            {
                var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                System.Diagnostics.Debug.WriteLine($"Location permission requested, result: {status}");
                return status == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error requesting location permission: {ex.Message}");
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

    public class DeviceInfoResponse
    {
        [JsonProperty("device")]
        public string Device { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("ip")]
        public string Ip { get; set; }
    }
}