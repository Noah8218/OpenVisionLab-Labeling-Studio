using System;
using System.Collections.Generic;
using System.Threading;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;

namespace MvcVisionSystem
{
    public class ImageDecodeCacheDiagnostics
    {
        public ImageDecodeCacheDiagnostics(int count, long bytes, long hits, long misses, long stores, long evictions, int capacity, long maxBytes)
        {
            Count = count;
            Bytes = bytes;
            Hits = hits;
            Misses = misses;
            Stores = stores;
            Evictions = evictions;
            Capacity = capacity;
            MaxBytes = maxBytes;
        }

        public int Count { get; }

        public long Bytes { get; }

        public long Hits { get; }

        public long Misses { get; }

        public long Stores { get; }

        public long Evictions { get; }

        public int Capacity { get; }

        public long MaxBytes { get; }
    }

    public class CachedDecodedImage : IDisposable
    {
        public CachedDecodedImage(string imagePath, DrawingBitmap bitmap, CvMat mat)
        {
            ImagePath = imagePath ?? string.Empty;
            Bitmap = bitmap;
            Mat = mat;
            EstimatedBytes = EstimateBytes(bitmap, mat);
        }

        public string ImagePath { get; }

        public DrawingBitmap Bitmap { get; private set; }

        public CvMat Mat { get; private set; }

        public long EstimatedBytes { get; }

        public DrawingBitmap TakeBitmap()
        {
            DrawingBitmap bitmap = Bitmap;
            Bitmap = null;
            return bitmap;
        }

        public CvMat TakeMat()
        {
            CvMat mat = Mat;
            Mat = null;
            return mat;
        }

        public void Dispose()
        {
            Bitmap?.Dispose();
            Mat?.Dispose();
            Bitmap = null;
            Mat = null;
        }

        private static long EstimateBytes(DrawingBitmap bitmap, CvMat mat)
        {
            long bitmapBytes = bitmap == null
                ? 0L
                : (long)bitmap.Width * bitmap.Height * 3L;
            long matBytes = mat == null
                ? 0L
                : Math.Max(0L, mat.Total() * mat.ElemSize());
            return bitmapBytes + matBytes;
        }
    }

    public class ImageDecodeCacheService
    {
        public const int DefaultCapacity = 8;
        public const long DefaultMaxPixels = 4L * 1024L * 1024L;
        public const long DefaultMaxBytes = 64L * 1024L * 1024L;

        private readonly object syncRoot = new object();
        private readonly Dictionary<string, CachedDecodedImage> cache;
        private readonly LinkedList<string> order = new LinkedList<string>();
        private readonly int capacity;
        private readonly long maxBytes;
        private long bytes;
        private long hits;
        private long misses;
        private long stores;
        private long evictions;

        public ImageDecodeCacheService()
            : this(DefaultCapacity, DefaultMaxBytes)
        {
        }

        public ImageDecodeCacheService(int capacity, long maxBytes)
        {
            this.capacity = Math.Max(1, capacity);
            this.maxBytes = Math.Max(1L, maxBytes);
            cache = new Dictionary<string, CachedDecodedImage>(StringComparer.OrdinalIgnoreCase);
        }

        public ImageDecodeCacheDiagnostics GetDiagnostics()
        {
            lock (syncRoot)
            {
                return new ImageDecodeCacheDiagnostics(
                    cache.Count,
                    bytes,
                    Interlocked.Read(ref hits),
                    Interlocked.Read(ref misses),
                    Interlocked.Read(ref stores),
                    Interlocked.Read(ref evictions),
                    capacity,
                    maxBytes);
            }
        }

        public bool TryTake(string imagePath, out CachedDecodedImage cachedImage)
        {
            lock (syncRoot)
            {
                if (!string.IsNullOrWhiteSpace(imagePath) && cache.TryGetValue(imagePath, out cachedImage))
                {
                    cache.Remove(imagePath);
                    order.Remove(cachedImage.ImagePath);
                    bytes = Math.Max(0L, bytes - cachedImage.EstimatedBytes);
                    Interlocked.Increment(ref hits);
                    return true;
                }
            }

            Interlocked.Increment(ref misses);
            cachedImage = null;
            return false;
        }

        public bool IsCached(string imagePath)
        {
            lock (syncRoot)
            {
                return !string.IsNullOrWhiteSpace(imagePath) && cache.ContainsKey(imagePath);
            }
        }

        public void Store(CachedDecodedImage decoded)
        {
            if (decoded == null || string.IsNullOrWhiteSpace(decoded.ImagePath))
            {
                decoded?.Dispose();
                return;
            }

            lock (syncRoot)
            {
                if (cache.ContainsKey(decoded.ImagePath))
                {
                    decoded.Dispose();
                    return;
                }

                cache[decoded.ImagePath] = decoded;
                order.AddLast(decoded.ImagePath);
                bytes += decoded.EstimatedBytes;
                Interlocked.Increment(ref stores);

                // Cache eviction is kept outside the shell so image loading can stay a View orchestration concern.
                while ((cache.Count > capacity || bytes > maxBytes) && order.First != null)
                {
                    string oldestPath = order.First.Value;
                    order.RemoveFirst();
                    if (cache.TryGetValue(oldestPath, out CachedDecodedImage oldest))
                    {
                        cache.Remove(oldestPath);
                        bytes = Math.Max(0L, bytes - oldest.EstimatedBytes);
                        oldest.Dispose();
                        Interlocked.Increment(ref evictions);
                    }
                }
            }
        }

        public void Clear()
        {
            lock (syncRoot)
            {
                foreach (CachedDecodedImage decoded in cache.Values)
                {
                    decoded.Dispose();
                }

                cache.Clear();
                order.Clear();
                bytes = 0L;
            }
        }
    }

    [Obsolete("Use ImageDecodeCacheDiagnostics.", false)]
    public sealed class WpfImageDecodeCacheDiagnostics : ImageDecodeCacheDiagnostics
    {
        public WpfImageDecodeCacheDiagnostics(int count, long bytes, long hits, long misses, long stores, long evictions, int capacity, long maxBytes)
            : base(count, bytes, hits, misses, stores, evictions, capacity, maxBytes)
        {
        }
    }

    [Obsolete("Use CachedDecodedImage.", false)]
    public sealed class WpfCachedDecodedImage : CachedDecodedImage
    {
        public WpfCachedDecodedImage(string imagePath, DrawingBitmap bitmap, CvMat mat)
            : base(imagePath, bitmap, mat)
        {
        }
    }

    [Obsolete("Use ImageDecodeCacheService.", false)]
    public sealed class WpfImageDecodeCacheService : ImageDecodeCacheService
    {
        public WpfImageDecodeCacheService()
        {
        }

        public WpfImageDecodeCacheService(int capacity, long maxBytes)
            : base(capacity, maxBytes)
        {
        }
    }
}
