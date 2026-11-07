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
		private byte[] buffer;

		internal FileChunkReader(SafeFileHandle handle, long fileLength, int chunkSize)
		{
			this.handle = handle;
			this.fileLength = fileLength;
			this.chunkSize = chunkSize;

			// Read a little past the chunk so the last line usually completes without a second read.
			readAhead = Math.Clamp(chunkSize / 8, 4096, 1024 * 1024);
			buffer = ArrayPool<byte>.Shared.Rent(chunkSize + readAhead + 1);
		}

		/// <summary>Parses the lines owned by chunk number <paramref name="chunk"/> into <paramref name="output"/>.</summary>
		internal void ParseChunk(long chunk, List<StarSystem?> output, JsonSerializerOptions options, Func<ElementParseError, bool>? onError = null)
		{
			long start = chunk * chunkSize;
			long end = Math.Min(start + chunkSize, fileLength);

			// Start one byte early: if that byte is a newline, a line starts exactly at 'start'.
			long bufferStart = start == 0 ? 0 : start - 1;
			int count = Fill(bufferStart, 0, (int)Math.Min(end - bufferStart + readAhead, fileLength - bufferStart));

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
					// The line runs past what has been read; read more of it.
					int searched = count - position;
					count = ReadMore(bufferStart, count);
					newline = buffer.AsSpan(position + searched, count - position - searched).IndexOf((byte)'\n');
					if (newline >= 0)
					{
						newline += searched;
					}
				}

				int lineEnd = newline < 0 ? count : position + newline;
				LineDelimited.ParseLine(buffer.AsSpan(position, lineEnd - position), bufferStart + position, output, options, onError);
				if (newline < 0)
				{
					break;
				}

				position = lineEnd + 1;
			}
		}

		public void Dispose()
		{
			ArrayPool<byte>.Shared.Return(buffer);
			buffer = [];
		}

		private int ReadMore(long bufferStart, int count)
		{
			if (buffer.Length - count < readAhead)
			{
				var larger = ArrayPool<byte>.Shared.Rent(LineDelimited.NextBufferSize(buffer.Length, (long)count + readAhead));
				buffer.AsSpan(0, count).CopyTo(larger);
				ArrayPool<byte>.Shared.Return(buffer);
				buffer = larger;
			}

			int wanted = (int)Math.Min(buffer.Length - count, fileLength - (bufferStart + count));
			int read = Fill(bufferStart + count, count, wanted);
			if (read == 0)
			{
				throw new IOException("The file ended before its reported length; was it modified while being parsed?");
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

			return total;
		}
	}
}
