using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace MvcVisionSystem
{
    public class CrashRecoveryJournalService
    {
        public const int CurrentSchemaVersion = 1;
        public const string FormatName = "openvisionlab-labeling-crash-recovery";
        public const long MaximumJournalBytes = 256L * 1024L * 1024L;
        public const int MaximumObjectCount = 4096;
        public const int MaximumPolygonPointCount = 1_000_000;
        public static readonly TimeSpan MaximumDraftAge = TimeSpan.FromDays(7);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly object syncRoot = new object();
        private long discardedThroughRevision;

        public CrashRecoveryJournalService(string applicationDataRoot = null)
        {
            string root = string.IsNullOrWhiteSpace(applicationDataRoot)
                ? RuntimeDiagnosticsPaths.Resolve().ApplicationDataRoot
                : Path.GetFullPath(applicationDataRoot);
            RecoveryDirectory = Path.Combine(root, "Recovery");
            InvalidDirectory = Path.Combine(RecoveryDirectory, "Invalid");
            JournalPath = Path.Combine(RecoveryDirectory, "current-image-draft.json");
            TemporaryPath = JournalPath + ".tmp";
        }

        public string RecoveryDirectory { get; }

        public string InvalidDirectory { get; }

        public string JournalPath { get; }

        public string TemporaryPath { get; }

        public bool Write(WpfCrashRecoveryDraft draft, long revision = 1)
        {
            ValidateDraft(draft, DateTime.UtcNow, validateAge: false);
            ValidateEstimatedPayloadSize(draft);
            string draftJson = JsonSerializer.Serialize(draft, JsonOptions);
            var envelope = new WpfCrashRecoveryEnvelope
            {
                SchemaVersion = CurrentSchemaVersion,
                Format = FormatName,
                PayloadSha256 = HashingService.ComputeUtf8TextSha256(draftJson, lowerCase: true),
                Draft = draft
            };
            string envelopeJson = JsonSerializer.Serialize(envelope, JsonOptions);
            long byteCount = Encoding.UTF8.GetByteCount(envelopeJson);
            if (byteCount > MaximumJournalBytes)
            {
                throw new InvalidDataException(
                    $"복구 초안이 허용 크기 {MaximumJournalBytes / (1024 * 1024)}MB를 초과했습니다.");
            }

            lock (syncRoot)
            {
                if (revision <= discardedThroughRevision)
                {
                    return false;
                }

                Directory.CreateDirectory(RecoveryDirectory);
                File.WriteAllText(TemporaryPath, envelopeJson, new UTF8Encoding(false));
                File.Move(TemporaryPath, JournalPath, overwrite: true);
                return true;
            }
        }

        public WpfCrashRecoveryReadResult ReadAvailable(
            string expectedRecipeName,
            string expectedDatasetRoot,
            DateTime? utcNow = null)
        {
            lock (syncRoot)
            {
                TryDeleteFile(TemporaryPath);
                if (!File.Exists(JournalPath))
                {
                    return WpfCrashRecoveryReadResult.None;
                }

                try
                {
                    var fileInfo = new FileInfo(JournalPath);
                    if (fileInfo.Length <= 0 || fileInfo.Length > MaximumJournalBytes)
                    {
                        throw new InvalidDataException("복구 저널 파일 크기가 허용 범위를 벗어났습니다.");
                    }

                    string json = File.ReadAllText(JournalPath, Encoding.UTF8);
                    WpfCrashRecoveryEnvelope envelope =
                        JsonSerializer.Deserialize<WpfCrashRecoveryEnvelope>(json, JsonOptions)
                        ?? throw new InvalidDataException("복구 저널을 읽을 수 없습니다.");
                    if (envelope.SchemaVersion != CurrentSchemaVersion
                        || !string.Equals(envelope.Format, FormatName, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("지원하지 않는 복구 저널 형식입니다.");
                    }

                    string draftJson = JsonSerializer.Serialize(envelope.Draft, JsonOptions);
                    string actualSha256 = HashingService.ComputeUtf8TextSha256(draftJson, lowerCase: true);
                    if (!IsSha256(envelope.PayloadSha256)
                        || !string.Equals(envelope.PayloadSha256, actualSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("복구 저널 무결성 검증에 실패했습니다.");
                    }

                    DateTime now = utcNow ?? DateTime.UtcNow;
                    ValidateDraft(envelope.Draft, now, validateAge: true);
                    ValidateContext(envelope.Draft, expectedRecipeName, expectedDatasetRoot);
                    return WpfCrashRecoveryReadResult.Available(envelope.Draft);
                }
                catch (Exception ex) when (
                    ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is JsonException
                    || ex is InvalidDataException
                    || ex is ArgumentException
                    || ex is NotSupportedException)
                {
                    string quarantinePath = QuarantineUnsafe();
                    return WpfCrashRecoveryReadResult.Invalid(ex.Message, quarantinePath);
                }
            }
        }

        public void Discard(long revision = long.MaxValue)
        {
            lock (syncRoot)
            {
                discardedThroughRevision = Math.Max(discardedThroughRevision, revision);
                TryDeleteFile(TemporaryPath);
                TryDeleteFile(JournalPath);
            }
        }

        private static void ValidateContext(
            WpfCrashRecoveryDraft draft,
            string expectedRecipeName,
            string expectedDatasetRoot)
        {
            string normalizedRecipe = NormalizeRequired(expectedRecipeName, "현재 Recipe");
            if (!string.Equals(draft.RecipeName, normalizedRecipe, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"복구 초안 Recipe '{draft.RecipeName}'가 현재 Recipe '{normalizedRecipe}'와 다릅니다.");
            }

            string normalizedExpectedRoot = NormalizePath(expectedDatasetRoot, "현재 데이터셋");
            if (!string.Equals(draft.DatasetRootPath, normalizedExpectedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("복구 초안 데이터셋이 현재 Recipe 데이터셋과 다릅니다.");
            }
        }

        private static void ValidateDraft(
            WpfCrashRecoveryDraft draft,
            DateTime utcNow,
            bool validateAge)
        {
            if (draft == null)
            {
                throw new InvalidDataException("복구 초안이 비어 있습니다.");
            }

            draft.RecipeName = NormalizeRequired(draft.RecipeName, "Recipe");
            draft.DatasetRootPath = NormalizePath(draft.DatasetRootPath, "데이터셋");
            draft.ImagePath = NormalizePath(draft.ImagePath, "이미지");
            draft.DirtyReason = string.IsNullOrWhiteSpace(draft.DirtyReason)
                ? "저장되지 않은 편집"
                : draft.DirtyReason.Trim();
            if (draft.CreatedUtc == default || draft.CreatedUtc.Kind == DateTimeKind.Unspecified)
            {
                throw new InvalidDataException("복구 초안 생성 시간이 올바르지 않습니다.");
            }

            DateTime createdUtc = draft.CreatedUtc.ToUniversalTime();
            if (createdUtc > utcNow.AddMinutes(5))
            {
                throw new InvalidDataException("복구 초안 생성 시간이 현재 시간보다 늦습니다.");
            }
            if (validateAge && utcNow - createdUtc > MaximumDraftAge)
            {
                throw new InvalidDataException(
                    $"복구 초안 보관 기간 {MaximumDraftAge.TotalDays:0}일을 초과했습니다.");
            }

            if (!File.Exists(draft.ImagePath))
            {
                throw new InvalidDataException("복구할 원본 이미지가 존재하지 않습니다.");
            }

            var imageInfo = new FileInfo(draft.ImagePath);
            if (draft.ImageLength < 0 || imageInfo.Length != draft.ImageLength)
            {
                throw new InvalidDataException("복구할 원본 이미지 크기가 변경되었습니다.");
            }
            if (draft.ImageLastWriteUtcTicks <= 0
                || imageInfo.LastWriteTimeUtc.Ticks != draft.ImageLastWriteUtcTicks)
            {
                throw new InvalidDataException("복구할 원본 이미지 수정 시간이 변경되었습니다.");
            }
            if (draft.ImageWidth <= 0 || draft.ImageHeight <= 0)
            {
                throw new InvalidDataException("복구 초안 이미지 크기가 올바르지 않습니다.");
            }

            draft.Boxes ??= new List<WpfCrashRecoveryBox>();
            draft.Segments ??= new List<WpfCrashRecoverySegment>();
            if (draft.Boxes.Count + draft.Segments.Count > MaximumObjectCount)
            {
                throw new InvalidDataException($"복구 객체 수가 허용 개수 {MaximumObjectCount}개를 초과했습니다.");
            }

            foreach (WpfCrashRecoveryBox box in draft.Boxes)
            {
                ValidateBox(box, draft.ImageWidth, draft.ImageHeight);
            }

            int totalPointCount = 0;
            foreach (WpfCrashRecoverySegment segment in draft.Segments)
            {
                totalPointCount += ValidateSegment(segment, draft.ImageWidth, draft.ImageHeight);
                if (totalPointCount > MaximumPolygonPointCount)
                {
                    throw new InvalidDataException(
                        $"복구 폴리곤 점 수가 허용 개수 {MaximumPolygonPointCount}개를 초과했습니다.");
                }
            }
        }

        private static void ValidateEstimatedPayloadSize(WpfCrashRecoveryDraft draft)
        {
            long estimatedBytes = 0;
            estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, draft.ApplicationVersion);
            estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, draft.RecipeName);
            estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, draft.DatasetRootPath);
            estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, draft.ImagePath);
            estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, draft.DirtyReason);

            foreach (WpfCrashRecoveryBox box in draft.Boxes)
            {
                estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, box.ClassName);
                estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, box.ShapeKind);
                estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, box.Metadata.GroupId);
                foreach (string tag in box.Metadata.Tags)
                {
                    estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, tag);
                }
            }

            foreach (WpfCrashRecoverySegment segment in draft.Segments)
            {
                estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, segment.ClassName);
                estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, segment.ObjectId);
                estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, segment.LastStructuralOperation);
                estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, segment.Metadata.GroupId);
                estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, segment.MaskData?.LongLength ?? 0L);
                estimatedBytes = AddEstimatedPayloadBytes(
                    estimatedBytes,
                    (long)segment.Points.Count * 8L);
                foreach (List<WpfCrashRecoveryPoint> cutout in segment.CutoutPolygons)
                {
                    estimatedBytes = AddEstimatedPayloadBytes(
                        estimatedBytes,
                        (long)cutout.Count * 8L);
                }
                foreach (string tag in segment.Metadata.Tags)
                {
                    estimatedBytes = AddEstimatedPayloadBytes(estimatedBytes, tag);
                }
            }

            if (estimatedBytes > MaximumJournalBytes)
            {
                throw new InvalidDataException(
                    $"복구 초안 원시 데이터가 허용 크기 {MaximumJournalBytes / (1024 * 1024)}MB를 초과했습니다.");
            }
        }

        private static long AddEstimatedPayloadBytes(long current, string value)
            => AddEstimatedPayloadBytes(current, value == null ? 0L : Encoding.UTF8.GetByteCount(value));

        private static long AddEstimatedPayloadBytes(long current, long additional)
        {
            if (additional <= 0)
            {
                return current;
            }

            return current > MaximumJournalBytes - additional
                ? MaximumJournalBytes + 1
                : current + additional;
        }

        private static void ValidateBox(WpfCrashRecoveryBox box, int imageWidth, int imageHeight)
        {
            if (box == null)
            {
                throw new InvalidDataException("복구 박스가 비어 있습니다.");
            }

            box.ClassName = NormalizeRequired(box.ClassName, "박스 클래스");
            if (box.Width <= 0 || box.Height <= 0
                || box.X < 0 || box.Y < 0
                || (long)box.X + box.Width > imageWidth
                || (long)box.Y + box.Height > imageHeight)
            {
                throw new InvalidDataException("복구 박스가 이미지 경계를 벗어났습니다.");
            }

            if (!Enum.TryParse(box.ShapeKind, ignoreCase: true, out CanvasRoiShapeKind _))
            {
                throw new InvalidDataException($"지원하지 않는 복구 박스 종류입니다: {box.ShapeKind}");
            }

            ValidateMetadata(box.Metadata);
        }

        private static int ValidateSegment(
            WpfCrashRecoverySegment segment,
            int imageWidth,
            int imageHeight)
        {
            if (segment == null)
            {
                throw new InvalidDataException("복구 세그멘테이션 객체가 비어 있습니다.");
            }

            segment.ClassName = NormalizeRequired(segment.ClassName, "세그멘테이션 클래스");
            segment.ObjectId = segment.ObjectId?.Trim() ?? string.Empty;
            segment.LastStructuralOperation = segment.LastStructuralOperation?.Trim() ?? string.Empty;
            segment.Points ??= new List<WpfCrashRecoveryPoint>();
            segment.CutoutPolygons ??= new List<List<WpfCrashRecoveryPoint>>();
            segment.MaskData ??= Array.Empty<byte>();

            int pointCount = segment.Points.Count;
            foreach (WpfCrashRecoveryPoint point in segment.Points)
            {
                ValidatePoint(point, imageWidth, imageHeight);
            }
            foreach (List<WpfCrashRecoveryPoint> cutout in segment.CutoutPolygons)
            {
                if (cutout == null || cutout.Count < 3)
                {
                    throw new InvalidDataException("복구 세그멘테이션의 내부 구멍이 올바르지 않습니다.");
                }
                pointCount += cutout.Count;
                foreach (WpfCrashRecoveryPoint point in cutout)
                {
                    ValidatePoint(point, imageWidth, imageHeight);
                }
            }

            bool hasPolygon = segment.Points.Count >= 3;
            bool hasMask = segment.MaskWidth > 0
                && segment.MaskHeight > 0
                && segment.MaskData.Length == (long)segment.MaskWidth * segment.MaskHeight;
            if (hasMask)
            {
                if (segment.MaskWidth != imageWidth || segment.MaskHeight != imageHeight)
                {
                    throw new InvalidDataException("복구 마스크 크기가 이미지 크기와 다릅니다.");
                }
            }
            else if (segment.MaskData.Length != 0 || segment.MaskWidth != 0 || segment.MaskHeight != 0)
            {
                throw new InvalidDataException("복구 마스크 데이터가 올바르지 않습니다.");
            }

            if (!hasPolygon && !hasMask)
            {
                throw new InvalidDataException("복구 세그멘테이션에 폴리곤이나 마스크가 없습니다.");
            }

            ValidateMetadata(segment.Metadata);
            return pointCount;
        }

        private static void ValidatePoint(WpfCrashRecoveryPoint point, int imageWidth, int imageHeight)
        {
            if (point == null
                || point.X < 0 || point.Y < 0
                || point.X >= imageWidth || point.Y >= imageHeight)
            {
                throw new InvalidDataException("복구 폴리곤 점이 이미지 경계를 벗어났습니다.");
            }
        }

        private static void ValidateMetadata(WpfCrashRecoveryMetadata metadata)
        {
            if (metadata == null)
            {
                throw new InvalidDataException("복구 객체 메타데이터가 비어 있습니다.");
            }

            metadata.Tags = ObjectMetadataStateService.NormalizeTags(metadata.Tags).ToList();
            metadata.GroupId = ObjectMetadataStateService.NormalizeGroupId(metadata.GroupId);
        }

        private string QuarantineUnsafe()
        {
            try
            {
                Directory.CreateDirectory(InvalidDirectory);
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture);
                string target = Path.Combine(InvalidDirectory, $"current-image-draft-{stamp}.invalid.json");
                File.Move(JournalPath, target, overwrite: false);
                return target;
            }
            catch
            {
                TryDeleteFile(JournalPath);
                return string.Empty;
            }
        }

        private static string NormalizeRequired(string value, string fieldName)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidDataException($"{fieldName} 값이 비어 있습니다.");
            }
            return normalized;
        }

        private static string NormalizePath(string value, string fieldName)
        {
            try
            {
                return Path.GetFullPath(NormalizeRequired(value, fieldName))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception ex) when (
                ex is ArgumentException
                || ex is NotSupportedException
                || ex is PathTooLongException)
            {
                throw new InvalidDataException($"{fieldName} 경로가 올바르지 않습니다.", ex);
            }
        }

        private static bool IsSha256(string value)
            => value?.Length == 64 && value.All(Uri.IsHexDigit);

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    [Obsolete("Use CrashRecoveryJournalService.", false)]
    public sealed class WpfCrashRecoveryJournalService : CrashRecoveryJournalService
    {
        public WpfCrashRecoveryJournalService(string applicationDataRoot = null) : base(applicationDataRoot)
        {
        }
    }

    public sealed class WpfCrashRecoveryEnvelope
    {
        public int SchemaVersion { get; set; }

        public string Format { get; set; } = string.Empty;

        public string PayloadSha256 { get; set; } = string.Empty;

        public WpfCrashRecoveryDraft Draft { get; set; }
    }

    public sealed class WpfCrashRecoveryDraft
    {
        public DateTime CreatedUtc { get; set; }

        public string ApplicationVersion { get; set; } = string.Empty;

        public string RecipeName { get; set; } = string.Empty;

        public string DatasetRootPath { get; set; } = string.Empty;

        public string ImagePath { get; set; } = string.Empty;

        public long ImageLength { get; set; }

        public long ImageLastWriteUtcTicks { get; set; }

        public int ImageWidth { get; set; }

        public int ImageHeight { get; set; }

        public string DirtyReason { get; set; } = string.Empty;

        public List<WpfCrashRecoveryBox> Boxes { get; set; } = new List<WpfCrashRecoveryBox>();

        public List<WpfCrashRecoverySegment> Segments { get; set; } = new List<WpfCrashRecoverySegment>();
    }

    public sealed class WpfCrashRecoveryBox
    {
        public string ClassName { get; set; } = string.Empty;

        public string ShapeKind { get; set; } = CanvasRoiShapeKind.Rectangle.ToString();

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public WpfCrashRecoveryMetadata Metadata { get; set; } = new WpfCrashRecoveryMetadata();
    }

    public sealed class WpfCrashRecoverySegment
    {
        public string ClassName { get; set; } = string.Empty;

        public string ObjectId { get; set; } = string.Empty;

        public int ComponentIndex { get; set; } = -1;

        public int ZOrder { get; set; }

        public string LastStructuralOperation { get; set; } = string.Empty;

        public List<WpfCrashRecoveryPoint> Points { get; set; } = new List<WpfCrashRecoveryPoint>();

        public List<List<WpfCrashRecoveryPoint>> CutoutPolygons { get; set; } =
            new List<List<WpfCrashRecoveryPoint>>();

        public byte[] MaskData { get; set; } = Array.Empty<byte>();

        public int MaskWidth { get; set; }

        public int MaskHeight { get; set; }

        public int MaskBoundsX { get; set; }

        public int MaskBoundsY { get; set; }

        public int MaskBoundsWidth { get; set; }

        public int MaskBoundsHeight { get; set; }

        public WpfCrashRecoveryMetadata Metadata { get; set; } = new WpfCrashRecoveryMetadata();
    }

    public sealed class WpfCrashRecoveryPoint
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    public sealed class WpfCrashRecoveryMetadata
    {
        public bool IsOccluded { get; set; }

        public List<string> Tags { get; set; } = new List<string>();

        public string GroupId { get; set; } = string.Empty;
    }

    public sealed class WpfCrashRecoveryReadResult
    {
        private WpfCrashRecoveryReadResult(
            WpfCrashRecoveryReadStatus status,
            WpfCrashRecoveryDraft draft,
            string error,
            string quarantinePath)
        {
            Status = status;
            Draft = draft;
            Error = error ?? string.Empty;
            QuarantinePath = quarantinePath ?? string.Empty;
        }

        public static WpfCrashRecoveryReadResult None { get; } =
            new WpfCrashRecoveryReadResult(WpfCrashRecoveryReadStatus.None, null, string.Empty, string.Empty);

        public WpfCrashRecoveryReadStatus Status { get; }

        public WpfCrashRecoveryDraft Draft { get; }

        public string Error { get; }

        public string QuarantinePath { get; }

        public static WpfCrashRecoveryReadResult Available(WpfCrashRecoveryDraft draft)
            => new WpfCrashRecoveryReadResult(
                WpfCrashRecoveryReadStatus.Available,
                draft,
                string.Empty,
                string.Empty);

        public static WpfCrashRecoveryReadResult Invalid(string error, string quarantinePath)
            => new WpfCrashRecoveryReadResult(
                WpfCrashRecoveryReadStatus.Invalid,
                null,
                error,
                quarantinePath);
    }

    public enum WpfCrashRecoveryReadStatus
    {
        None,
        Available,
        Invalid
    }
}
