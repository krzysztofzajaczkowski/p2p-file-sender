using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileExchanger.Services.FileManager
{
    public interface IFileManager
    {
        void SaveFile(byte[] data, string fileName);
        byte[] GetFile(string fileName);
        void DeleteFile(string fileName);
        void DeleteAllFiles();
    }
}
