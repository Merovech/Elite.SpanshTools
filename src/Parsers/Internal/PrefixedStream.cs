namespace Elite.SpanshTools.Parsers.Internal
{
	/// <summary>
	/// A read-only, forward-only stream that replays bytes already read from <paramref name="inner"/> and then
	/// continues with it.  Used after peeking at a stream that can't seek back.  Disposing it does not dispose
	/// <paramref name="inner"/>.
	/// </summary>
	internal sealed class PrefixedStream(byte[] prefix, Stream inner) : Stream
	{
		private int prefixPosition;

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

		public override int Read(Span<byte> buffer)
		{
			int fromPrefix = CopyFromPrefix(buffer);
			return fromPrefix > 0 ? fromPrefix : inner.Read(buffer);
		}

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
			ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			int fromPrefix = CopyFromPrefix(buffer.Span);
			return fromPrefix > 0 ? new ValueTask<int>(fromPrefix) : inner.ReadAsync(buffer, cancellationToken);
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		private int CopyFromPrefix(Span<byte> destination)
		{
			int count = Math.Min(destination.Length, prefix.Length - prefixPosition);
			if (count <= 0)
			{
				return 0;
			}

			prefix.AsSpan(prefixPosition, count).CopyTo(destination);
			prefixPosition += count;
			return count;
		}
	}
}
