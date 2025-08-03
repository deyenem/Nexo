using System;

namespace Nexo.Models
{
    public class FileItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string Icon => IsDirectory ? "📁" : GetFileIcon();
        public string SizeFormatted => IsDirectory ? "" : FormatSize(Size);

        private string GetFileIcon()
        {
            var extension = System.IO.Path.GetExtension(Name).ToLower();
            return extension switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "🖼️",
                ".mp4" or ".avi" or ".mkv" or ".mov" => "🎬",
                ".mp3" or ".wav" or ".flac" or ".aac" => "🎵",
                ".pdf" => "📄",
                ".doc" or ".docx" => "📝",
                ".xls" or ".xlsx" => "📊",
                ".zip" or ".rar" or ".7z" => "📦",
                ".apk" => "📱",
                _ => "📄"
            };
        }

        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}