using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using FileExchanger.Models;

namespace FileExchanger.Helpers
{
    public static class DataPackager
    {
        public static List<Package> DivideIntoPackages(byte[] data, int packageSize)
        {
            var packages = new List<Package>();
            var i = 0;
            while (data.Length > packages.Count * packageSize)
            {
                packages.Add(new Package
                {
                    Id = Guid.NewGuid(),
                    Number = ++i,
                    Data = data.Skip(packages.Count * packageSize).Take(packageSize).ToList()
                });
            }

            return packages;
        }

        public static List<Package> SortPackagesByIdList(List<Guid> ids, List<Package> packages)
        {
            var packagesDict = packages.ToImmutableDictionary(k => k.Id);

            var sortedPackages = ids.Select(i => packagesDict[i]).ToList();

            return sortedPackages;
        }

        public static byte[] UnpackData(List<Package> packages)
        {
            return packages.Aggregate(new List<byte>(), (combined, package) =>
            {
                combined.AddRange(package.Data);
                return combined;
            }).ToArray();
        }
    }
}
