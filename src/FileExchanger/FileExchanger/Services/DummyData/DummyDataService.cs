using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileExchanger.Services.DummyData
{
    public class DummyDataService : IDummyDataService
    {

        public bool SendDummyDataOnly { get; private set; }
        public bool ReceiveDummyDataOnly { get; private set; }
        public string KeyDummyData { get; } = "KeyDummyDataDefaultValue12345678";
        private static string _lorem = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam porta quam augue, eget efficitur leo imperdiet commodo. Vestibulum rhoncus velit at libero dapibus mattis. Pellentesque pretium massa non aliquam porta. Donec et iaculis tellus, vitae dapibus ante. Maecenas quis elit imperdiet nibh facilisis dapibus. In hac habitasse platea dictumst. Fusce ac justo lectus. Aliquam molestie vel lorem at elementum. Ut tincidunt ac est in placerat. Duis nulla urna, tincidunt et tincidunt volutpat sed.";

        public DummyDataService()
        {
            SendDummyDataOnly = false;
            ReceiveDummyDataOnly = false;
        }

        public void SetSendDummyDataOnly()
        {
            SendDummyDataOnly = true;
        }

        public void SetReceiveDummyDataOnly()
        {
            ReceiveDummyDataOnly = true;
        }

        public string GetStringDummyData()
        {
            return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(_lorem).Take(500).ToArray());
        }

        public byte[] GetBytesDummyData()
        {
            return Encoding.UTF8.GetBytes(_lorem).Take(500).ToArray();
        }
    }
}
