using System;
using System.Threading;

namespace WebCache.Models
{
    public class WebCacheEntry
    {
        public DateTimeOffset? CachedTime { get; set; }
        public string Message { get; set; }
        public CancellationTokenSource CTS { get; set; }
    }
}
