using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OpenVisionLab.ImageCanvas.Overlays
{
	internal sealed class CanvasOverlaySpatialIndex
	{
		private const float CellSize = 64.0f;
		private const float LargeCellSize = CellSize * 64.0f;
		private const int MaxCellsPerItem = 4096;

		private readonly Dictionary<long, List<CanvasOverlayItem>> _cells = new Dictionary<long, List<CanvasOverlayItem>>();
		private readonly Dictionary<long, List<CanvasOverlayItem>> _largeCells = new Dictionary<long, List<CanvasOverlayItem>>();
		private readonly Dictionary<long, CanvasOverlayItem> _minAreaCellItems = new Dictionary<long, CanvasOverlayItem>();
		private readonly Dictionary<long, CanvasOverlayItem> _minAreaLargeCellItems = new Dictionary<long, CanvasOverlayItem>();
		private readonly Dictionary<CanvasOverlayItem, SpatialEntry> _entries = new Dictionary<CanvasOverlayItem, SpatialEntry>();
		private readonly HashSet<CanvasOverlayItem> _globalItems = new HashSet<CanvasOverlayItem>();

		public int Count => _entries.Count;

		public void Clear()
		{
			_cells.Clear();
			_largeCells.Clear();
			_minAreaCellItems.Clear();
			_minAreaLargeCellItems.Clear();
			_entries.Clear();
			_globalItems.Clear();
		}

		public void Add(CanvasOverlayItem item)
		{
			Remove(item);
			if (!TryCreateEntry(item, out SpatialEntry entry))
			{
				return;
			}

			_entries[item] = entry;
			if (entry.Tier == SpatialEntryTier.Global)
			{
				_globalItems.Add(item);
				return;
			}

			AddToBuckets(
				entry.Tier == SpatialEntryTier.Large ? _largeCells : _cells,
				entry.Tier == SpatialEntryTier.Large ? _minAreaLargeCellItems : _minAreaCellItems,
				entry.CellKeys,
				item);
		}

		public void Update(CanvasOverlayItem item)
		{
			Add(item);
		}

		public void Remove(CanvasOverlayItem item)
		{
			if (item == null || !_entries.TryGetValue(item, out SpatialEntry entry))
			{
				return;
			}

			if (entry.Tier == SpatialEntryTier.Global)
			{
				_globalItems.Remove(item);
			}
			else
			{
				RemoveFromBuckets(
					entry.Tier == SpatialEntryTier.Large ? _largeCells : _cells,
					entry.Tier == SpatialEntryTier.Large ? _minAreaLargeCellItems : _minAreaCellItems,
					entry.CellKeys,
					item);
			}

			_entries.Remove(item);
		}

		public List<CanvasOverlayItem> QueryPoint(PointF point, float radius, bool includeGroupRectangles)
		{
			float safeRadius = Math.Max(1.0f, radius);
			RectangleF queryBounds = RectangleF.FromLTRB(
				point.X - safeRadius,
				point.Y - safeRadius,
				point.X + safeRadius,
				point.Y + safeRadius);
			return QueryBounds(queryBounds, includeGroupRectangles);
		}

		public void VisitPoint(PointF point, float radius, bool includeGroupRectangles, Action<CanvasOverlayItem> visitor)
		{
			if (visitor == null)
			{
				return;
			}

			float safeRadius = Math.Max(1.0f, radius);
			RectangleF queryBounds = RectangleF.FromLTRB(
				point.X - safeRadius,
				point.Y - safeRadius,
				point.X + safeRadius,
				point.Y + safeRadius);

			// Hit-testing only needs the best ROI, not a materialized candidate list.
			// Visiting buckets directly avoids large List/HashSet allocations when many
			// labels overlap the same click point.
			VisitBuckets(_cells, queryBounds, CellSize, includeGroupRectangles, visitor);
			VisitBuckets(_largeCells, queryBounds, LargeCellSize, includeGroupRectangles, visitor);

			foreach (CanvasOverlayItem item in _globalItems)
			{
				VisitIfCandidate(item, queryBounds, includeGroupRectangles, visitor);
			}
		}

		public CanvasOverlayItem FindBestRectAtPoint(PointF point, float radius, bool includeGroupRectangles, bool groupOnly, float zoomScale, float handleSize)
		{
			float safeRadius = Math.Max(1.0f, radius);
			RectangleF queryBounds = RectangleF.FromLTRB(
				point.X - safeRadius,
				point.Y - safeRadius,
				point.X + safeRadius,
				point.Y + safeRadius);

			var search = new BestRectSearch(point, zoomScale, handleSize, groupOnly);
			FindBestRectInBuckets(_cells, _minAreaCellItems, queryBounds, CellSize, includeGroupRectangles, ref search);
			FindBestRectInBuckets(_largeCells, _minAreaLargeCellItems, queryBounds, LargeCellSize, includeGroupRectangles, ref search);

			foreach (CanvasOverlayItem item in _globalItems)
			{
				TryUpdateBestRect(item, queryBounds, includeGroupRectangles, ref search);
			}

			return search.BestItem;
		}

		public List<CanvasOverlayItem> QueryBounds(RectangleF bounds, bool includeGroupRectangles)
		{
			var result = new List<CanvasOverlayItem>();
			var seen = new HashSet<CanvasOverlayItem>();

			QueryBuckets(_cells, bounds, CellSize, includeGroupRectangles, seen, result);
			QueryBuckets(_largeCells, bounds, LargeCellSize, includeGroupRectangles, seen, result);

			foreach (CanvasOverlayItem item in _globalItems)
			{
				AddIfCandidate(item, bounds, includeGroupRectangles, seen, result);
			}

			return result;
		}

		public int VisitBounds(RectangleF bounds, bool includeGroupRectangles, int maxCandidates, Action<CanvasOverlayItem> visitor)
		{
			if (visitor == null)
			{
				return 0;
			}

			int remaining = maxCandidates <= 0 ? int.MaxValue : maxCandidates;
			int visitedCount = 0;
			var seen = new HashSet<CanvasOverlayItem>();
			if (VisitBuckets(_cells, bounds, CellSize, includeGroupRectangles, seen, visitor, ref visitedCount, ref remaining))
			{
				return visitedCount;
			}

			if (VisitBuckets(_largeCells, bounds, LargeCellSize, includeGroupRectangles, seen, visitor, ref visitedCount, ref remaining))
			{
				return visitedCount;
			}

			foreach (CanvasOverlayItem item in _globalItems)
			{
				if (VisitCandidate(item, bounds, includeGroupRectangles, seen, visitor, ref visitedCount, ref remaining))
				{
					break;
				}
			}

			return visitedCount;
		}

		private void AddIfCandidate(CanvasOverlayItem item, RectangleF bounds, bool includeGroupRectangles, HashSet<CanvasOverlayItem> seen, List<CanvasOverlayItem> result)
		{
			if (item == null || !seen.Add(item))
			{
				return;
			}

			if (!IsInteractiveCandidate(item, includeGroupRectangles))
			{
				return;
			}

			if (!_entries.TryGetValue(item, out SpatialEntry entry) || !Intersects(entry.Bounds, bounds))
			{
				return;
			}

			result.Add(item);
		}

		private static bool TryCreateEntry(CanvasOverlayItem item, out SpatialEntry entry)
		{
			entry = default;
			if (item?.Shape is not CanvasRect<float> rect || rect.IsEmpty())
			{
				return false;
			}

			RectangleF bounds = RectangleF.FromLTRB(rect.Left, rect.Bottom, rect.Right, rect.Top);
			NormalizeCellRange(
				ToCell(bounds.Left, CellSize),
				ToCell(bounds.Right, CellSize),
				ToCell(bounds.Top, CellSize),
				ToCell(bounds.Bottom, CellSize),
				out int left,
				out int right,
				out int bottom,
				out int top);
			int cellCount = GetCellCount(left, right, bottom, top);
			if (cellCount <= MaxCellsPerItem)
			{
				entry = new SpatialEntry(bounds, BuildCellKeys(left, right, bottom, top, cellCount), SpatialEntryTier.Fine);
				return true;
			}

			// Very large ROI must not fall back to a full scan. Coarse buckets keep
			// point and viewport queries bounded while avoiding millions of fine cells.
			NormalizeCellRange(
				ToCell(bounds.Left, LargeCellSize),
				ToCell(bounds.Right, LargeCellSize),
				ToCell(bounds.Top, LargeCellSize),
				ToCell(bounds.Bottom, LargeCellSize),
				out int largeLeft,
				out int largeRight,
				out int largeBottom,
				out int largeTop);
			int largeCellCount = GetCellCount(largeLeft, largeRight, largeBottom, largeTop);
			if (largeCellCount <= MaxCellsPerItem)
			{
				entry = new SpatialEntry(bounds, BuildCellKeys(largeLeft, largeRight, largeBottom, largeTop, largeCellCount), SpatialEntryTier.Large);
				return true;
			}

			entry = new SpatialEntry(bounds, null, SpatialEntryTier.Global);
			return true;
		}

		private static void AddToBuckets(Dictionary<long, List<CanvasOverlayItem>> buckets, Dictionary<long, CanvasOverlayItem> minAreaItems, List<long> keys, CanvasOverlayItem item)
		{
			foreach (long key in keys)
			{
				if (!buckets.TryGetValue(key, out List<CanvasOverlayItem> bucket))
				{
					bucket = new List<CanvasOverlayItem>();
					buckets[key] = bucket;
				}

				bucket.Add(item);
				if (!minAreaItems.TryGetValue(key, out CanvasOverlayItem currentMin) || GetArea(item) < GetArea(currentMin))
				{
					minAreaItems[key] = item;
				}
			}
		}

		private static void RemoveFromBuckets(Dictionary<long, List<CanvasOverlayItem>> buckets, Dictionary<long, CanvasOverlayItem> minAreaItems, List<long> keys, CanvasOverlayItem item)
		{
			foreach (long key in keys)
			{
				if (!buckets.TryGetValue(key, out List<CanvasOverlayItem> bucket))
				{
					continue;
				}

				bucket.Remove(item);
				if (bucket.Count == 0)
				{
					buckets.Remove(key);
					minAreaItems.Remove(key);
				}
				else if (minAreaItems.TryGetValue(key, out CanvasOverlayItem currentMin) && ReferenceEquals(currentMin, item))
				{
					minAreaItems[key] = FindSmallestAreaItem(bucket);
				}
			}
		}

		private void FindBestRectInBuckets(
			Dictionary<long, List<CanvasOverlayItem>> buckets,
			Dictionary<long, CanvasOverlayItem> minAreaItems,
			RectangleF bounds,
			float cellSize,
			bool includeGroupRectangles,
			ref BestRectSearch search)
		{
			NormalizeCellRange(
				ToCell(bounds.Left, cellSize),
				ToCell(bounds.Right, cellSize),
				ToCell(bounds.Top, cellSize),
				ToCell(bounds.Bottom, cellSize),
				out int left,
				out int right,
				out int bottom,
				out int top);

			for (int cellX = left; cellX <= right; cellX++)
			{
				for (int cellY = bottom; cellY <= top; cellY++)
				{
					long key = MakeKey(cellX, cellY);
					if (!buckets.TryGetValue(key, out List<CanvasOverlayItem> bucket))
					{
						continue;
					}

					// Dense overlap is common in labeling review. Try the smallest ROI in
					// the bucket first so a click inside it can skip geometry checks for
					// thousands of larger containing boxes.
					if (minAreaItems.TryGetValue(key, out CanvasOverlayItem minAreaItem))
					{
						TryUpdateBestRect(minAreaItem, bounds, includeGroupRectangles, ref search);
					}

					for (int i = 0; i < bucket.Count; i++)
					{
						CanvasOverlayItem item = bucket[i];
						double area = GetArea(item);
						if (search.BestItem != null && area - search.BestArea > 0.001)
						{
							continue;
						}

						TryUpdateBestRect(item, bounds, includeGroupRectangles, ref search);
					}
				}
			}
		}

		private static CanvasOverlayItem FindSmallestAreaItem(List<CanvasOverlayItem> bucket)
		{
			CanvasOverlayItem bestItem = null;
			double bestArea = double.MaxValue;
			for (int i = 0; i < bucket.Count; i++)
			{
				CanvasOverlayItem item = bucket[i];
				double area = GetArea(item);
				if (area < bestArea)
				{
					bestArea = area;
					bestItem = item;
				}
			}

			return bestItem;
		}

		private void TryUpdateBestRect(CanvasOverlayItem item, RectangleF bounds, bool includeGroupRectangles, ref BestRectSearch search)
		{
			if (item == null || !IsInteractiveCandidate(item, includeGroupRectangles))
			{
				return;
			}

			if (search.GroupOnly)
			{
				if (!item.IsGroupRectangle)
				{
					return;
				}
			}
			else if (item.IsGroupRectangle || item.ItemType != EnumItemType.Window)
			{
				return;
			}

			if (!_entries.TryGetValue(item, out SpatialEntry entry) || !Intersects(entry.Bounds, bounds))
			{
				return;
			}

			if (item.Shape is not CanvasRect<float> rect)
			{
				return;
			}

			LineOverType hitType = rect.CheckHandleContainsPosition(search.Point.X, search.Point.Y, search.ZoomScale, search.HandleSize);
			if (hitType == LineOverType.None)
			{
				return;
			}

			int hitPriority = hitType == LineOverType.Move2D ? 1 : 0;
			double area = GetArea(rect);
			double distance = search.GroupOnly
				? CalculateDistanceToRectangle(search.Point, new RectangleF(rect.Left, rect.Bottom, rect.Width, rect.Height))
				: CalculateSquaredDistance(search.Point, rect.Center);
			if (IsBetterHit(hitPriority, area, distance, search.BestHitPriority, search.BestArea, search.BestDistance))
			{
				search.BestHitPriority = hitPriority;
				search.BestArea = area;
				search.BestDistance = distance;
				search.BestItem = item;
			}
		}

		private static double GetArea(CanvasOverlayItem item)
		{
			return item?.Shape is CanvasRect<float> rect ? GetArea(rect) : double.MaxValue;
		}

		private static double GetArea(CanvasRect<float> rect)
		{
			return Math.Max(1.0, Math.Abs(rect.Width * rect.Height));
		}

		private static bool IsBetterHit(int hitPriority, double area, double distance, int bestHitPriority, double bestArea, double bestDistance)
		{
			if (Math.Abs(area - bestArea) > 0.001)
			{
				return area < bestArea;
			}

			if (Math.Abs(distance - bestDistance) > 0.001)
			{
				return distance < bestDistance;
			}

			return hitPriority < bestHitPriority;
		}

		private static double CalculateSquaredDistance(PointF point, CanvasPoint<float> center)
		{
			double dx = center.X - point.X;
			double dy = center.Y - point.Y;
			return (dx * dx) + (dy * dy);
		}

		private static double CalculateDistanceToRectangle(PointF point, RectangleF rect)
		{
			float dx = Math.Max(Math.Max(rect.Left - point.X, 0), point.X - rect.Right);
			float dy = Math.Max(Math.Max(rect.Top - point.Y, 0), point.Y - rect.Bottom);
			return Math.Sqrt(dx * dx + dy * dy);
		}

		private void QueryBuckets(
			Dictionary<long, List<CanvasOverlayItem>> buckets,
			RectangleF bounds,
			float cellSize,
			bool includeGroupRectangles,
			HashSet<CanvasOverlayItem> seen,
			List<CanvasOverlayItem> result)
		{
			NormalizeCellRange(
				ToCell(bounds.Left, cellSize),
				ToCell(bounds.Right, cellSize),
				ToCell(bounds.Top, cellSize),
				ToCell(bounds.Bottom, cellSize),
				out int left,
				out int right,
				out int bottom,
				out int top);

			for (int cellX = left; cellX <= right; cellX++)
			{
				for (int cellY = bottom; cellY <= top; cellY++)
				{
					long key = MakeKey(cellX, cellY);
					if (!buckets.TryGetValue(key, out List<CanvasOverlayItem> bucket))
					{
						continue;
					}

					foreach (CanvasOverlayItem item in bucket)
					{
						AddIfCandidate(item, bounds, includeGroupRectangles, seen, result);
					}
				}
			}
		}

		private void VisitBuckets(
			Dictionary<long, List<CanvasOverlayItem>> buckets,
			RectangleF bounds,
			float cellSize,
			bool includeGroupRectangles,
			Action<CanvasOverlayItem> visitor)
		{
			NormalizeCellRange(
				ToCell(bounds.Left, cellSize),
				ToCell(bounds.Right, cellSize),
				ToCell(bounds.Top, cellSize),
				ToCell(bounds.Bottom, cellSize),
				out int left,
				out int right,
				out int bottom,
				out int top);

			for (int cellX = left; cellX <= right; cellX++)
			{
				for (int cellY = bottom; cellY <= top; cellY++)
				{
					long key = MakeKey(cellX, cellY);
					if (!buckets.TryGetValue(key, out List<CanvasOverlayItem> bucket))
					{
						continue;
					}

					foreach (CanvasOverlayItem item in bucket)
					{
						VisitIfCandidate(item, bounds, includeGroupRectangles, visitor);
					}
				}
			}
		}

		private bool VisitBuckets(
			Dictionary<long, List<CanvasOverlayItem>> buckets,
			RectangleF bounds,
			float cellSize,
			bool includeGroupRectangles,
			HashSet<CanvasOverlayItem> seen,
			Action<CanvasOverlayItem> visitor,
			ref int visitedCount,
			ref int remaining)
		{
			NormalizeCellRange(
				ToCell(bounds.Left, cellSize),
				ToCell(bounds.Right, cellSize),
				ToCell(bounds.Top, cellSize),
				ToCell(bounds.Bottom, cellSize),
				out int left,
				out int right,
				out int bottom,
				out int top);

			for (int cellX = left; cellX <= right; cellX++)
			{
				for (int cellY = bottom; cellY <= top; cellY++)
				{
					long key = MakeKey(cellX, cellY);
					if (!buckets.TryGetValue(key, out List<CanvasOverlayItem> bucket))
					{
						continue;
					}

					foreach (CanvasOverlayItem item in bucket)
					{
						if (VisitCandidate(item, bounds, includeGroupRectangles, seen, visitor, ref visitedCount, ref remaining))
						{
							return true;
						}
					}
				}
			}

			return false;
		}

		private bool VisitCandidate(
			CanvasOverlayItem item,
			RectangleF bounds,
			bool includeGroupRectangles,
			HashSet<CanvasOverlayItem> seen,
			Action<CanvasOverlayItem> visitor,
			ref int visitedCount,
			ref int remaining)
		{
			if (remaining <= 0)
			{
				return true;
			}

			if (item == null || !seen.Add(item))
			{
				return false;
			}

			if (!IsInteractiveCandidate(item, includeGroupRectangles))
			{
				return false;
			}

			if (!_entries.TryGetValue(item, out SpatialEntry entry) || !Intersects(entry.Bounds, bounds))
			{
				return false;
			}

			visitor(item);
			visitedCount++;
			remaining--;
			return remaining <= 0;
		}

		private void VisitIfCandidate(CanvasOverlayItem item, RectangleF bounds, bool includeGroupRectangles, Action<CanvasOverlayItem> visitor)
		{
			if (item == null || !IsInteractiveCandidate(item, includeGroupRectangles))
			{
				return;
			}

			if (!_entries.TryGetValue(item, out SpatialEntry entry) || !Intersects(entry.Bounds, bounds))
			{
				return;
			}

			visitor(item);
		}

		private static List<long> BuildCellKeys(int left, int right, int bottom, int top, int cellCount)
		{
			var keys = new List<long>(cellCount);
			for (int cellX = left; cellX <= right; cellX++)
			{
				for (int cellY = bottom; cellY <= top; cellY++)
				{
					keys.Add(MakeKey(cellX, cellY));
				}
			}

			return keys;
		}

		private static void NormalizeCellRange(int left, int right, int bottom, int top, out int normalizedLeft, out int normalizedRight, out int normalizedBottom, out int normalizedTop)
		{
			normalizedLeft = Math.Min(left, right);
			normalizedRight = Math.Max(left, right);
			normalizedBottom = Math.Min(bottom, top);
			normalizedTop = Math.Max(bottom, top);
		}

		private static int GetCellCount(int left, int right, int bottom, int top)
		{
			long width = Math.Max(1L, (long)right - left + 1L);
			long height = Math.Max(1L, (long)top - bottom + 1L);
			long count = width * height;
			return count > int.MaxValue ? int.MaxValue : (int)count;
		}

		private static bool IsInteractiveCandidate(CanvasOverlayItem item, bool includeGroupRectangles)
		{
			if (!item.IsVisible || item.IsControlLock)
			{
				return false;
			}

			if (item.IsGroupRectangle)
			{
				return includeGroupRectangles;
			}

			return item.ItemType == EnumItemType.Window;
		}

		private static bool Intersects(RectangleF a, RectangleF b)
		{
			return a.Left <= b.Right
				&& a.Right >= b.Left
				&& a.Top <= b.Bottom
				&& a.Bottom >= b.Top;
		}

		private static int ToCell(float value, float cellSize)
		{
			return (int)Math.Floor(value / cellSize);
		}

		private static long MakeKey(int x, int y)
		{
			return ((long)x << 32) ^ (uint)y;
		}

		private readonly struct SpatialEntry
		{
			public SpatialEntry(RectangleF bounds, List<long> cellKeys, SpatialEntryTier tier)
			{
				Bounds = bounds;
				CellKeys = cellKeys;
				Tier = tier;
			}

			public RectangleF Bounds { get; }
			public List<long> CellKeys { get; }
			public SpatialEntryTier Tier { get; }
		}

		private struct BestRectSearch
		{
			public BestRectSearch(PointF point, float zoomScale, float handleSize, bool groupOnly)
			{
				Point = point;
				ZoomScale = zoomScale;
				HandleSize = handleSize;
				GroupOnly = groupOnly;
				BestItem = null;
				BestHitPriority = int.MaxValue;
				BestArea = double.MaxValue;
				BestDistance = double.MaxValue;
			}

			public PointF Point { get; }
			public float ZoomScale { get; }
			public float HandleSize { get; }
			public bool GroupOnly { get; }
			public CanvasOverlayItem BestItem { get; set; }
			public int BestHitPriority { get; set; }
			public double BestArea { get; set; }
			public double BestDistance { get; set; }
		}

		private enum SpatialEntryTier
		{
			Fine,
			Large,
			Global
		}
	}
}
