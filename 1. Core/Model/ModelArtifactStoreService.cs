using System;
using System.IO;

namespace MvcVisionSystem
{
    public sealed class ModelArtifactCaptureResult
    {
        internal ModelArtifactCaptureResult(
            string sha256,
            string artifactPath,
            string status,
            string error = "")
        {
            Sha256 = sha256 ?? string.Empty;
            ArtifactPath = artifactPath ?? string.Empty;
            Status = status ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public string Sha256 { get; }

        public string ArtifactPath { get; }

        public string Status { get; }

        public string Error { get; }

        public bool IsVerified => !string.IsNullOrWhiteSpace(Sha256)
            && !string.IsNullOrWhiteSpace(ArtifactPath)
            && string.Equals(Status, "verified", StringComparison.Ordinal);

        public bool IsLegacy => string.Equals(Status, "legacy", StringComparison.Ordinal);
    }

    /// <summary>
    /// Captures model weights into a content-addressed directory without
    /// overwriting an existing artifact. The source path remains provenance;
    /// the artifact path is the immutable bytes used for later verification.
    /// </summary>
    public static class ModelArtifactStoreService
    {
        public const string ArtifactFolderName = ".openvisionlab-artifacts";
        public const string WeightsFolderName = "weights";

        public static ModelArtifactCaptureResult Capture(
            string sourcePath,
            string outputRootPath,
            string expectedSha256 = "",
            string existingArtifactPath = "")
        {
            string normalizedSourcePath = NormalizePath(sourcePath);
            string normalizedExpectedSha256 = NormalizeHash(expectedSha256);
            string normalizedExistingArtifactPath = NormalizePath(existingArtifactPath);
            if (!string.IsNullOrWhiteSpace(expectedSha256)
                && string.IsNullOrWhiteSpace(normalizedExpectedSha256))
            {
                return new ModelArtifactCaptureResult(
                    string.Empty,
                    normalizedExistingArtifactPath,
                    "mismatch",
                    "recorded artifact hash is not a SHA-256 value");
            }

            if (!string.IsNullOrWhiteSpace(normalizedExistingArtifactPath)
                && !string.IsNullOrWhiteSpace(normalizedExpectedSha256)
                && Verify(normalizedExistingArtifactPath, normalizedExpectedSha256))
            {
                return Verified(normalizedExpectedSha256, normalizedExistingArtifactPath, "verified");
            }

            if (!File.Exists(normalizedSourcePath))
            {
                return new ModelArtifactCaptureResult(
                    string.Empty,
                    normalizedExistingArtifactPath,
                    "legacy",
                    "weights file is missing");
            }

            string sourceSha256;
            try
            {
                sourceSha256 = HashingService.ComputeFileSha256(normalizedSourcePath, lowerCase: true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return new ModelArtifactCaptureResult(string.Empty, string.Empty, "unavailable", ex.Message);
            }

            if (!string.IsNullOrWhiteSpace(normalizedExpectedSha256)
                && !string.Equals(sourceSha256, normalizedExpectedSha256, StringComparison.Ordinal))
            {
                return new ModelArtifactCaptureResult(
                    sourceSha256,
                    normalizedExistingArtifactPath,
                    "mismatch",
                    "source weights hash does not match the recorded artifact");
            }

            string targetPath = string.IsNullOrWhiteSpace(normalizedExistingArtifactPath)
                ? BuildArtifactPath(outputRootPath, normalizedSourcePath, sourceSha256)
                : normalizedExistingArtifactPath;
            if (string.Equals(normalizedSourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return Verified(sourceSha256, targetPath, "verified");
            }

            try
            {
                string directory = Path.GetDirectoryName(targetPath) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return new ModelArtifactCaptureResult(sourceSha256, string.Empty, "unavailable", "artifact directory is empty");
                }

                Directory.CreateDirectory(directory);
                if (File.Exists(targetPath))
                {
                    return Verify(targetPath, sourceSha256)
                        ? Verified(sourceSha256, targetPath, "verified")
                        : new ModelArtifactCaptureResult(sourceSha256, targetPath, "conflict", "existing artifact hash does not match its content address");
                }

                string temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    CopyAndFlush(normalizedSourcePath, temporaryPath);
                    if (!Verify(temporaryPath, sourceSha256))
                    {
                        return new ModelArtifactCaptureResult(sourceSha256, string.Empty, "unavailable", "copied artifact hash verification failed");
                    }

                    File.Move(temporaryPath, targetPath);
                }
                finally
                {
                    TryDelete(temporaryPath);
                }

                return Verify(targetPath, sourceSha256)
                    ? Verified(sourceSha256, targetPath, "verified")
                    : new ModelArtifactCaptureResult(sourceSha256, targetPath, "unavailable", "artifact hash verification failed after publish");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                if (File.Exists(targetPath) && Verify(targetPath, sourceSha256))
                {
                    return Verified(sourceSha256, targetPath, "verified");
                }

                return new ModelArtifactCaptureResult(sourceSha256, targetPath, "unavailable", ex.Message);
            }
        }

        public static bool Verify(string artifactPath, string expectedSha256)
        {
            string normalizedPath = NormalizePath(artifactPath);
            string normalizedHash = NormalizeHash(expectedSha256);
            if (string.IsNullOrWhiteSpace(normalizedPath)
                || string.IsNullOrWhiteSpace(normalizedHash)
                || !File.Exists(normalizedPath))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    HashingService.ComputeFileSha256(normalizedPath, lowerCase: true),
                    normalizedHash,
                    StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return false;
            }
        }

        private static ModelArtifactCaptureResult Verified(string sha256, string artifactPath, string status)
            => new ModelArtifactCaptureResult(sha256, artifactPath, status);

        private static string BuildArtifactPath(string outputRootPath, string sourcePath, string sha256)
        {
            string root = NormalizePath(outputRootPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            }

            string fileName = Path.GetFileName(sourcePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "weights.bin";
            }

            return Path.Combine(root, ArtifactFolderName, WeightsFolderName, sha256, fileName);
        }

        private static void CopyAndFlush(string sourcePath, string destinationPath)
        {
            using FileStream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
            using FileStream destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);
            source.CopyTo(destination);
            destination.Flush(flushToDisk: true);
        }

        private static string NormalizePath(string path)
        {
            string trimmed = path?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(trimmed);
            }
            catch
            {
                return trimmed;
            }
        }

        private static string NormalizeHash(string value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length != 64)
            {
                return string.Empty;
            }

            foreach (char character in trimmed)
            {
                bool isHex = character >= '0' && character <= '9'
                    || character >= 'a' && character <= 'f'
                    || character >= 'A' && character <= 'F';
                if (!isHex)
                {
                    return string.Empty;
                }
            }

            return trimmed.ToLowerInvariant();
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
