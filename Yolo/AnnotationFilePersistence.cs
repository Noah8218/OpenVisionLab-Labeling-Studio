using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Linq;
using Newtonsoft.Json;

namespace MvcVisionSystem.Yolo
{
    internal enum AnnotationFilePersistenceFaultPoint
    {
        BeforeTemporaryFlush,
        BeforeCommit,
        AfterCommit,
        AfterTransactionCommit
    }

    internal static class AnnotationFilePersistence
    {
        private static readonly object transactionSync = new object();
        private static string recoveryDirectory;

        [ThreadStatic]
        private static AnnotationFileTransaction currentTransaction;

        [ThreadStatic]
        private static Action<AnnotationFilePersistenceFaultPoint, string> testFaultInjector;

        internal static string RecoveryDirectory => recoveryDirectory;

        // The application configures its existing per-user data root before opening a Recipe.
        // Tests supply an isolated root; the persistence owner does not depend on WPF paths.
        internal static void ConfigureRecoveryDirectory(string directory)
        {
            lock (transactionSync)
            {
                if (currentTransaction != null)
                {
                    throw new InvalidOperationException("Cannot change recovery storage during a save.");
                }

                recoveryDirectory = directory == null ? null : Path.GetFullPath(directory);
                RecoverInterruptedTransactions();
            }
        }

        internal static void RecoverInterruptedTransactions()
        {
            lock (transactionSync)
            {
                if (recoveryDirectory == null || !Directory.Exists(recoveryDirectory))
                {
                    return;
                }

                foreach (string path in Directory.EnumerateFiles(recoveryDirectory, "*.json"))
                {
                    AnnotationFileTransaction.Recover(path);
                }
            }
        }

        internal static IDisposable PushTestFaultInjector(
            Action<AnnotationFilePersistenceFaultPoint, string> injector)
        {
            ArgumentNullException.ThrowIfNull(injector);
            Action<AnnotationFilePersistenceFaultPoint, string> previous = testFaultInjector;
            testFaultInjector = injector;
            return new TestFaultInjectorScope(previous);
        }

        public static bool ExecuteTransaction(Func<bool> saveFiles)
        {
            // ponytail: one writer is sufficient for this single-operator application.
            // Keep recovery and writes serialized; use dataset locks only if concurrent editing is added.
            lock (transactionSync)
            {
                return ExecuteTransactionCore(saveFiles);
            }
        }

        private static bool ExecuteTransactionCore(Func<bool> saveFiles)
        {
            ArgumentNullException.ThrowIfNull(saveFiles);
            if (currentTransaction != null)
            {
                return saveFiles();
            }

            RecoverInterruptedTransactions();
            var transaction = new AnnotationFileTransaction(recoveryDirectory);
            currentTransaction = transaction;
            try
            {
                bool shouldCommit;
                try
                {
                    shouldCommit = saveFiles();
                    if (shouldCommit)
                    {
                        transaction.Commit();
                    }
                }
                catch (Exception saveFailure)
                {
                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception rollbackFailure)
                    {
                        throw new AggregateException(
                            "Annotation save failed and its file rollback was incomplete.",
                            saveFailure,
                            rollbackFailure);
                    }

                    ExceptionDispatchInfo.Capture(saveFailure).Throw();
                    throw;
                }

                if (!shouldCommit)
                {
                    transaction.Rollback();
                    return false;
                }

                return true;
            }
            finally
            {
                currentTransaction = null;
            }
        }

        public static void ExecuteTransaction(Action saveFiles)
        {
            ArgumentNullException.ThrowIfNull(saveFiles);
            ExecuteTransaction(() =>
            {
                saveFiles();
                return true;
            });
        }

        public static void WriteAtomically(string path, Action<string> writeTemporaryFile)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Annotation path is required.", nameof(path));
            }

            ArgumentNullException.ThrowIfNull(writeTemporaryFile);

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("Annotation path has no parent directory.");
            Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(fullPath)}.tmp-{Guid.NewGuid():N}");
            try
            {
                writeTemporaryFile(temporaryPath);
                InjectTestFault(AnnotationFilePersistenceFaultPoint.BeforeTemporaryFlush, fullPath);
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None))
                {
                    stream.Flush(flushToDisk: true);
                }

                if (currentTransaction != null)
                {
                    InjectTestFault(AnnotationFilePersistenceFaultPoint.BeforeCommit, fullPath);
                    currentTransaction.Write(temporaryPath, fullPath);
                }
                else
                {
                    InjectTestFault(AnnotationFilePersistenceFaultPoint.BeforeCommit, fullPath);
                    ReplaceOrMove(temporaryPath, fullPath, backupPath: null);
                }

                InjectTestFault(AnnotationFilePersistenceFaultPoint.AfterCommit, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        internal static void ReplacePreparedFile(string temporaryPath, string path, string backupPath)
        {
            if (currentTransaction == null)
            {
                ReplaceOrMove(temporaryPath, path, backupPath);
                return;
            }

            if (File.Exists(path))
            {
                WriteAtomically(backupPath, staged => File.Copy(path, staged));
            }

            WriteAtomically(path, staged => File.Move(temporaryPath, staged));
        }

        public static void Delete(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return;
            }

            if (currentTransaction != null)
            {
                currentTransaction.Delete(fullPath);
                return;
            }

            File.Delete(fullPath);
        }

        private static void ReplaceOrMove(string sourcePath, string destinationPath, string backupPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Replace(sourcePath, destinationPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }
        }

        private static void InjectTestFault(
            AnnotationFilePersistenceFaultPoint point,
            string path)
        {
            testFaultInjector?.Invoke(point, path);
        }

        private sealed class TestFaultInjectorScope : IDisposable
        {
            private readonly Action<AnnotationFilePersistenceFaultPoint, string> previous;
            private bool disposed;

            public TestFaultInjectorScope(
                Action<AnnotationFilePersistenceFaultPoint, string> previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                testFaultInjector = previous;
                disposed = true;
            }
        }

        private sealed class AnnotationFileTransaction
        {
            private readonly Dictionary<string, TransactionEntry> entries =
                new Dictionary<string, TransactionEntry>(StringComparer.OrdinalIgnoreCase);
            private readonly List<TransactionEntry> order = new List<TransactionEntry>();
            private readonly string id;
            private readonly string journalPath;

            public AnnotationFileTransaction(string directory)
            {
                id = Guid.NewGuid().ToString("N");
                journalPath = directory == null ? null : Path.Combine(directory, id + ".json");
            }

            private AnnotationFileTransaction(string path, JournalRecord record)
            {
                id = record.Id;
                journalPath = path;
                order.AddRange(record.Entries);
            }

            public static void Recover(string path)
            {
                JournalEnvelope envelope = JsonConvert.DeserializeObject<JournalEnvelope>(File.ReadAllText(path));
                if (envelope?.Payload == null || !string.Equals(envelope.Sha256,
                    HashingService.ComputeUtf8TextSha256(envelope.Payload, lowerCase: true), StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Annotation recovery journal checksum failed: " + path);
                }

                JournalRecord record = JsonConvert.DeserializeObject<JournalRecord>(envelope.Payload);
                if (record?.Version != 1 || !Guid.TryParseExact(record.Id, "N", out _)
                    || Path.GetFileNameWithoutExtension(path) != record.Id || record.Entries == null
                    || record.Entries.Any(entry => entry == null || !Path.IsPathFullyQualified(entry.TargetPath)
                        || !string.Equals(entry.BackupPath, entry.OriginalExists ? CreateBackupPath(entry.TargetPath, record.Id) : string.Empty, StringComparison.Ordinal))
                    || record.Entries.Select(entry => entry.TargetPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != record.Entries.Count)
                {
                    throw new InvalidDataException("Annotation recovery journal is invalid: " + path);
                }

                var transaction = new AnnotationFileTransaction(path, record);
                if (record.Committed)
                {
                    transaction.CleanupCommitted();
                }
                else
                {
                    transaction.Rollback();
                    AppLog.COMM("Recovered interrupted annotation save: " + record.Id);
                }
            }

            public void Write(string temporaryPath, string targetPath)
            {
                if (entries.TryGetValue(targetPath, out _))
                {
                    ReplaceOrMove(temporaryPath, targetPath, backupPath: null);
                    return;
                }

                bool originalExists = File.Exists(targetPath);
                string backupPath = originalExists ? CreateBackupPath(targetPath, id) : string.Empty;
                var entry = new TransactionEntry(targetPath, backupPath, originalExists);
                entries.Add(targetPath, entry);
                order.Add(entry);
                PersistJournal(committed: false);
                ReplaceOrMove(temporaryPath, targetPath, originalExists ? backupPath : null);
            }

            public void Delete(string targetPath)
            {
                if (entries.ContainsKey(targetPath))
                {
                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }

                    return;
                }

                if (!File.Exists(targetPath))
                {
                    return;
                }

                string backupPath = CreateBackupPath(targetPath, id);
                var entry = new TransactionEntry(targetPath, backupPath, originalExists: true);
                entries.Add(targetPath, entry);
                order.Add(entry);
                PersistJournal(committed: false);
                File.Move(targetPath, backupPath);
            }

            public void Commit()
            {
                PersistJournal(committed: true);
                InjectTestFault(AnnotationFilePersistenceFaultPoint.AfterTransactionCommit, journalPath);
                CleanupCommitted();
            }

            private void CleanupCommitted()
            {
                foreach (TransactionEntry entry in order)
                {
                    TryDeleteBackup(entry.BackupPath);
                }

                if (order.All(entry => string.IsNullOrEmpty(entry.BackupPath) || !File.Exists(entry.BackupPath)))
                {
                    TryDeleteBackup(journalPath);
                }
            }

            public void Rollback()
            {
                var failures = new List<Exception>();
                for (int index = order.Count - 1; index >= 0; index--)
                {
                    TransactionEntry entry = order[index];
                    try
                    {
                        if (!entry.OriginalExists)
                        {
                            if (File.Exists(entry.TargetPath))
                            {
                                File.Delete(entry.TargetPath);
                            }

                            continue;
                        }

                        if (!File.Exists(entry.BackupPath))
                        {
                            if (!File.Exists(entry.TargetPath))
                            {
                                throw new InvalidDataException("Annotation recovery cannot find either original or backup: " + entry.TargetPath);
                            }

                            continue;
                        }

                        ReplaceOrMove(entry.BackupPath, entry.TargetPath, backupPath: null);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(ex);
                    }
                }

                if (failures.Count > 0)
                {
                    throw new AggregateException("One or more annotation files could not be rolled back.", failures);
                }

                if (journalPath != null && File.Exists(journalPath))
                {
                    File.Delete(journalPath);
                }
            }

            private void PersistJournal(bool committed)
            {
                if (journalPath == null || order.Count == 0)
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(journalPath));
                string payload = JsonConvert.SerializeObject(new JournalRecord { Id = id, Committed = committed, Entries = order });
                string json = JsonConvert.SerializeObject(new JournalEnvelope
                {
                    Payload = payload,
                    Sha256 = HashingService.ComputeUtf8TextSha256(payload, lowerCase: true)
                });
                string temporaryPath = journalPath + ".tmp";
                try
                {
                    File.WriteAllText(temporaryPath, json);
                    using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        stream.Flush(flushToDisk: true);
                    }

                    ReplaceOrMove(temporaryPath, journalPath, backupPath: null);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }

            private static string CreateBackupPath(string targetPath, string transactionId)
            {
                string directory = Path.GetDirectoryName(targetPath)
                    ?? throw new InvalidOperationException("Annotation path has no parent directory.");
                return Path.Combine(
                    directory,
                    $".{Path.GetFileName(targetPath)}.rollback-{transactionId}");
            }

            private static void TryDeleteBackup(string backupPath)
            {
                if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                {
                    return;
                }

                try
                {
                    File.Delete(backupPath);
                }
                catch (IOException error)
                {
                    AppLog.ABNORMAL($"Committed annotation cleanup deferred: {backupPath} / {error.Message}");
                }
                catch (UnauthorizedAccessException error)
                {
                    AppLog.ABNORMAL($"Committed annotation cleanup deferred: {backupPath} / {error.Message}");
                }
            }
        }

        private sealed class JournalEnvelope
        {
            public string Payload { get; set; }
            public string Sha256 { get; set; }
        }

        private sealed class JournalRecord
        {
            public int Version { get; set; } = 1;
            public string Id { get; set; }
            public bool Committed { get; set; }
            public List<TransactionEntry> Entries { get; set; }
        }

        private sealed class TransactionEntry
        {
            public TransactionEntry(string targetPath, string backupPath, bool originalExists)
            {
                TargetPath = targetPath;
                BackupPath = backupPath;
                OriginalExists = originalExists;
            }

            public string TargetPath { get; }
            public string BackupPath { get; }
            public bool OriginalExists { get; }
        }
    }
}
