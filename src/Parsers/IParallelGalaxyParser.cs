using Elite.SpanshTools.Model;

namespace Elite.SpanshTools.Parsers
{
	/// <summary>
	/// Parses a galaxy dump using every available core.  Separate from <see cref="IGalaxyParser"/> so that adding it
	/// does not break existing implementations of that interface.
	/// </summary>
	/// <remarks>
	/// <para>The fast path applies to input laid out like the Spansh dumps: '[' on the first line, then one array
	/// element per line (commas and surrounding whitespace allowed), then ']'.  Each element is parsed and validated
	/// on its own; the commas between elements are not.  Input with any other layout (minified, pretty-printed,
	/// several elements per line at the start) is detected and parsed sequentially instead, with the same results.</para>
	/// <para>Gzip-compressed input is detected by its content and decompressed on the fly.  Decompression is
	/// single-threaded, so it usually limits throughput for compressed input.</para>
	/// <para>Parsing allocates heavily.  For full throughput the host application should use server garbage
	/// collection with concurrent collection off (in the application's project file:
	/// <c>&lt;ServerGarbageCollector&gt;true&lt;/ServerGarbageCollector&gt;</c> and
	/// <c>&lt;ConcurrentGarbageCollection&gt;false&lt;/ConcurrentGarbageCollection&gt;</c>).  A library cannot set
	/// these on the application's behalf.</para>
	/// </remarks>
	public interface IParallelGalaxyParser
	{
		/// <summary>
		/// Parses a file, calling <paramref name="onSystem"/> for each array element.
		/// </summary>
		/// <param name="filename">The file to parse.  May be gzip-compressed.</param>
		/// <param name="onSystem">Called once per element (null for a JSON null element).  Unless
		/// <see cref="ParallelParseOptions.PreserveOrder"/> is set, it is called concurrently from several threads and
		/// must be thread-safe.</param>
		/// <param name="options">Tuning options; null for the defaults.</param>
		/// <param name="cancellationToken">Stops parsing; the returned task is then cancelled.</param>
		/// <returns>A task that completes when every element has been delivered.  It faults with the first error
		/// encountered, including an exception thrown by <paramref name="onSystem"/>.</returns>
		Task ParseFileParallelAsync(string filename, Action<StarSystem?> onSystem, ParallelParseOptions? options = null, CancellationToken cancellationToken = default);

		/// <summary>
		/// Parses a file, returning the elements in batches.  Batches arrive as they are parsed, or in file order
		/// when <see cref="ParallelParseOptions.PreserveOrder"/> is set.
		/// </summary>
		/// <param name="filename">The file to parse.  May be gzip-compressed.</param>
		/// <param name="options">Tuning options; null for the defaults.</param>
		/// <param name="cancellationToken">Stops parsing.</param>
		/// <returns>Non-empty batches of elements (null for a JSON null element).</returns>
		IAsyncEnumerable<IReadOnlyList<StarSystem?>> ParseFileInBatchesAsync(string filename, ParallelParseOptions? options = null, CancellationToken cancellationToken = default);

		/// <summary>
		/// Parses a stream, calling <paramref name="onSystem"/> for each array element.  Unlike
		/// <see cref="IGalaxyParser.ParseStreamAsync"/>, the stream does not need to be seekable, so network and
		/// decompression streams are accepted.  The caller is responsible for disposing the stream.
		/// </summary>
		/// <param name="inputStream">A readable stream, read from its current position.  May be gzip-compressed.</param>
		/// <param name="onSystem">See <see cref="ParseFileParallelAsync"/>.</param>
		/// <param name="options">Tuning options; null for the defaults.</param>
		/// <param name="cancellationToken">Stops parsing; the returned task is then cancelled.</param>
		/// <returns>See <see cref="ParseFileParallelAsync"/>.</returns>
		Task ParseStreamParallelAsync(Stream inputStream, Action<StarSystem?> onSystem, ParallelParseOptions? options = null, CancellationToken cancellationToken = default);

		/// <summary>
		/// Parses a stream, returning the elements in batches.  See <see cref="ParseStreamParallelAsync"/> for the
		/// stream requirements and <see cref="ParseFileInBatchesAsync"/> for the batch semantics.
		/// </summary>
		/// <param name="inputStream">A readable stream, read from its current position.  May be gzip-compressed.</param>
		/// <param name="options">Tuning options; null for the defaults.</param>
		/// <param name="cancellationToken">Stops parsing.</param>
		/// <returns>Non-empty batches of elements (null for a JSON null element).</returns>
		IAsyncEnumerable<IReadOnlyList<StarSystem?>> ParseStreamInBatchesAsync(Stream inputStream, ParallelParseOptions? options = null, CancellationToken cancellationToken = default);
	}
}
