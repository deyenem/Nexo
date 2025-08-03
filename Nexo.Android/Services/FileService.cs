using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.Content;
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Nexo.Droid;
using Nexo.Models;
using Nexo.Services;
using Xamarin.Forms;

[assembly: Dependency(typeof(FileService))]
namespace Nexo.Droid
{
    public class FileService : IFileService
    {
        private Context context;
        private const int STORAGE_PERMISSION_REQUEST = 1001;

        public FileService()
        {
            context = Xamarin.Essentials.Platform.CurrentActivity ?? Android.App.Application.Context;
        }

        public async Task<List<FileItem>> GetFilesAsync(string path = null)
        {
            var files = new List<FileItem>();

            try
            {
                string targetPath;
                if (string.IsNullOrEmpty(path))
                {
                    targetPath = GetExternalStoragePath();
                }
                else
                {
                    targetPath = path;
                }

                if (!Directory.Exists(targetPath))
                {
                    return files;
                }

                var directoryInfo = new DirectoryInfo(targetPath);

                // Add directories first
                foreach (var dir in directoryInfo.GetDirectories().OrderBy(d => d.Name))
                {
                    try
                    {
                        files.Add(new FileItem
                        {
                            Name = dir.Name,
                            Path = dir.FullName,
                            IsDirectory = true,
                            Size = 0,
                            ModifiedDate = dir.LastWriteTime
                        });
                    }
                    catch
                    {
                        // Skip directories we can't access
                    }
                }

                // Add files
                foreach (var file in directoryInfo.GetFiles().OrderBy(f => f.Name))
                {
                    try
                    {
                        files.Add(new FileItem
                        {
                            Name = file.Name,
                            Path = file.FullName,
                            IsDirectory = false,
                            Size = file.Length,
                            ModifiedDate = file.LastWriteTime
                        });
                    }
                    catch
                    {
                        // Skip files we can't access
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetFilesAsync Error: {ex.Message}");
            }

            return files;
        }

        public async Task<bool> HasStoragePermissionAsync()
        {
            var status = ContextCompat.CheckSelfPermission(context, Manifest.Permission.ReadExternalStorage);
            return status == Permission.Granted;
        }

        public async Task<bool> RequestStoragePermissionAsync()
        {
            try
            {
                var status = await Xamarin.Essentials.Permissions.RequestAsync<Xamarin.Essentials.Permissions.StorageRead>();
                return status == Xamarin.Essentials.PermissionStatus.Granted;
            }
            catch
            {
                return false;
            }
        }

        public string GetInternalStoragePath()
        {
            return Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
        }

        public string GetExternalStoragePath()
        {
            return Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
        }

        public bool IsExternalStorageAvailable()
        {
            string state = Android.OS.Environment.ExternalStorageState;
            return Android.OS.Environment.MediaMounted.Equals(state);
        }

        public async Task<List<FileItem>> GetCommonDirectoriesAsync()
        {
            var directories = new List<FileItem>();

            try
            {
                var externalStorage = GetExternalStoragePath();

                // Common directories
                var commonPaths = new[]
                {
                    new { Name = "📱 Internal Storage", Path = externalStorage },
                    new { Name = "📁 Documents", Path = Path.Combine(externalStorage, "Documents") },
                    new { Name = "📷 Pictures", Path = Path.Combine(externalStorage, "Pictures") },
                    new { Name = "🎵 Music", Path = Path.Combine(externalStorage, "Music") },
                    new { Name = "🎬 Videos", Path = Path.Combine(externalStorage, "Movies") },
                    new { Name = "📥 Downloads", Path = Path.Combine(externalStorage, "Download") },
                    new { Name = "📱 DCIM", Path = Path.Combine(externalStorage, "DCIM") }
                };

                foreach (var dir in commonPaths)
                {
                    if (Directory.Exists(dir.Path))
                    {
                        var dirInfo = new DirectoryInfo(dir.Path);
                        directories.Add(new FileItem
                        {
                            Name = dir.Name,
                            Path = dir.Path,
                            IsDirectory = true,
                            Size = 0,
                            ModifiedDate = dirInfo.LastWriteTime
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCommonDirectoriesAsync Error: {ex.Message}");
            }

            return directories;
        }
    }
}