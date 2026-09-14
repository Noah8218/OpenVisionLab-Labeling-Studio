using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace MvcVisionSystem
{
    public enum RecipeConfigurationFailureKind
    {
        None,
        Missing,
        ReadFailed,
        WriteFailed,
        ValidationFailed
    }

    public sealed class RecipeConfigurationLoadResult
    {
        public RecipeConfigurationLoadResult(
            LabelingProjectData data,
            string path,
            RecipeConfigurationFailureKind failureKind = RecipeConfigurationFailureKind.None,
            string errorMessage = "")
        {
            Data = data;
            Path = path ?? string.Empty;
            FailureKind = failureKind;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public LabelingProjectData Data { get; }

        public string Path { get; }

        public RecipeConfigurationFailureKind FailureKind { get; }

        public string ErrorMessage { get; }

        public bool IsSuccess => Data != null && FailureKind == RecipeConfigurationFailureKind.None;
    }

    public sealed class RecipeConfigurationSaveResult
    {
        public RecipeConfigurationSaveResult(
            string path,
            string backupPath = "",
            RecipeConfigurationFailureKind failureKind = RecipeConfigurationFailureKind.None,
            string errorMessage = "")
        {
            Path = path ?? string.Empty;
            BackupPath = backupPath ?? string.Empty;
            FailureKind = failureKind;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public string Path { get; }

        public string BackupPath { get; }

        public RecipeConfigurationFailureKind FailureKind { get; }

        public string ErrorMessage { get; }

        public bool IsSuccess => FailureKind == RecipeConfigurationFailureKind.None;
    }

    /// <summary>
    /// Owns only the raw VISION.xml persistence boundary. Workflow and UI
    /// callers receive a result; this type never opens a message box or mutates
    /// global application state.
    /// </summary>
    public sealed class RecipeConfigurationStore
    {
        private const string BackupExtension = ".bak";

        public RecipeConfigurationLoadResult Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new RecipeConfigurationLoadResult(
                    null,
                    path,
                    RecipeConfigurationFailureKind.Missing,
                    "Recipe configuration file does not exist.");
            }

            try
            {
                LabelingProjectData data = Deserialize(path);
                if (data == null)
                {
                    return new RecipeConfigurationLoadResult(
                        null,
                        path,
                        RecipeConfigurationFailureKind.ValidationFailed,
                        "Recipe configuration did not contain a data object.");
                }

                return new RecipeConfigurationLoadResult(data, path);
            }
            catch (Exception error) when (error is IOException
                || error is UnauthorizedAccessException
                || error is InvalidOperationException
                || error is XmlException)
            {
                AppLog.ABNORMAL($"Recipe configuration load failed: {path} / {error.Message}");
                return new RecipeConfigurationLoadResult(
                    null,
                    path,
                    RecipeConfigurationFailureKind.ReadFailed,
                    error.Message);
            }
        }

        public RecipeConfigurationSaveResult Save(string path, LabelingProjectData data)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new RecipeConfigurationSaveResult(
                    path,
                    failureKind: RecipeConfigurationFailureKind.WriteFailed,
                    errorMessage: "Recipe configuration path is required.");
            }

            if (data == null)
            {
                return new RecipeConfigurationSaveResult(
                    path,
                    failureKind: RecipeConfigurationFailureKind.ValidationFailed,
                    errorMessage: "Recipe configuration data is required.");
            }

            string directoryPath = Path.GetDirectoryName(path);
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string backupPath = path + BackupExtension;
            try
            {
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    throw new IOException("Recipe configuration directory is invalid.");
                }

                Directory.CreateDirectory(directoryPath);
                Serialize(temporaryPath, data);
                if (Deserialize(temporaryPath) == null)
                {
                    throw new InvalidDataException("Temporary Recipe configuration did not contain a data object.");
                }

                Yolo.AnnotationFilePersistence.ReplacePreparedFile(temporaryPath, path, backupPath);

                return new RecipeConfigurationSaveResult(path, File.Exists(backupPath) ? backupPath : string.Empty);
            }
            catch (Exception error) when (error is IOException
                || error is UnauthorizedAccessException
                || error is InvalidOperationException
                || error is XmlException)
            {
                AppLog.ABNORMAL($"Recipe configuration save failed: {path} / {error.Message}");
                return new RecipeConfigurationSaveResult(
                    path,
                    File.Exists(backupPath) ? backupPath : string.Empty,
                    RecipeConfigurationFailureKind.WriteFailed,
                    error.Message);
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        private static LabelingProjectData Deserialize(string path)
        {
            var serializer = new XmlSerializer(typeof(LabelingProjectData));
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using XmlReader reader = XmlReader.Create(stream, settings);
            return serializer.Deserialize(reader) as LabelingProjectData;
        }

        private static void Serialize(string path, LabelingProjectData data)
        {
            var serializer = new XmlSerializer(typeof(LabelingProjectData));
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            serializer.Serialize(stream, data);
            stream.Flush(flushToDisk: true);
        }

        private static void TryDeleteTemporaryFile(string path)
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
                // A failed cleanup must not overwrite the canonical Recipe file.
            }
        }
    }
}
