using System.Buffers;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Elite.SpanshTools.Model;
using Elite.SpanshTools.Parsers.Internal;
using Microsoft.Win32.SafeHandles;

namespace Elite.SpanshTools.Parsers
{
	public partial class GalaxyParser : IParallelGalaxyParser
	{
		// Batch size used when the input isn't line-delimited and is parsed sequentially.
		private const int FallbackBatchSize = 1024;

		// Hands a parsed batch to the consumer, with its position in input order.
		private delegate void Deliver(long sequence, List<StarSystem?> batch);

		// Parses the whole input, calling 'deliver' once per batch.  When 'window' is not null, one slot must be
		// taken from it before each batch is produced; the consumer gives slots back as it takes batches.  That
		// bounds memory, and in ordered mode it also guarantees the next batch in order is always being produced.
		private delegate Task Pipeline(Deliver deliver, SemaphoreSlim? window, CancellationToken cancellationToken);

		/// <inheritdoc />
		public async Task ParseFileParallelAsync(string filename, Action<StarSystem?> onSystem, ParallelParseOptions? options = null, CancellationToken cancellationToken = default)
		{
			ValidateFilename(filename);
			ArgumentNullException.ThrowIfNull(onSystem);
			var settings = (options ?? new ParallelParseOptions()).Validate();

			await RunWithCallbackAsync((d, w, ct) => FilePipelineAsync(filename, settings, d, w, ct), settings, onSystem, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<IReadOnlyList<StarSystem?>> ParseFileInBatchesAsync(string filename, ParallelParseOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			ValidateFilename(filename);
			var settings = (options ?? new ParallelParseOptions()).Validate();

			await foreach (var batch in RunInBatchesAsync((d, w, ct) => FilePipelineAsync(filename, settings, d, w, ct), settings, cancellationToken).ConfigureAwait(false))
			{
				yield return batch;
			}
		}

		/// <inheritdoc />
		public async Task ParseStreamParallelAsync(Stream inputStream, Action<StarSystem?> onSystem, ParallelParseOptions? options = null, CancellationToken cancellationToken = default)
		{
			ValidateForwardOnlyStream(inputStream);
			ArgumentNullException.ThrowIfNull(onSystem);
			var settings = (options ?? new ParallelParseOptions()).Validate();

			await RunWithCallbackAsync((d, w, ct) => StreamPipelineAsync(inputStream, settings, d, w, ct), settings, onSystem, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public async IAsyncEnumerable<IReadOnlyList<StarSystem?>> ParseStreamInBatchesAsync(Stream inputStream, ParallelParseOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			ValidateForwardOnlyStream(inputStream);
			var settings = (options ?? new ParallelParseOptions()).Validate();

			await foreach (var batch in RunInBatchesAsync((d, w, ct) => StreamPipelineAsync(inputStream, settings, d, w, ct), settings, cancellationToken).ConfigureAwait(false))
			{
				yield return batch;
			}
		}

		private static void ValidateForwardOnlyStream(Stream inputStream)
		{
			ArgumentNullException.ThrowIfNull(inputStream);

			if (!inputStream.CanRead)
			{
				throw new ArgumentException("Stream is unreadable.");
			}

			// Matches ParseStreamAsync for the streams where emptiness can be checked up front.
			if (inputStream.CanSeek && inputStream.Length == 0)
			{
				throw new ArgumentException("Cannot parse an empty stream.");
			}
		}

		// ---- Consumers ---------------------------------------------------------------------------------------

		private static async Task RunWithCallbackAsync(Pipeline pipeline, ParallelParseOptions.Snapshot settings, Action<StarSystem?> onSystem, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (settings.PreserveOrder)
			{
				// In order means one at a time, on the consuming thread.
				await foreach (var batch in RunInBatchesAsync(pipeline, settings, cancellationToken).ConfigureAwait(false))
				{
					foreach (var system in batch)
					{
						onSystem(system);
					}
				}

				return;
			}

			// Unordered: call back directly from the worker that parsed the batch.
			await pipeline(
				(_, batch) =>
				{
					foreach (var system in batch)
					{
						onSystem(system);
					}
				},
				null,
				cancellationToken).ConfigureAwait(false);
		}

		private static async IAsyncEnumerable<IReadOnlyList<StarSystem?>> RunInBatchesAsync(Pipeline pipeline, ParallelParseOptions.Snapshot settings, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			using var window = new SemaphoreSlim(settings.MaxBufferedBatches, settings.MaxBufferedBatches);

			// Unbounded is safe: 'window' caps how many batches can be in the channel.
			var channel = Channel.CreateUnbounded<(long Sequence, List<StarSystem?> Batch)>(new UnboundedChannelOptions { SingleReader = true });
			var producer = Task.Run(async () =>
			{
				try
				{
					await pipeline((sequence, batch) => channel.Writer.TryWrite((sequence, batch)), window, cts.Token).ConfigureAwait(false);
				}
				finally
				{
					channel.Writer.TryComplete();
				}
			});

			var pending = settings.PreserveOrder ? new Dictionary<long, List<StarSystem?>>() : null;
			long nextInOrder = 0;
			bool drained = false;
			try
			{
				await foreach (var (sequence, batch) in channel.Reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
				{
					if (pending is null)
					{
						if (batch.Count > 0)
						{
							yield return batch;
						}

						window.Release();
						continue;
					}

					pending.Add(sequence, batch);
					while (pending.Remove(nextInOrder, out var ready))
					{
						nextInOrder++;
						if (ready.Count > 0)
						{
							yield return ready;
						}

						window.Release();
					}
				}

				drained = true;
			}
			finally
			{
				if (!drained)
				{
					// The consumer stopped early or failed: stop the workers and wait for them to let go of the input.
					cts.Cancel();
				}

				try
				{
					await producer.ConfigureAwait(false);
				}
				catch when (!drained)
				{
					// The consumer is already leaving with its own outcome; the workers' cancellation doesn't matter.
				}
			}
		}

		// ---- Pipelines ---------------------------------------------------------------------------------------

		private async Task FilePipelineAsync(string filename, ParallelParseOptions.Snapshot settings, Deliver deliver, SemaphoreSlim? window, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			using (SafeFileHandle handle = File.OpenHandle(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				long length = RandomAccess.GetLength(handle);
				if (DetectFileLayout(handle, length) == Layout.LineDelimited)
				{
					await FileLinesPipelineAsync(handle, length, settings, deliver, window, cancellationToken).ConfigureAwait(false);
					return;
				}
			}

			// Compressed, or not line-delimited: read it as a stream, which handles both.
			var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
			await using (fileStream.ConfigureAwait(false))
			{
				await StreamPipelineAsync(fileStream, settings, deliver, window, cancellationToken).ConfigureAwait(false);
			}
		}

		private static Layout DetectFileLayout(SafeFileHandle handle, long length)
		{
			int size = (int)Math.Min(length, 64 * 1024);
			while (true)
			{
				byte[] head = ArrayPool<byte>.Shared.Rent(Math.Max(size, 1));
				try
				{
					int count = 0;
					while (count < size)
					{
						int read = RandomAccess.Read(handle, head.AsSpan(count, size - count), count);
						if (read <= 0)
						{
							break;
						}

						count += read;
					}

					if (LineDelimited.IsGzip(head.AsSpan(0, count)))
					{
						return Layout.Other;
					}

					var layout = LineDelimited.Detect(head.AsSpan(0, count), isComplete: count >= length);
					if (layout != Layout.NeedMoreData)
					{
						return layout;
					}

					if (size >= LineDelimited.MaxDetectionBytes)
					{
						return Layout.Other;
					}

					size = (int)Math.Min(Math.Min((long)size * 4, LineDelimited.MaxDetectionBytes), length);
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(head);
				}
			}
		}

		// Each worker claims the next chunk number and reads and parses it by position, so reads and parsing
		// both run in parallel and the number of reads in flight scales with the number of workers.
		private async Task FileLinesPipelineAsync(SafeFileHandle handle, long length, ParallelParseOptions.Snapshot settings, Deliver deliver, SemaphoreSlim? window, CancellationToken cancellationToken)
		{
			long chunkCount = (length + settings.ChunkSize - 1) / settings.ChunkSize;
			long nextChunk = -1;
			var errors = new FirstError();
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			var workers = new Task[(int)Math.Min(settings.MaxDegreeOfParallelism, chunkCount)];
			for (int i = 0; i < workers.Length; i++)
			{
				workers[i] = Task.Factory.StartNew(
					() =>
					{
						using var reader = new FileChunkReader(handle, length, settings.ChunkSize);
						try
						{
							while (true)
							{
								cts.Token.ThrowIfCancellationRequested();
								window?.Wait(cts.Token);

								long chunk = Interlocked.Increment(ref nextChunk);
								if (chunk >= chunkCount)
								{
									window?.Release();
									return;
								}

								var batch = new List<StarSystem?>();
								reader.ParseChunk(chunk, batch, serializerOptions, settings.OnElementError);
								deliver(chunk, batch);
							}
						}
						catch (Exception ex)
						{
							errors.Record(ex);
							cts.Cancel();
						}
					},
					CancellationToken.None,
					TaskCreationOptions.LongRunning,
					TaskScheduler.Default);
			}

			await Task.WhenAll(workers).ConfigureAwait(false);
			errors.ThrowIfAny(cancellationToken);
		}

		private async Task StreamPipelineAsync(Stream stream, ParallelParseOptions.Snapshot settings, Deliver deliver, SemaphoreSlim? window, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			byte[]? buffer = ArrayPool<byte>.Shared.Rent(Math.Max(settings.ChunkSize, 4096));
			try
			{
				int count = 0;
				bool endOfStream = false;

				// Enough to recognize gzip.
				while (count < 2 && !endOfStream)
				{
					int read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken).ConfigureAwait(false);
					endOfStream = read == 0;
					count += read;
				}

				if (LineDelimited.IsGzip(buffer.AsSpan(0, count)))
				{
					var replay = new PrefixedStream(buffer.AsSpan(0, count).ToArray(), stream);
					ArrayPool<byte>.Shared.Return(buffer);
					buffer = null;

					var gzip = new GZipStream(replay, CompressionMode.Decompress);
					await using (gzip.ConfigureAwait(false))
					{
						await StreamPipelineAsync(gzip, settings, deliver, window, cancellationToken).ConfigureAwait(false);
					}

					return;
				}

				Layout layout;
				while ((layout = LineDelimited.Detect(buffer.AsSpan(0, count), endOfStream)) == Layout.NeedMoreData)
				{
					if (count >= LineDelimited.MaxDetectionBytes)
					{
						layout = Layout.Other;
						break;
					}

					if (count == buffer.Length)
					{
						buffer = Grow(buffer, count, buffer.Length * 2);
					}

					int read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken).ConfigureAwait(false);
					endOfStream = read == 0;
					count += read;
				}

				if (layout != Layout.LineDelimited)
				{
					using var replay = new PrefixedStream(buffer.AsSpan(0, count).ToArray(), stream);
					ArrayPool<byte>.Shared.Return(buffer);
					buffer = null;
					await SequentialPipelineAsync(replay, deliver, window, cancellationToken).ConfigureAwait(false);
					return;
				}

				var firstBuffer = buffer;
				buffer = null;
				await StreamLinesPipelineAsync(stream, firstBuffer, count, endOfStream, settings, deliver, window, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				if (buffer is not null)
				{
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}
		}

		// One reader cuts the stream into line-aligned segments (it has to: the stream can only be read in order),
		// and a pool of workers parses them.
		private async Task StreamLinesPipelineAsync(Stream stream, byte[] buffer, int count, bool endOfStream, ParallelParseOptions.Snapshot settings, Deliver deliver, SemaphoreSlim? window, CancellationToken cancellationToken)
		{
			var errors = new FirstError();
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var segments = Channel.CreateBounded<Segment>(new BoundedChannelOptions(Math.Max(2, settings.MaxDegreeOfParallelism * 2)) { SingleWriter = true });

			var workers = new Task[settings.MaxDegreeOfParallelism];
			for (int i = 0; i < workers.Length; i++)
			{
				workers[i] = Task.Run(async () =>
				{
					try
					{
						await foreach (var segment in segments.Reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
						{
							try
							{
								var batch = new List<StarSystem?>();
								LineDelimited.ParseRegion(segment.Buffer.AsSpan(0, segment.Count), segment.Offset, batch, serializerOptions, settings.OnElementError);
								deliver(segment.Sequence, batch);
							}
							finally
							{
								ArrayPool<byte>.Shared.Return(segment.Buffer);
							}
						}
					}
					catch (Exception ex)
					{
						errors.Record(ex);
						cts.Cancel();
					}
				});
			}

			try
			{
				long sequence = 0;
				long offset = 0;
				while (true)
				{
					while (!endOfStream && count < buffer.Length)
					{
						int read = await stream.ReadAsync(buffer.AsMemory(count), cts.Token).ConfigureAwait(false);
						endOfStream = read == 0;
						count += read;
					}

					// Hand over everything up to the last complete line; keep the partial line for the next segment.
					int cut = endOfStream ? count : buffer.AsSpan(0, count).LastIndexOf((byte)'\n') + 1;
					if (cut == 0)
					{
						if (endOfStream)
						{
							break;
						}

						buffer = Grow(buffer, count, buffer.Length * 2); // One line is longer than the buffer.
						continue;
					}

					var next = ArrayPool<byte>.Shared.Rent(Math.Max(settings.ChunkSize, (count - cut) * 2));
					buffer.AsSpan(cut, count - cut).CopyTo(next);

					if (window is not null)
					{
						await window.WaitAsync(cts.Token).ConfigureAwait(false);
					}

					await segments.Writer.WriteAsync(new Segment(sequence++, buffer, cut, offset), cts.Token).ConfigureAwait(false);
					offset += cut;
					count -= cut;
					buffer = next;

					if (endOfStream && count == 0)
					{
						break;
					}
				}
			}
			catch (Exception ex)
			{
				errors.Record(ex);
				cts.Cancel();
			}
			finally
			{
				segments.Writer.TryComplete();
				ArrayPool<byte>.Shared.Return(buffer);
			}

			await Task.WhenAll(workers).ConfigureAwait(false);
			errors.ThrowIfAny(cancellationToken);
		}

		private async Task SequentialPipelineAsync(Stream stream, Deliver deliver, SemaphoreSlim? window, CancellationToken cancellationToken)
		{
			long sequence = 0;
			var batch = new List<StarSystem?>(FallbackBatchSize);
			await foreach (var system in JsonSerializer.DeserializeAsyncEnumerable<StarSystem>(stream, serializerOptions, cancellationToken).ConfigureAwait(false))
			{
				batch.Add(system);
				if (batch.Count == FallbackBatchSize)
				{
					if (window is not null)
					{
						await window.WaitAsync(cancellationToken).ConfigureAwait(false);
					}

					deliver(sequence++, batch);
					batch = new List<StarSystem?>(FallbackBatchSize);
				}
			}

			if (batch.Count > 0)
			{
				if (window is not null)
				{
					await window.WaitAsync(cancellationToken).ConfigureAwait(false);
				}

				deliver(sequence, batch);
			}
		}

		private static byte[] Grow(byte[] buffer, int count, int newSize)
		{
			var larger = ArrayPool<byte>.Shared.Rent(LineDelimited.NextBufferSize(buffer.Length, newSize));
			buffer.AsSpan(0, count).CopyTo(larger);
			ArrayPool<byte>.Shared.Return(buffer);
			return larger;
		}

		private readonly record struct Segment(long Sequence, byte[] Buffer, int Count, long Offset);
	}
}
