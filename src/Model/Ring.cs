using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	public class Ring
	{
		public required string Name { get; set; }

		public required string Type { get; set; }

		public double Mass { get; set; }

		public double InnerRadius { get; set; }

		public double OuterRadius { get; set; }

		[JsonPropertyName("id64")]
		public long Id { get; set; }

		public RingSignals? Signals { get; set; }
	}
}
