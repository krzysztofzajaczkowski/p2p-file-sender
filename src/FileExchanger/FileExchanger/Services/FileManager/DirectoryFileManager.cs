using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FileExchanger.Services.FileManager
{
    public class DirectoryFileManager : IFileManager
    {
        private readonly OSPlatform _platform;
        private readonly string _dirSeparator;
        private readonly string _fileDir;

        public DirectoryFileManager()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _platform = OSPlatform.Linux;
                _dirSeparator = "/";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _platform = OSPlatform.Windows;
                _dirSeparator = @"\";
            }

            _fileDir = @$"{Environment.CurrentDirectory}{_dirSeparator}files";
            Directory.CreateDirectory(_fileDir);
        }

        public DirectoryFileManager(string clientName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _platform = OSPlatform.Linux;
                _dirSeparator = "/";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _platform = OSPlatform.Windows;
                _dirSeparator = @"\";
            }

            _fileDir = @$"{Environment.CurrentDirectory}{_dirSeparator}{clientName}{_dirSeparator}files";
            Directory.CreateDirectory(_fileDir);
        }

        public void SaveFile(byte[] data, string fileName)
        {
            File.WriteAllBytes($@"{_fileDir}{_dirSeparator}{fileName}", data);
        }

        public byte[] GetFile(string fileName)
        {
            return File.ReadAllBytes($@"{_fileDir}{_dirSeparator}{fileName}");
        }

        public void DeleteFile(string fileName)
        {
            if (File.Exists($@"{_fileDir}{_dirSeparator}{fileName}"))
            {
                File.Delete($@"{_fileDir}{_dirSeparator}{fileName}");
            }
        }

        public void DeleteAllFiles()
        {
            var files = Directory.GetFiles(_fileDir);
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
    }
}
