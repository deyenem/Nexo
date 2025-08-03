using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nexo.Models;

namespace Nexo.Services
{
    public interface IFileService
    {
        Task<List<FileItem>> GetFilesAsync(string path = null);
        Task<bool> HasStoragePermissionAsync();
        Task<bool> RequestStoragePermissionAsync();
        string GetInternalStoragePath();
        string GetExternalStoragePath();
        bool IsExternalStorageAvailable();
        Task<List<FileItem>> GetCommonDirectoriesAsync();
    }
}