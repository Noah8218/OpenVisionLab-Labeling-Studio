using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace MvcVisionSystem
{
    public class PortableProjectArchiveService
    {
        public const int CurrentSchemaVersion = 1;
        public const string FormatName = "openvisionlab-labeling-project";
        public const string ManifestEntryName = "archive-manifest.json";
        public const int MaximumEntryCount = 100_000;
        public const long MaximumUncompressedBytes = 500L * 1024L * 1024L * 1024L;

        public WpfProjectArchiveExportResult Export(
            string recipeName,
            string recipeDirectory,
            string datasetRootPath,
            string archivePath)
        {
            string normalizedRecipeName = NormalizeRecipeName(recipeName);
            string normalizedRecipeDirectory = RequireExistingDirectory(recipeDirectory, "Recipe directory");
            string normalizedDatasetRoot = RequireExistingDirectory(datasetRootPath, "Dataset root");
            string normalizedArchivePath = RequireArchivePath(archivePath);
            EnsurePathOutside(normalizedArchivePath, normalizedRecipeDirectory, "Archive path must be outside the Recipe directory.");
            EnsurePathOutside(normalizedArchivePath, normalizedDatasetRoot, "Archive path must be outside the dataset directory.");

            string configPath = Path.Combine(normalizedRecipeDirectory, "VISION.xml");
            if (!File.Exists(configPath))
            {
                throw new InvalidDataException("The Recipe does not contain VISION.xml.");
            }

            LabelingProjectData data = SerializeHelper.FromXmlFile<LabelingProjectData>(configPath)
                ?? throw new InvalidDataException("VISION.xml could not be read.");
            string configuredDatasetRoot = Path.GetFullPath(data.OutputRootPath);
            if (!PathsEqual(configuredDatasetRoot, normalizedDatasetRoot))
            {
                throw new InvalidDataException("VISION.xml does not point to the selected dataset root.");
            }

            var sources = new List<ArchiveSourceFile>();
            AddDirectoryFiles(sources, normalizedRecipeDirectory, "recipe", "recipe");
            AddDirectoryFiles(sources, normalizedDatasetRoot, "dataset", "dataset");
            if (sources.Count == 0 || sources.Count > MaximumEntryCount)
            {
                throw new InvalidDataException($"Project archive file count must be between 1 and {MaximumEntryCount:N0}.");
            }

            List<WpfProjectArchiveFileRecord> records = sources
                .Select(source => BuildRecord(source))
                .OrderBy(record => record.EntryName, StringComparer.Ordinal)
                .ToList();
            long totalBytes = checked(records.Sum(record => record.Length));
            if (totalBytes > MaximumUncompressedBytes)
            {
                throw new InvalidDataException("Project archive content exceeds the supported uncompressed size.");
            }

            var manifest = new WpfProjectArchiveManifest
            {
                SchemaVersion = CurrentSchemaVersion,
                Format = FormatName,
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                SourceApplicationVersion = ResolveApplicationVersion(),
                MinimumCompatibleApplicationVersion = ResolveApplicationVersion(),
                RecipeName = normalizedRecipeName,
                DatasetPurpose = data.ProjectSettings?.DatasetPurpose.ToString() ?? string.Empty,
                DatasetVersionId = TryReadDatasetVersionId(
                    Path.Combine(normalizedRecipeDirectory, LabelingDatasetManifestService.FileName)),
                SourceDatasetRoot = normalizedDatasetRoot,
                Classes = data.ClassNamedList?
                    .Select(item => item?.Text?.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList() ?? new List<string>(),
                Files = records,
                ExternalReferences = BuildExternalReferences(data, normalizedDatasetRoot)
            };

            string parentPath = Path.GetDirectoryName(normalizedArchivePath)
                ?? throw new InvalidDataException("Archive parent directory could not be resolved.");
            Directory.CreateDirectory(parentPath);
            string temporaryPath = normalizedArchivePath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
                {
                    WriteTextEntry(
                        archive,
                        ManifestEntryName,
                        JsonConvert.SerializeObject(manifest, Formatting.Indented));
                    foreach (ArchiveSourceFile source in sources.OrderBy(item => item.EntryName, StringComparer.Ordinal))
                    {
                        archive.CreateEntryFromFile(source.SourcePath, source.EntryName, CompressionLevel.Optimal);
                    }
                }

                ValidateArchive(normalizedArchivePath: temporaryPath);
                File.Move(temporaryPath, normalizedArchivePath, overwrite: true);
            }
            finally
            {
                TryDeleteFile(temporaryPath);
            }

            return new WpfProjectArchiveExportResult
            {
                ArchivePath = normalizedArchivePath,
                RecipeName = normalizedRecipeName,
                FileCount = records.Count,
                TotalBytes = totalBytes,
                ExternalReferenceCount = manifest.ExternalReferences.Count
            };
        }

        public WpfProjectArchiveImportResult Import(
            string archivePath,
            string recipeRootPath,
            string datasetParentPath)
        {
            string normalizedArchivePath = Path.GetFullPath(archivePath ?? string.Empty);
            if (!File.Exists(normalizedArchivePath))
            {
                throw new FileNotFoundException("Project archive was not found.", normalizedArchivePath);
            }

            WpfProjectArchiveManifest manifest = ValidateArchive(normalizedArchivePath);
            string recipeName = NormalizeRecipeName(manifest.RecipeName);
            string normalizedRecipeRoot = Path.GetFullPath(recipeRootPath ?? string.Empty);
            string normalizedDatasetParent = Path.GetFullPath(datasetParentPath ?? string.Empty);
            Directory.CreateDirectory(normalizedRecipeRoot);
            Directory.CreateDirectory(normalizedDatasetParent);

            string targetRecipeDirectory = Path.Combine(normalizedRecipeRoot, recipeName);
            string targetDatasetRoot = Path.Combine(normalizedDatasetParent, recipeName);
            if (Directory.Exists(targetRecipeDirectory) || File.Exists(targetRecipeDirectory))
            {
                throw new IOException("A Recipe with the archive name already exists: " + recipeName);
            }
            if (Directory.Exists(targetDatasetRoot) || File.Exists(targetDatasetRoot))
            {
                throw new IOException("A dataset folder with the archive name already exists: " + targetDatasetRoot);
            }

            string operationId = Guid.NewGuid().ToString("N");
            string stagedRecipeDirectory = Path.Combine(normalizedRecipeRoot, ".ovl-import-" + operationId);
            string stagedDatasetRoot = Path.Combine(normalizedDatasetParent, ".ovl-import-" + operationId);
            bool recipePromoted = false;
            bool datasetPromoted = false;
            try
            {
                Directory.CreateDirectory(stagedRecipeDirectory);
                Directory.CreateDirectory(stagedDatasetRoot);
                ExtractValidatedArchive(
                    normalizedArchivePath,
                    manifest,
                    stagedRecipeDirectory,
                    stagedDatasetRoot);
                RebaseImportedTextFiles(
                    stagedRecipeDirectory,
                    stagedDatasetRoot,
                    manifest.SourceDatasetRoot,
                    targetDatasetRoot);
                LabelingProjectData importedData = ValidateImportedRecipe(
                    stagedRecipeDirectory,
                    targetDatasetRoot,
                    manifest);
                RewriteImportedDatasetManifest(
                    Path.Combine(stagedRecipeDirectory, LabelingDatasetManifestService.FileName),
                    recipeName,
                    targetDatasetRoot,
                    importedData);

                Directory.Move(stagedDatasetRoot, targetDatasetRoot);
                datasetPromoted = true;
                Directory.Move(stagedRecipeDirectory, targetRecipeDirectory);
                recipePromoted = true;

                return new WpfProjectArchiveImportResult
                {
                    ArchivePath = normalizedArchivePath,
                    RecipeName = recipeName,
                    RecipeDirectory = targetRecipeDirectory,
                    DatasetRootPath = targetDatasetRoot,
                    FileCount = manifest.Files.Count,
                    TotalBytes = manifest.Files.Sum(file => file.Length),
                    ExternalReferenceCount = manifest.ExternalReferences?.Count ?? 0
                };
            }
            catch
            {
                if (recipePromoted)
                {
                    TryDeleteDirectory(targetRecipeDirectory);
                }
                if (datasetPromoted)
                {
                    TryDeleteDirectory(targetDatasetRoot);
                }
                throw;
            }
            finally
            {
                TryDeleteDirectory(stagedRecipeDirectory);
                TryDeleteDirectory(stagedDatasetRoot);
            }
        }

        public WpfProjectArchiveManifest ValidateArchive(string normalizedArchivePath)
        {
            string archivePath = Path.GetFullPath(normalizedArchivePath ?? string.Empty);
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumEntryCount + 1)
            {
                throw new InvalidDataException("Project archive entry count is outside the supported range.");
            }

            Dictionary<string, ZipArchiveEntry> entries = BuildEntryMap(archive);
            if (!entries.TryGetValue(ManifestEntryName, out ZipArchiveEntry manifestEntry))
            {
                throw new InvalidDataException("Project archive manifest is missing.");
            }

            WpfProjectArchiveManifest manifest;
            using (StreamReader reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                manifest = JsonConvert.DeserializeObject<WpfProjectArchiveManifest>(reader.ReadToEnd());
            }

            ValidateManifest(manifest);
            var expectedNames = new HashSet<string>(
                manifest.Files.Select(file => NormalizeEntryName(file.EntryName)),
                StringComparer.OrdinalIgnoreCase);
            var actualNames = new HashSet<string>(
                entries.Keys.Where(name => !string.Equals(name, ManifestEntryName, StringComparison.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);
            if (!expectedNames.SetEquals(actualNames))
            {
                throw new InvalidDataException("Project archive entries do not match the signed manifest list.");
            }

            long totalBytes = 0;
            foreach (WpfProjectArchiveFileRecord record in manifest.Files)
            {
                string entryName = NormalizeEntryName(record.EntryName);
                ZipArchiveEntry entry = entries[entryName];
                if (entry.Length != record.Length)
                {
                    throw new InvalidDataException("Project archive entry length mismatch: " + entryName);
                }

                totalBytes = checked(totalBytes + entry.Length);
                if (totalBytes > MaximumUncompressedBytes)
                {
                    throw new InvalidDataException("Project archive content exceeds the supported uncompressed size.");
                }

                using Stream stream = entry.Open();
                string actualSha256 = HashingService.ComputeStreamSha256(stream, lowerCase: true);
                if (!string.Equals(actualSha256, record.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Project archive checksum mismatch: " + entryName);
                }
            }

            return manifest;
        }

        private static void ExtractValidatedArchive(
            string archivePath,
            WpfProjectArchiveManifest manifest,
            string recipeStage,
            string datasetStage)
        {
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            Dictionary<string, ZipArchiveEntry> entries = BuildEntryMap(archive);
            foreach (WpfProjectArchiveFileRecord record in manifest.Files)
            {
                string entryName = NormalizeEntryName(record.EntryName);
                string prefix;
                string targetRoot;
                if (entryName.StartsWith("recipe/", StringComparison.OrdinalIgnoreCase))
                {
                    prefix = "recipe/";
                    targetRoot = recipeStage;
                }
                else if (entryName.StartsWith("dataset/", StringComparison.OrdinalIgnoreCase))
                {
                    prefix = "dataset/";
                    targetRoot = datasetStage;
                }
                else
                {
                    throw new InvalidDataException("Unsupported project archive entry root: " + entryName);
                }

                string relativePath = entryName.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
                string targetPath = Path.GetFullPath(Path.Combine(targetRoot, relativePath));
                EnsurePathInside(targetPath, targetRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? targetRoot);
                using Stream source = entries[entryName].Open();
                using FileStream target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                source.CopyTo(target);
            }
        }

        private static LabelingProjectData ValidateImportedRecipe(
            string stagedRecipeDirectory,
            string targetDatasetRoot,
            WpfProjectArchiveManifest manifest)
        {
            string configPath = Path.Combine(stagedRecipeDirectory, "VISION.xml");
            if (!File.Exists(configPath))
            {
                throw new InvalidDataException("Imported Recipe is missing VISION.xml.");
            }

            LabelingProjectData data = SerializeHelper.FromXmlFile<LabelingProjectData>(configPath)
                ?? throw new InvalidDataException("Imported VISION.xml could not be deserialized.");
            if (!PathsEqual(data.OutputRootPath, targetDatasetRoot))
            {
                throw new InvalidDataException("Imported VISION.xml was not rebased to the selected dataset destination.");
            }

            string[] actualClasses = data.ClassNamedList?
                .Select(item => item?.Text?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray() ?? Array.Empty<string>();
            string[] expectedClasses = manifest.Classes?
                .Select(item => item?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray() ?? Array.Empty<string>();
            if (!actualClasses.SequenceEqual(expectedClasses, StringComparer.Ordinal))
            {
                throw new InvalidDataException("Imported Recipe class order does not match the archive manifest.");
            }

            return data;
        }

        private static void RebaseImportedTextFiles(
            string recipeStage,
            string datasetStage,
            string sourceDatasetRoot,
            string targetDatasetRoot)
        {
            RebaseXmlConfig(
                Path.Combine(recipeStage, "VISION.xml"),
                sourceDatasetRoot,
                targetDatasetRoot);

            foreach (string root in new[] { recipeStage, datasetStage })
            {
                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    string extension = Path.GetExtension(path);
                    if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        ReplacePathReferences(path, sourceDatasetRoot, targetDatasetRoot);
                    }
                }
            }
        }

        private static void RebaseXmlConfig(string configPath, string sourceRoot, string targetRoot)
        {
            XDocument document = XDocument.Load(configPath, LoadOptions.PreserveWhitespace);
            foreach (XElement element in document.Descendants().Where(item => !item.HasElements))
            {
                string value = element.Value?.Trim() ?? string.Empty;
                if (TryRebasePath(value, sourceRoot, targetRoot, out string rebased))
                {
                    element.Value = rebased;
                }
            }
            document.Save(configPath);
        }

        private static void ReplacePathReferences(string path, string sourceRoot, string targetRoot)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (DecoderFallbackException)
            {
                return;
            }

            string replaced = text;
            foreach ((string source, string target) in BuildPathReplacementPairs(sourceRoot, targetRoot))
            {
                replaced = replaced.Replace(source, target, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.Equals(text, replaced, StringComparison.Ordinal))
            {
                File.WriteAllText(path, replaced, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }

        private static IEnumerable<(string Source, string Target)> BuildPathReplacementPairs(
            string sourceRoot,
            string targetRoot)
        {
            string source = Path.GetFullPath(sourceRoot ?? string.Empty).TrimEnd('\\', '/');
            string target = Path.GetFullPath(targetRoot ?? string.Empty).TrimEnd('\\', '/');
            yield return (JsonEscape(source), JsonEscape(target));
            yield return (source.Replace('\\', '/'), target.Replace('\\', '/'));
            yield return (source, target);
        }

        private static string JsonEscape(string value)
            => JsonConvert.ToString(value ?? string.Empty).Trim('"');

        private static bool TryRebasePath(
            string value,
            string sourceRoot,
            string targetRoot,
            out string rebased)
        {
            rebased = value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value))
            {
                return false;
            }

            string fullValue;
            string fullSource;
            try
            {
                fullValue = Path.GetFullPath(value);
                fullSource = Path.GetFullPath(sourceRoot);
            }
            catch
            {
                return false;
            }

            if (!IsPathInsideOrEqual(fullValue, fullSource))
            {
                return false;
            }

            string relativePath = Path.GetRelativePath(fullSource, fullValue);
            rebased = Path.GetFullPath(Path.Combine(targetRoot, relativePath));
            return true;
        }

        private static void RewriteImportedDatasetManifest(
            string manifestPath,
            string recipeName,
            string datasetRoot,
            LabelingProjectData data)
        {
            if (!File.Exists(manifestPath))
            {
                return;
            }

            JObject document = JObject.Parse(File.ReadAllText(manifestPath));
            document["recipeName"] = recipeName;
            document["outputRootPath"] = datasetRoot;
            document["imageRootPath"] = data.ProjectSettings?.ResolveImageRootPath() ?? string.Empty;
            document["dataYamlFilePath"] = data.DataYamlFilePath;
            File.WriteAllText(manifestPath, document.ToString(Formatting.Indented));
        }

        private static Dictionary<string, ZipArchiveEntry> BuildEntryMap(ZipArchive archive)
        {
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string entryName = NormalizeEntryName(entry.FullName);
                if (string.IsNullOrWhiteSpace(entryName) || entryName.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }
                if (!entries.TryAdd(entryName, entry))
                {
                    throw new InvalidDataException("Project archive contains a duplicate entry: " + entryName);
                }
            }
            return entries;
        }

        private static void ValidateManifest(WpfProjectArchiveManifest manifest)
        {
            if (manifest == null
                || manifest.SchemaVersion != CurrentSchemaVersion
                || !string.Equals(manifest.Format, FormatName, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(manifest.SourceApplicationVersion)
                || string.IsNullOrWhiteSpace(manifest.MinimumCompatibleApplicationVersion)
                || string.IsNullOrWhiteSpace(manifest.SourceDatasetRoot)
                || !Path.IsPathRooted(manifest.SourceDatasetRoot))
            {
                throw new InvalidDataException("Project archive format or schema version is not supported.");
            }

            NormalizeRecipeName(manifest.RecipeName);
            manifest.Files ??= new List<WpfProjectArchiveFileRecord>();
            manifest.Classes ??= new List<string>();
            manifest.ExternalReferences ??= new List<WpfProjectArchiveExternalReference>();
            if (manifest.Files.Count == 0 || manifest.Files.Count > MaximumEntryCount)
            {
                throw new InvalidDataException("Project archive manifest file count is outside the supported range.");
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WpfProjectArchiveFileRecord record in manifest.Files)
            {
                string entryName = NormalizeEntryName(record?.EntryName);
                if (!entryName.StartsWith("recipe/", StringComparison.OrdinalIgnoreCase)
                    && !entryName.StartsWith("dataset/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Project archive manifest contains an unsupported entry root.");
                }
                if (!names.Add(entryName))
                {
                    throw new InvalidDataException("Project archive manifest contains duplicate entries.");
                }
                if (record.Length < 0
                    || string.IsNullOrWhiteSpace(record.Sha256)
                    || record.Sha256.Length != 64
                    || record.Sha256.Any(character => !Uri.IsHexDigit(character)))
                {
                    throw new InvalidDataException("Project archive manifest contains an invalid file record.");
                }
            }

            if (!names.Contains("recipe/VISION.xml"))
            {
                throw new InvalidDataException("Project archive manifest does not contain recipe/VISION.xml.");
            }
        }

        private static void AddDirectoryFiles(
            ICollection<ArchiveSourceFile> target,
            string directoryPath,
            string entryRoot,
            string kind)
        {
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(directoryPath);
            while (pendingDirectories.Count > 0)
            {
                string currentDirectory = pendingDirectories.Pop();
                foreach (string childDirectory in Directory.EnumerateDirectories(
                    currentDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly))
                {
                    if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException(
                            "Project archive does not follow directory reparse points: " + childDirectory);
                    }
                    pendingDirectories.Push(childDirectory);
                }

                foreach (string path in Directory.EnumerateFiles(
                    currentDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly))
                {
                    FileAttributes attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException(
                            "Project archive does not follow file reparse points: " + path);
                    }

                    string fileName = Path.GetFileName(path);
                    if (fileName.Contains(".tmp-", StringComparison.OrdinalIgnoreCase)
                        || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string relativePath = Path.GetRelativePath(directoryPath, path)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/');
                    target.Add(new ArchiveSourceFile
                    {
                        SourcePath = path,
                        EntryName = NormalizeEntryName(entryRoot + "/" + relativePath),
                        Kind = kind,
                        Split = ResolveSplit(entryRoot, relativePath)
                    });
                }
            }
        }

        private static WpfProjectArchiveFileRecord BuildRecord(ArchiveSourceFile source)
        {
            var info = new FileInfo(source.SourcePath);
            using FileStream stream = File.OpenRead(source.SourcePath);
            return new WpfProjectArchiveFileRecord
            {
                EntryName = source.EntryName,
                Kind = source.Kind,
                Split = source.Split,
                Length = info.Length,
                Sha256 = HashingService.ComputeStreamSha256(stream, lowerCase: true)
            };
        }

        private static string ResolveSplit(string entryRoot, string relativePath)
        {
            if (!string.Equals(entryRoot, "dataset", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string[] parts = relativePath.Replace('\\', '/').Split('/');
            return parts.Length >= 3
                && string.Equals(parts[0], "data", StringComparison.OrdinalIgnoreCase)
                && new[] { "train", "valid", "test" }.Contains(parts[1], StringComparer.OrdinalIgnoreCase)
                    ? parts[1].ToLowerInvariant()
                    : string.Empty;
        }

        private static List<WpfProjectArchiveExternalReference> BuildExternalReferences(
            LabelingProjectData data,
            string datasetRoot)
        {
            var references = new List<WpfProjectArchiveExternalReference>();
            PythonModelSettings python = data.ProjectSettings?.PythonModel;
            ExternalYoloDatasetSettings external = data.ProjectSettings?.ExternalYoloDataset;
            AddExternalReference(references, "pythonExecutable", python?.PythonExecutablePath, datasetRoot);
            AddExternalReference(references, "modelProjectRoot", python?.ProjectRootPath, datasetRoot);
            AddExternalReference(references, "modelClientScript", python?.ClientScriptPath, datasetRoot);
            AddExternalReference(references, "modelWeights", python?.WeightsPath, datasetRoot);
            AddExternalReference(
                references,
                "imageRoot",
                data.ProjectSettings?.ResolveImageRootPath(),
                datasetRoot);
            AddExternalReference(references, "externalYoloDataYaml", external?.DataYamlFilePath, datasetRoot);
            return references;
        }

        private static void AddExternalReference(
            ICollection<WpfProjectArchiveExternalReference> references,
            string kind,
            string path,
            string datasetRoot)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            if (!IsPathInsideOrEqual(fullPath, datasetRoot))
            {
                references.Add(new WpfProjectArchiveExternalReference
                {
                    Kind = kind ?? string.Empty,
                    Path = fullPath,
                    Included = false
                });
            }
        }

        private static string TryReadDatasetVersionId(string manifestPath)
        {
            try
            {
                return File.Exists(manifestPath)
                    ? JObject.Parse(File.ReadAllText(manifestPath))["datasetVersionId"]?.Value<string>() ?? string.Empty
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string RequireExistingDirectory(string path, string description)
        {
            string fullPath = Path.GetFullPath(path ?? string.Empty);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException(description + " was not found: " + fullPath);
            }
            return fullPath;
        }

        private static string RequireArchivePath(string path)
        {
            string fullPath = Path.GetFullPath(path ?? string.Empty);
            if (!string.Equals(Path.GetExtension(fullPath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Project archive path must use the .zip extension.");
            }
            return fullPath;
        }

        private static string NormalizeRecipeName(string recipeName)
        {
            string normalized = recipeName?.Trim() ?? string.Empty;
            if (!ProjectRecipeService.IsValidRecipeName(normalized)
                || normalized == "."
                || normalized == "..")
            {
                throw new InvalidDataException("Project archive Recipe name is invalid.");
            }
            return normalized;
        }

        private static string NormalizeEntryName(string entryName)
        {
            string normalized = (entryName ?? string.Empty).Replace('\\', '/').TrimStart('/');
            string[] parts = normalized.Split('/');
            if (string.IsNullOrWhiteSpace(normalized)
                || normalized.Contains('\0')
                || parts.Any(IsUnsafeEntrySegment))
            {
                throw new InvalidDataException("Project archive contains an unsafe entry path.");
            }
            return normalized;
        }

        private static bool IsUnsafeEntrySegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment)
                || segment == "."
                || segment == ".."
                || segment.EndsWith(" ", StringComparison.Ordinal)
                || segment.EndsWith(".", StringComparison.Ordinal)
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return true;
            }

            string stem = Path.GetFileNameWithoutExtension(segment);
            return new[]
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            }.Contains(stem, StringComparer.OrdinalIgnoreCase);
        }

        private static void EnsurePathInside(string path, string root)
        {
            if (!IsPathInsideOrEqual(path, root))
            {
                throw new InvalidDataException("Project archive extraction attempted to leave the staging directory.");
            }
        }

        private static void EnsurePathOutside(string path, string root, string message)
        {
            if (IsPathInsideOrEqual(path, root))
            {
                throw new InvalidDataException(message);
            }
        }

        private static bool IsPathInsideOrEqual(string path, string root)
        {
            string normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(left ?? string.Empty).TrimEnd('\\', '/'),
                    Path.GetFullPath(right ?? string.Empty).TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveApplicationVersion()
            => typeof(PortableProjectArchiveService).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                ?? typeof(PortableProjectArchiveService).Assembly.GetName().Version?.ToString()
                ?? "0.0.0";

        private static void WriteTextEntry(ZipArchive archive, string entryName, string text)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using StreamWriter writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(text ?? string.Empty);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
            }
        }

        private sealed class ArchiveSourceFile
        {
            public string SourcePath { get; init; } = string.Empty;
            public string EntryName { get; init; } = string.Empty;
            public string Kind { get; init; } = string.Empty;
            public string Split { get; init; } = string.Empty;
        }
    }

    [Obsolete("Use PortableProjectArchiveService.", false)]
    public sealed class WpfPortableProjectArchiveService : PortableProjectArchiveService
    {
    }

    public sealed class WpfProjectArchiveManifest
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; } = string.Empty;

        [JsonProperty("createdUtc")]
        public string CreatedUtc { get; set; } = string.Empty;

        [JsonProperty("sourceApplicationVersion")]
        public string SourceApplicationVersion { get; set; } = string.Empty;

        [JsonProperty("minimumCompatibleApplicationVersion")]
        public string MinimumCompatibleApplicationVersion { get; set; } = string.Empty;

        [JsonProperty("recipeName")]
        public string RecipeName { get; set; } = string.Empty;

        [JsonProperty("datasetPurpose")]
        public string DatasetPurpose { get; set; } = string.Empty;

        [JsonProperty("datasetVersionId")]
        public string DatasetVersionId { get; set; } = string.Empty;

        [JsonProperty("sourceDatasetRoot")]
        public string SourceDatasetRoot { get; set; } = string.Empty;

        [JsonProperty("classes")]
        public List<string> Classes { get; set; } = new List<string>();

        [JsonProperty("files")]
        public List<WpfProjectArchiveFileRecord> Files { get; set; } = new List<WpfProjectArchiveFileRecord>();

        [JsonProperty("externalReferences")]
        public List<WpfProjectArchiveExternalReference> ExternalReferences { get; set; } =
            new List<WpfProjectArchiveExternalReference>();
    }

    public sealed class WpfProjectArchiveFileRecord
    {
        [JsonProperty("entryName")]
        public string EntryName { get; set; } = string.Empty;

        [JsonProperty("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonProperty("split")]
        public string Split { get; set; } = string.Empty;

        [JsonProperty("length")]
        public long Length { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; } = string.Empty;
    }

    public sealed class WpfProjectArchiveExternalReference
    {
        [JsonProperty("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonProperty("path")]
        public string Path { get; set; } = string.Empty;

        [JsonProperty("included")]
        public bool Included { get; set; }
    }

    public sealed class WpfProjectArchiveExportResult
    {
        public string ArchivePath { get; init; } = string.Empty;
        public string RecipeName { get; init; } = string.Empty;
        public int FileCount { get; init; }
        public long TotalBytes { get; init; }
        public int ExternalReferenceCount { get; init; }
    }

    public sealed class WpfProjectArchiveImportResult
    {
        public string ArchivePath { get; init; } = string.Empty;
        public string RecipeName { get; init; } = string.Empty;
        public string RecipeDirectory { get; init; } = string.Empty;
        public string DatasetRootPath { get; init; } = string.Empty;
        public int FileCount { get; init; }
        public long TotalBytes { get; init; }
        public int ExternalReferenceCount { get; init; }
    }
}
