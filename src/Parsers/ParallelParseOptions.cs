namespace Elite.SpanshTools.Parsers
{
	/// <summary>Tuning options for the parallel parsing methods of <see cref="IParallelGalaxyParser"/>.</summary>
	/// <remarks>The defaults are hardware-neutral: they scale with the number of logical processors.</remarks>
	public sealed class ParallelParseOptions
	{
		/// <summary>Default size, in bytes, of each unit of work read from the input (8 MiB).</summary>
		public const int DefaultChunkSize = 8 * 1024 * 1024;

		/// <summary>Largest allowed <see cref="ChunkSize"/> (1 GiB).</summary>
		public const int MaxChunkSize = 1024 * 1024 * 1024;

		/// <summary>
		/// Maximum number of chunks parsed at the same time.  Defaults to <see cref="Environment.ProcessorCount"/>.
		/// </summary>
		public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

		/// <summary>
		/// Size, in bytes, of each unit of work read from the input.  Lines are never split: a chunk owns every
		/// line that starts inside it, however long.  Defaults to <see cref="DefaultChunkSize"/>.
		/// </summary>
		public int ChunkSize { get; set; } = DefaultChunkSize;

		/// <summary>
		/// When true, batches (and callbacks) are delivered in file order, and callbacks are invoked one at a time.
		/// When false (the default), batches arrive as they finish and callbacks run concurrently on worker threads.
		/// </summary>
		public bool PreserveOrder { get; set; }

		/// <summary>
		/// For the batch methods: the maximum number of parsed batches waiting for the consumer before the parser
		/// pauses.  Bounds memory use when the consumer is slower than the parser.  0 (the default) means twice
		/// <see cref="MaxDegreeOfParallelism"/>.
		/// </summary>
		public int MaxBufferedBatches { get; set; }

		/// <summary>
		/// Called when an array element can't be parsed (malformed JSON, a missing required member, or a property
		/// that the parser's <see cref="System.Text.Json.JsonSerializerOptions"/> reject, such as an unmapped one
		/// under <see cref="System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow"/>).  Return true to
		/// skip the element and keep parsing, or false to stop: the parse then fails with
		/// <see cref="ElementParseError.Exception"/>.  Null (the default) stops at the first bad element.
		/// </summary>
		/// <remarks>
		/// <para>Called from worker threads, possibly several at once and never in input order (even with
		/// <see cref="PreserveOrder"/>), so it must be thread-safe.  An exception it throws fails the parse.</para>
		/// <para>Skipped elements are not delivered.  Skipping is only possible for line-delimited input (the
		/// Spansh layout).  Other input is parsed sequentially, which can't resume after a bad element; there a bad
		/// element always fails the parse and this is not called.  Errors that aren't about one element (I/O
		/// errors, a truncated file, cancellation) also always fail the parse.</para>
		/// </remarks>
		public Func<ElementParseError, bool>? OnElementError { get; set; }

		/// <summary>Validates the options and returns an immutable snapshot of them.</summary>
		internal Snapshot Validate()
		{
			if (MaxDegreeOfParallelism < 1)
			{
				throw new ArgumentOutOfRangeException(nameof(MaxDegreeOfParallelism), MaxDegreeOfParallelism, "Must be at least 1.");
			}

			if (ChunkSize < 1 || ChunkSize > MaxChunkSize)
			{
				throw new ArgumentOutOfRangeException(nameof(ChunkSize), ChunkSize, $"Must be between 1 and {MaxChunkSize}.");
			}

			if (MaxBufferedBatches < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(MaxBufferedBatches), MaxBufferedBatches, "Must not be negative.");
			}

			int buffered = MaxBufferedBatches == 0 ? Math.Max(2, MaxDegreeOfParallelism * 2) : MaxBufferedBatches;
			return new Snapshot(MaxDegreeOfParallelism, ChunkSize, PreserveOrder, buffered, OnElementError);
		}

		internal readonly record struct Snapshot(int MaxDegreeOfParallelism, int ChunkSize, bool PreserveOrder, int MaxBufferedBatches, Func<ElementParseError, bool>? OnElementError);
	}
}
