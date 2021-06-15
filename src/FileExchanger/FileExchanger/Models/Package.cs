using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileExchanger.Models
{
    public class Package
    {
        public Guid Id { get; set; }
        public int Number { get; set; }
        public List<byte> Data { get; set; }
        public List<byte> Iv { get; set; }
    }
}
