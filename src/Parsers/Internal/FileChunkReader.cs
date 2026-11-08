using System.Buffers;
using System.Text.Json;
using Elite.SpanshTools.Model;
using Microsoft.Win32.SafeHandles;

namespace Elite.SpanshTools.Parsers.Internal
{
	/// <summary>
	/// Reads and parses one fixed-size chunk of a line-delimited file at a time, by position, so that several
	/// readers can work on the same file at once.  A chunk owns every line that <em>starts</em> inside it, so each
	/// line is parsed exactly once however the chunk boundaries fall.  One instance per worker; not thread-safe.
	/// </summary>
	internal sealed class FileChunkReader : IDisposable
	{
		private readonly SafeFileHandle handle;
		private readonly long fileLength;
		private readonly int chunkSize;
		private readonly int readAhead;
		private readonly int maxLineLength;
		private byte[] buffer;

		internal FileChunkReader(SafeFileHandle handle, long fileLength, int chunkSize, int maxLineLength)
		{
			this.handle = handle;
			this.fileLength = fileLength;
			this.chunkSize = chunkSize;
			this.maxLineLength = maxLineLength;

			// Read a little past the chunk so the last line usually completes without a second read.
			readAhead = Math.Clamp(chunkSize / 8, 4096, 1024 * 1024);
			initialRequest = chunkSize + readAhead + 1;
			buffer = ArrayPool<byte>.Shared.Rent(initialRequest);
			initialLength = buffer.Length;
			PeakBufferLength = buffer.Length;
		}

		private readonly int initialRequest;
		private readonly int initialLength;

		/// <summary>Current buffer size, for tests.</summary>
		internal int BufferLength => buffer.Length;

		/// <summary>Largest buffer size so far, for tests.</summary>
		internal int PeakBufferLength { get; private set; }

		/// <summary>Bytes read from the file so far, for tests.</summary>
		internal long BytesRead { get; private set; }

		/// <summary>Parses the lines owned by chunk number <paramref name="chunk"/> into <paramref name="output"/>.</summary>
		/// <exception cref="InvalidDataException">A line is longer than the maximum line length.</exception>
		/// <exception cref="IOException">The file is shorter than its reported length.</exception>
		internal void ParseChunk(long chunk, List<StarSystem?> output, JsonSerializerOptions options, Func<ElementParseError, bool>? onError = null)
		{
			try
			{
				ParseLines(chunk, output, options, onError);
			}
			finally
			{
				// A long line grew the buffer; don't hold on to that for the rest of the parse.
				// Rent before returning: if renting fails, 'buffer' must still be ours for Dispose to return.
				if (buffer.Length > initialLength)
				{
					var smaller = ArrayPool<byte>.Shared.Rent(initialRequest);
					ArrayPool<byte>.Shared.Return(buffer);
					buffer = smaller;
				}
			}
		}

		private void ParseLines(long chunk, List<StarSystem?> output, JsonSerializerOptions options, Func<ElementParseError, bool>? onError)
		{
			long start = chunk * chunkSize;
			long end = Math.Min(start + chunkSize, fileLength);

			// Start one byte early: if that byte is a newline, a line starts exactly at 'start'.
			long bufferStart = start == 0 ? 0 : start - 1;
			int wanted = (int)Math.Min(end - bufferStart + readAhead, fileLength - bufferStart);
			int count = Fill(bufferStart, 0, wanted);
			if (count < wanted)
			{
				// Past this point the buffer holds stale bytes from earlier chunks; never scan them.
				throw Truncated();
			}

			int position = 0;
			if (start > 0)
			{
				int firstNewline = buffer.AsSpan(0, (int)(end - bufferStart)).IndexOf((byte)'\n');
				if (firstNewline < 0)
				{
					return; // A line from an earlier chunk covers this whole chunk.
				}

				position = firstNewline + 1;
			}

			while (bufferStart + position < end)
			{
				int newline = buffer.AsSpan(position, count - position).IndexOf((byte)'\n');
				while (newline < 0 && bufferStart + count < fileLength)
				{
					// The line runs past what has been read.  Stop before buffering more than the limit allows.
					if (count - position > maxLineLength)
					{
						throw LineDelimited.LineTooLong(bufferStart + position, maxLineLength);
					}

					int searched = count - position;
					count = ReadMore(bufferStart, count, position);
					newline = buffer.AsSpan(position + searched, count - position - searched).IndexOf((byte)'\n');
					if (newline >= 0)
					{
						newline += searched;
					}
				}

				int lineEnd = newline < 0 ? count : position + newline;
				LineDelimited.ParseLine(buffer.AsSpan(position, lineEnd - position), bufferStart + position, output, options, onError, maxLineLength);
				if (newline < 0)
				{
					break;
				}

				position = lineEnd + 1;
			}
		}

		private static IOException Truncated() => new("The file ended before its reported length; was it modified while being parsed?");

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(buffer);
			buffer = [];
		}

		/// <summary>Reads more of the line starting at buffer index <paramref name="lineStart"/>; returns the new count.</summary>
		private int ReadMore(long bufferStart, int count, int lineStart)
		{
			// Deciding whether the line is too long never needs more than one byte past the limit, so never read
			// (or grow the buffer) beyond that.
			long limit = Math.Min((long)lineStart + maxLineLength + 1, fileLength - bufferStart);
			long target = Math.Min((long)count + readAhead, limit);
			if (buffer.Length < target)
			{
				int size = (int)Math.Min(LineDelimited.NextBufferSize(buffer.Length, target), Math.Max(limit, target));
				var larger = ArrayPool<byte>.Shared.Rent(size);
				buffer.AsSpan(0, count).CopyTo(larger);
				ArrayPool<byte>.Shared.Return(buffer);
				buffer = larger;
				PeakBufferLength = Math.Max(PeakBufferLength, buffer.Length);
			}

			int wanted = (int)Math.Min(buffer.Length - count, limit - count);
			int read = Fill(bufferStart + count, count, wanted);
			if (read == 0)
			{
				throw Truncated();
			}

			return count + read;
		}

		private int Fill(long fileOffset, int bufferIndex, int bytes)
		{
			int total = 0;
			while (total < bytes)
			{
				int read = RandomAccess.Read(handle, buffer.AsSpan(bufferIndex + total, bytes - total), fileOffset + total);
				if (read <= 0)
				{
					break;
				}

				total += read;
			}

			BytesRead += total;
			return total;
		}
	}
}
