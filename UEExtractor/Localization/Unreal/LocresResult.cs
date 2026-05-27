using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solicen.Localization.UE4
{
    public static class LocresResultUtil
    {
        public static void ReplaceAll(this LocresResult[] locres, Dictionary<string, string> keys)
        {
            foreach (var key in keys)
            {
                var u = locres.Where(x => x.Source == key.Key).ToList();
                if (u.Count > 0)
                {
                    for (int q = 0; q < u.Count(); q++)
                    {
                        int index = locres.ToList().IndexOf(u[q]);
                        locres[index].Translation = key.Value;
                    }
                }
            }
        }

        public static ConcurrentDictionary<string, LocresResult> ToConcurrent(this LocresResult[] locres)
        {
            var dict = new ConcurrentDictionary<string, LocresResult>();
            foreach (var value in locres)
            {
                var r = new LocresResult(value.Key, value.Source, value.Translation, value.Namespace)
                    { NsHash = value.NsHash, KeyHash = value.KeyHash };
                dict.TryAdd(value.Key, r);
            }
            return dict;
        }

        public static LocresResult[] FromConcurrent(this ConcurrentDictionary<string, LocresResult> concurrent)
        {
            return concurrent.Select(x => x.Value).ToArray();
        }

        public static IEnumerable<LocresResult> GetUnique(this IEnumerable<LocresResult> locres)
        {
            var result = new List<LocresResult>();
            foreach (var value in locres)
            {
                var v = result.FirstOrDefault(x => x.Source == value.Source);
                if (v == null) result.Add(value);
            }
            return result.ToArray();
        }
    }

    public class LocresResult
    {
        public string Url { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;

        public string Namespace { get; set; } = string.Empty;
        public string Key { get; set; }
        public string Source { get; set; }
        public string Translation { get; set; } = string.Empty;

        // Preserved from original game locres (v3 CityHash64 format).
        // 0 = not loaded; writer will fall back to computed hash.
        public uint NsHash  { get; set; } = 0;
        public uint KeyHash { get; set; } = 0;

        public LocresResult() { }
        public LocresResult(string Key, string Source, string Translation = null, string Namespace = "")
        {
            this.Key = Key;
            this.Source = Source;
            this.Translation = Translation;
            this.Namespace = Namespace;
        }
    }
}
