using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileExchanger.Services.FileManager
{
    public class InMemoryFileManager : IFileManager
    {
        private readonly Dictionary<string, byte[]> _files;

        public InMemoryFileManager()
        {
            _files = new Dictionary<string, byte[]>();
        }

        public void SaveFile(byte[] data, string fileName)
        {
            if (_files.ContainsKey(fileName))
            {
                _files[fileName] = data;
                return;
            }

            _files.Add(fileName, data);
        }

        public byte[] GetFile(string fileName)
        {
            if (!_files.TryGetValue(fileName, out var fileData))
            {
                return null;
            }

            return fileData;
        }

        public void DeleteFile(string fileName)
        {
            if (_files.ContainsKey(fileName))
            {
                _files.Remove(fileName);
            }
        }

        public void DeleteAllFiles()
        {
            _files.Clear();
        }
    }
}
