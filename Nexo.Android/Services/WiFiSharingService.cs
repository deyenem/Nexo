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
        private HttpListener httpListener;
        private bool isServerRunning = false;
        private bool isAccessPointActive = false;
        private CancellationTokenSource serverCancellationToken;

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
                // Check location permission first
                if (!await HasLocationPermission())
                {
                    await RequestLocationPermission();
                }

                // For Android 10+ we need to guide user to enable hotspot manually
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                {
                    return await StartAccessPointModern(hotspotName, password);
                }
                else
                {
                    return await StartAccessPointLegacy(hotspotName, password);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartAccessPointAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> StartAccessPointModern(string hotspotName, string password)
        {
            try
            {
                // For Android 10+, we need to use the Settings panel
                var intent = new Intent(Android.Provider.Settings.ActionWirelessSettings);
                intent.SetFlags(ActivityFlags.NewTask);
                context.StartActivity(intent);

                // Show instructions to user
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Enable Hotspot",
                        $"Please enable WiFi Hotspot manually:\n\n" +
                        $"1. Go to WiFi Hotspot settings\n" +
                        $"2. Set Network name: {hotspotName}\n" +
                        $"3. Set Password: {password}\n" +
                        $"4. Turn on the hotspot\n" +
                        $"5. Return to the app",
                        "OK");
                });

                // Wait for user to return and check hotspot status
                await Task.Delay(5000);
                isAccessPointActive = IsHotspotEnabled();
                return isAccessPointActive;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartAccessPointModern Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> StartAccessPointLegacy(string hotspotName, string password)
        {
            try
            {
                // This method works for older Android versions
                var method = wifiManager.Class.GetMethod("setWifiApEnabled");
                var wifiConfiguration = CreateWifiConfig(hotspotName, password);

                var result = (bool)method.Invoke(wifiManager, wifiConfiguration, true);

                if (result)
                {
                    isAccessPointActive = true;
                    await Task.Delay(3000); // Wait for hotspot to fully start
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartAccessPointLegacy Error: {ex.Message}");
                return false;
            }
        }

        private Java.Lang.Object CreateWifiConfig(string ssid, string password)
        {
            try
            {
                var wifiConfigClass = Java.Lang.Class.ForName("android.net.wifi.WifiConfiguration");
                var wifiConfig = wifiConfigClass.NewInstance();

                var ssidField = wifiConfigClass.GetField("SSID");
                ssidField.Set(wifiConfig, $"\"{ssid}\"");

                var preSharedKeyField = wifiConfigClass.GetField("preSharedKey");
                preSharedKeyField.Set(wifiConfig, $"\"{password}\"");

                return wifiConfig;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateWifiConfig Error: {ex.Message}");
                return null;
            }
        }

        private bool IsHotspotEnabled()
        {
            try
            {
                var method = wifiManager.Class.GetMethod("isWifiApEnabled");
                return (bool)method.Invoke(wifiManager);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> StopAccessPointAsync()
        {
            try
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                {
                    // Guide user to turn off hotspot manually
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Turn Off Hotspot",
                            "Please turn off the WiFi Hotspot manually from Settings.",
                            "OK");
                    });
                }
                else
                {
                    var method = wifiManager.Class.GetMethod("setWifiApEnabled");
                    method.Invoke(wifiManager, null, false);
                }

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
                if (!await HasLocationPermission())
                {
                    await RequestLocationPermission();
                }

                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                {
                    return await ConnectToWifiModern(ssid, password);
                }
                else
                {
                    return await ConnectToWifiLegacy(ssid, password);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConnectToDeviceAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ConnectToWifiModern(string ssid, string password)
        {
            try
            {
                // For Android 10+, guide user to connect manually
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Connect to WiFi",
                        $"Please connect to the WiFi network manually:\n\n" +
                        $"Network: {ssid}\n" +
                        $"Password: {password}\n\n" +
                        $"Go to WiFi settings and connect to this network.",
                        "OK");
                });

                var intent = new Intent(Android.Provider.Settings.ActionWifiSettings);
                intent.SetFlags(ActivityFlags.NewTask);
                context.StartActivity(intent);

                // Wait for user to connect
                await Task.Delay(10000);
                return await IsConnectedToWifi(ssid);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConnectToWifiModern Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ConnectToWifiLegacy(string ssid, string password)
        {
            try
            {
                var wifiConfig = new WifiConfiguration
                {
                    Ssid = $"\"{ssid}\"",
                    PreSharedKey = $"\"{password}\""
                };

                int networkId = wifiManager.AddNetwork(wifiConfig);
                if (networkId != -1)
                {
                    wifiManager.EnableNetwork(networkId, true);
                    wifiManager.Reconnect();

                    // Wait for connection
                    for (int i = 0; i < 20; i++)
                    {
                        await Task.Delay(1000);
                        if (await IsConnectedToWifi(ssid))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConnectToWifiLegacy Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> IsConnectedToWifi(string ssid)
        {
            try
            {
                var wifiInfo = wifiManager.ConnectionInfo;
                string currentSsid = null;

                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                {
                    // For Android 10+, use NetworkCallback or check network info differently
                    var connectivityManager = context.GetSystemService(Context.ConnectivityService) as Android.Net.ConnectivityManager;
                    var activeNetwork = connectivityManager?.ActiveNetwork;
                    var networkInfo = connectivityManager?.GetNetworkInfo(activeNetwork);

                    if (networkInfo != null && networkInfo.IsConnected && networkInfo.Type == Android.Net.ConnectivityType.Wifi)
                    {
                        // For newer versions, we can't easily get SSID due to privacy restrictions
                        // We'll assume connection is successful if we're connected to WiFi
                        return true;
                    }
                }
                else
                {
                    // For older Android versions - try different property access methods
                    try
                    {
                        // Try the most common property name
                        currentSsid = GetWifiSsid(wifiInfo);
                    }
                    catch
                    {
                        // Fallback: try reflection for SSID access
                        try
                        {
                            var ssidField = wifiInfo?.Class?.GetDeclaredField("mSSID");
                            if (ssidField != null)
                            {
                                ssidField.Accessible = true;
                                currentSsid = ssidField.Get(wifiInfo)?.ToString()?.Trim('"');
                            }
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }

                return !string.IsNullOrEmpty(currentSsid) && currentSsid.Equals(ssid, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsConnectedToWifi Error: {ex.Message}");
                return false;
            }
        }

        private string GetWifiSsid(WifiInfo wifiInfo)
        {
            if (wifiInfo == null) return null;

            try
            {
                // Use reflection to get SSID since direct property access is inconsistent
                var method = wifiInfo.Class.GetMethod("getSSID");
                if (method != null)
                {
                    var result = method.Invoke(wifiInfo);
                    return result?.ToString()?.Trim('"');
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetWifiSsid method access error: {ex.Message}");
            }

            try
            {
                // Try field access as fallback
                var ssidField = wifiInfo.Class.GetDeclaredField("mSSID");
                if (ssidField != null)
                {
                    ssidField.Accessible = true;
                    var result = ssidField.Get(wifiInfo);
                    return result?.ToString()?.Trim('"');
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetWifiSsid field access error: {ex.Message}");
            }

            return null;
        }

        public async Task<bool> StartFileServerAsync(int port = 8080)
        {
            try
            {
                if (isServerRunning)
                    return true;

                serverCancellationToken = new CancellationTokenSource();

                // Start HTTP server
                _ = Task.Run(() => StartHttpServer(port, serverCancellationToken.Token));

                isServerRunning = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartFileServerAsync Error: {ex.Message}");
                return false;
            }
        }

        private async Task StartHttpServer(int port, CancellationToken cancellationToken)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Any, port);
                listener.Start();

                System.Diagnostics.Debug.WriteLine($"HTTP Server started on port {port}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var tcpClient = await AcceptTcpClientAsync(listener, cancellationToken);
                        if (tcpClient != null)
                        {
                            _ = Task.Run(() => HandleHttpRequest(tcpClient), cancellationToken);
                        }
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        System.Diagnostics.Debug.WriteLine($"HTTP Server Accept Error: {ex.Message}");
                    }
                }

                listener?.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTP Server Error: {ex.Message}");
            }
        }

        private async Task<TcpClient> AcceptTcpClientAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            try
            {
                var tcpClientTask = listener.AcceptTcpClientAsync();
                var delayTask = Task.Delay(1000, cancellationToken);

                var completedTask = await Task.WhenAny(tcpClientTask, delayTask);

                if (completedTask == tcpClientTask)
                {
                    return await tcpClientTask;
                }

                return null;
            }
            catch
            {
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
                    // Read HTTP request
                    var requestLine = await reader.ReadLineAsync();
                    var headers = new List<string>();

                    string line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        headers.Add(line);
                    }

                    System.Diagnostics.Debug.WriteLine($"HTTP Request: {requestLine}");

                    // Create response
                    var deviceInfo = new
                    {
                        device = settings.DeviceName,
                        timestamp = DateTime.UtcNow,
                        status = "online"
                    };

                    var jsonResponse = JsonConvert.SerializeObject(deviceInfo);
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

        public async Task<bool> StopFileServerAsync()
        {
            try
            {
                isServerRunning = false;
                serverCancellationToken?.Cancel();
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
                // Check location permission
                if (!await HasLocationPermission())
                {
                    System.Diagnostics.Debug.WriteLine("Location permission not granted for WiFi scanning");
                    return devices;
                }

                // Start WiFi scan
                bool scanStarted = wifiManager.StartScan();
                if (!scanStarted)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to start WiFi scan");
                }

                // Wait for scan to complete
                await Task.Delay(3000);

                // Get scan results
                var scanResults = wifiManager.ScanResults;
                System.Diagnostics.Debug.WriteLine($"Found {scanResults?.Count ?? 0} WiFi networks");

                if (scanResults != null)
                {
                    foreach (var result in scanResults)
                    {
                        string ssid = null;
                        string bssid = null;

                        try
                        {
                            // Try to get SSID and BSSID safely using different methods
                            ssid = GetScanResultSsid(result);
                            bssid = GetScanResultBssid(result);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error getting scan result info: {ex.Message}");
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"Found network: {ssid}");

                        if (!string.IsNullOrEmpty(ssid) && ssid.StartsWith("FileShare_"))
                        {
                            devices.Add(new WiFiDevice
                            {
                                Name = ssid,
                                IpAddress = "192.168.43.1", // Default hotspot IP
                                Port = 8080,
                                IsConnected = false,
                                Role = DeviceRole.AccessPoint,
                                DeviceId = bssid ?? "unknown"
                            });

                            System.Diagnostics.Debug.WriteLine($"Added FileShare device: {ssid}");
                        }
                    }
                }

                // Also check for active connections on common IPs
                var commonIps = new[] { "192.168.43.1", "192.168.1.1", "192.168.0.1", "10.0.0.1" };

                foreach (var ip in commonIps)
                {
                    if (await TestConnection(ip, 8080))
                    {
                        System.Diagnostics.Debug.WriteLine($"Found active server at {ip}:8080");

                        try
                        {
                            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                            var response = await httpClient.GetStringAsync($"http://{ip}:8080/");

                            var deviceResponse = JsonConvert.DeserializeObject<DeviceInfoResponse>(response);

                            devices.Add(new WiFiDevice
                            {
                                Name = deviceResponse?.Device ?? "Unknown Device",
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

                System.Diagnostics.Debug.WriteLine($"Total devices found: {devices.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScanForDevices Error: {ex.Message}");
            }

            return devices;
        }

        private string GetScanResultSsid(ScanResult result)
        {
            if (result == null) return null;

            try
            {
                // Try reflection methods since direct property access is inconsistent
                var ssidField = result.Class.GetDeclaredField("SSID");
                if (ssidField != null)
                {
                    ssidField.Accessible = true;
                    var ssidValue = ssidField.Get(result)?.ToString()?.Trim('"');
                    if (!string.IsNullOrEmpty(ssidValue))
                        return ssidValue;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetScanResultSsid field access error: {ex.Message}");
            }

            try
            {
                // Try method invocation approach
                var toString = result.ToString();
                if (!string.IsNullOrEmpty(toString))
                {
                    // Parse SSID from toString output like "SSID: NetworkName, BSSID: ..."
                    var ssidIndex = toString.IndexOf("SSID: ");
                    if (ssidIndex >= 0)
                    {
                        var start = ssidIndex + 6; // Length of "SSID: "
                        var end = toString.IndexOf(",", start);
                        if (end > start)
                        {
                            return toString.Substring(start, end - start).Trim().Trim('"');
                        }
                        else
                        {
                            // If no comma found, take rest of string
                            return toString.Substring(start).Trim().Trim('"');
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetScanResultSsid parsing error: {ex.Message}");
            }

            return null;
        }

        private string GetScanResultBssid(ScanResult result)
        {
            if (result == null) return null;

            try
            {
                // Try reflection methods since direct property access is inconsistent
                var bssidField = result.Class.GetDeclaredField("BSSID");
                if (bssidField != null)
                {
                    bssidField.Accessible = true;
                    var bssidValue = bssidField.Get(result)?.ToString();
                    if (!string.IsNullOrEmpty(bssidValue))
                        return bssidValue;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetScanResultBssid field access error: {ex.Message}");
            }

            try
            {
                // Try method invocation approach
                var toString = result.ToString();
                if (!string.IsNullOrEmpty(toString))
                {
                    // Parse BSSID from toString output like "SSID: NetworkName, BSSID: aa:bb:cc:dd:ee:ff"
                    var bssidIndex = toString.IndexOf("BSSID: ");
                    if (bssidIndex >= 0)
                    {
                        var start = bssidIndex + 7; // Length of "BSSID: "
                        var end = toString.IndexOf(",", start);
                        if (end > start)
                        {
                            return toString.Substring(start, end - start).Trim();
                        }
                        else
                        {
                            // If no comma found, take rest of string
                            return toString.Substring(start).Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetScanResultBssid parsing error: {ex.Message}");
            }

            return "unknown";
        }

        private async Task<bool> TestConnection(string ip, int port)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                var timeoutTask = Task.Delay(3000);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                var isConnected = completedTask == connectTask && client.Connected;

                if (isConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"Successfully connected to {ip}:{port}");
                }

                return isConnected;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TestConnection to {ip}:{port} failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> HasLocationPermission()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            return status == PermissionStatus.Granted;
        }

        private async Task<bool> RequestLocationPermission()
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            return status == PermissionStatus.Granted;
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
        public DateTime Timestamp { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }
}