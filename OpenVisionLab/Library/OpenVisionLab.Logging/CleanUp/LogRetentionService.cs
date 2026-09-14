using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenVisionLab.Logging.Retention
{
	public sealed class LogRetentionService : IDisposable
	{
		private readonly string _logRootDir;
		private readonly int _retentionDays;
		private readonly Timer _timer;
		private readonly CancellationTokenSource _lifetimeCancellation = new CancellationTokenSource();
		private readonly object _sync = new object();
		private Task _cleanupTask = Task.CompletedTask;
		private bool _disposed;

		private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

		public LogRetentionService(string logRootDir, int retentionDays)
		{
			_logRootDir = logRootDir ?? throw new ArgumentNullException(nameof(logRootDir));
			_retentionDays = retentionDays;
			TimeSpan dueTime = GetInitialDueTime();
			_timer = new Timer(_ => RunCleanupSafe(), null, dueTime, OneDay);
			RunCleanupSafe();
		}

		private static TimeSpan GetInitialDueTime()
		{
			DateTime now = DateTime.Now;
			DateTime next = now.Date.AddDays(1).AddMinutes(1); 
			return next - now;
		}

		private void RunCleanupSafe()
		{
			lock (_sync)
			{
				if (_disposed || !_cleanupTask.IsCompleted)
				{
					return;
				}

				CancellationToken cancellationToken = _lifetimeCancellation.Token;
				_cleanupTask = Task.Run(() => RunCleanupCore(cancellationToken), cancellationToken);
			}
		}

		private void RunCleanupCore(CancellationToken cancellationToken)
		{
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				LogRetentionPruner.DeleteExpiredDateFolders(_logRootDir, _retentionDays);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch
			{
				// Retention is best-effort; the timer owner remains alive for the next run.
			}
		}

		public void Dispose()
		{
			Task cleanupTask;
			lock (_sync)
			{
				if (_disposed)
				{
					return;
				}

				_disposed = true;
				_timer.Dispose();
				_lifetimeCancellation.Cancel();
				cleanupTask = _cleanupTask;
			}

			try
			{
				cleanupTask.GetAwaiter().GetResult();
			}
			catch (OperationCanceledException)
			{
			}
			finally
			{
				_lifetimeCancellation.Dispose();
			}
		}
	}
}
