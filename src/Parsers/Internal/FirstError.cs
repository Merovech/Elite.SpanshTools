using System.Runtime.ExceptionServices;

namespace Elite.SpanshTools.Parsers.Internal
{
	/// <summary>
	/// Collects failures from concurrent workers and rethrows the one that matters: the first real error, ahead of
	/// the cancellations it caused in the other workers.
	/// </summary>
	internal sealed class FirstError
	{
		private Exception? firstError;
		private Exception? firstCancellation;

		internal void Record(Exception exception)
		{
			if (exception is OperationCanceledException)
			{
				Interlocked.CompareExchange(ref firstCancellation, exception, null);
			}
			else
			{
				Interlocked.CompareExchange(ref firstError, exception, null);
			}
		}

		/// <summary>Throws the first real error; otherwise a cancellation, if there was one.</summary>
		internal void ThrowIfAny(CancellationToken callerToken)
		{
			if (firstError is not null)
			{
				ExceptionDispatchInfo.Capture(firstError).Throw();
			}

			callerToken.ThrowIfCancellationRequested();

			if (firstCancellation is not null)
			{
				ExceptionDispatchInfo.Capture(firstCancellation).Throw();
			}
		}
	}
}
