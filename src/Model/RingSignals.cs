using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	public class RingSignals
	{
		[JsonPropertyName("signals")]
		public Dictionary<string, string> Signals = [];

		public required string UpdateTime { get; set; }
	}
}
