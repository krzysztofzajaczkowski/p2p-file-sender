using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileExchanger.Services.DummyData
{
    public interface IDummyDataService
    {
        bool SendDummyDataOnly { get; }
        bool ReceiveDummyDataOnly { get; }
        string KeyDummyData { get; }
        void SetSendDummyDataOnly();
        void SetReceiveDummyDataOnly();
        string GetStringDummyData();
        byte[] GetBytesDummyData();
    }
}
