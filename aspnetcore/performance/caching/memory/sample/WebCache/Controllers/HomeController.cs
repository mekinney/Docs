using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using WebCache.Models;

#region snippet_ctor
public class HomeController : Controller
{
    private IMemoryCache _cache;

    public HomeController(IMemoryCache memoryCache)
    {
        _cache = memoryCache;
    }
    #endregion

    #region snippet1
    public IActionResult Index()
    {
        WebCacheEntry cacheEntry;

        // Look for cache key.
        if (!_cache.TryGetValue(CacheKey.EntryKey, out cacheEntry))
        {
            // Key not in cache, so get data.
            cacheEntry = new WebCacheEntry
            {
                CachedTime = DateTime.Now
            };

            // Set cache options.
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                // Longest possible time to keep in cache.
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(6))
                // Keep in cache for this time, reset time if accessed.
                .SetSlidingExpiration(TimeSpan.FromSeconds(3))
                // Pin to cache.
                .SetPriority(CacheItemPriority.NeverRemove);

            // Save data in cache.
            _cache.Set(CacheKey.EntryKey, cacheEntry, cacheEntryOptions);
        }

        return View(cacheEntry);
    }
    #endregion

    #region snippet_gct
    public IActionResult IndexGet()
    {
        var cacheEntry = _cache.Get<WebCacheEntry>(CacheKey.EntryKey);
        return View("Index", cacheEntry);
    }
    #endregion

    #region snippet2
    public IActionResult IndexGetOrCreate()
    {
        var cacheEntry = _cache.GetOrCreate(CacheKey.EntryKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromSeconds(3);
            return new WebCacheEntry
            {
                CachedTime = DateTime.Now
            };
        });

        return View("Index", cacheEntry);
    }

    public async Task<IActionResult> IndexGetOrCreateAsync()
    {
        var cacheEntry = await
            _cache.GetOrCreateAsync(CacheKey.EntryKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromSeconds(3);
            return Task.FromResult(new WebCacheEntry
            {
                CachedTime = DateTime.Now
            });
        });

        return View("Index", cacheEntry);
    }
    #endregion

    public IActionResult Remove()
    {
        _cache.Remove(CacheKey.EntryKey);

        return View("Index");
    }

    #region snippet_et
    public IActionResult CreateCallbackEntry()
    {
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            // Longest possible time to keep in cache.
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(3))
            // Add eviction callback
            .RegisterPostEvictionCallback(callback: EvictionCallback, state: this);

        var cacheEntry = new WebCacheEntry
        {
            CachedTime = DateTime.Now
        };

        _cache.Set(CacheKey.CallbackKey, cacheEntry, cacheEntryOptions);

        return View("Index", cacheEntry);
    }

    public IActionResult GetCallbackEntry()
    {
        return View("Index", _cache.Get(CacheKey.CallbackKey));
    }

    // If the entry is evicted, add it back and display a message.
    private static void EvictionCallback(object key, object value,
        EvictionReason reason, object state)
    {
        var evictedEntry = (WebCacheEntry)value;
        evictedEntry.Message = $"Entry was evicted. Reason: {reason}.";

        ((HomeController)state)._cache.Set(
            CacheKey.CallbackKey,
            evictedEntry);
    }
    #endregion


    #region snippet_ed
    public IActionResult EvictDependency()
    {
        // Clear out eviction message, and key from previous run (if any).
        _cache.Remove(CacheKey.EvictMsg2);
        CancellationTokenSource cts2 = new CancellationTokenSource();
        _cache.Set<CancellationTokenSource>(CacheKey.CancelTokenSource2, cts2);

        using (var entry = _cache.CreateEntry(CacheKey.ParentKey))
        {
            // expire this entry if the dependant entry expires.
            entry.Value = DateTime.Now.TimeOfDay.Milliseconds.ToString();
            entry.RegisterPostEvictionCallback(AfterEvicted2, this);

            _cache.Set(CacheKey.ChildKey,
                DateTime.Now.AddMilliseconds(4).TimeOfDay.Milliseconds.ToString(),
                new CancellationChangeToken(cts2.Token));
        }

        return RedirectToAction("CheckEvictDependency");
    }

    public IActionResult CheckEvictDependency(int? id)
    {
        ViewData["CachedMS2"] = _cache.Get<string>(CacheKey.ParentKey);
        ViewData["CachedMS3"] = _cache.Get<string>(CacheKey.ChildKey);
        ViewData["MessageCED"] = _cache.Get<string>(CacheKey.EvictMsg2);

        if (id > 0)
        {
            CancellationTokenSource cts2 =
                _cache.Get<CancellationTokenSource>(CacheKey.CancelTokenSource2);
            cts2.Cancel();
        }

        return View();
    }
    #endregion

    private static void AfterEvicted2(object key, object value,
                                      EvictionReason reason, object state)
    {
        var em = $"key: {key}, Value: {value}, Reason: {reason}";
        ((HomeController)state)._cache.Set<string>(CacheKey.EvictMsg2, em);
    }


    public IActionResult CheckCancel(int? id = 0)
    {
        if (id > 0)
        {
            CancellationTokenSource cts =
               _cache.Get<CancellationTokenSource>(CacheKey.CancelTokenSource);
            cts.CancelAfter(100);
            // Cancel immediately with cts.Cancel();
        }

        ViewData["CachedTime"] = _cache.Get<string>(CacheKey.Ticks);
        ViewData["Message"] =  _cache.Get<string>(CacheKey.CancelMsg); ;

        return View();
    }
    public IActionResult CancelTest()
    {
        var cachedVal = DateTime.Now.Second.ToString();
        CancellationTokenSource cts = new CancellationTokenSource();
        _cache.Set<CancellationTokenSource>(CacheKey.CancelTokenSource, cts);

        // Don't use previous message.
        _cache.Remove(CacheKey.CancelMsg);

        _cache.Set(CacheKey.Ticks, cachedVal,
            new MemoryCacheEntryOptions()
            .AddExpirationToken(new CancellationChangeToken(cts.Token))
            .RegisterPostEvictionCallback(
                (key, value, reason, substate) =>
                {
                    var cm = $"'{key}':'{value}' was evicted because: {reason}";
                    _cache.Set<string>(CacheKey.CancelMsg, cm);
                }
            ));

        return RedirectToAction("CheckCancel");
    }
}

