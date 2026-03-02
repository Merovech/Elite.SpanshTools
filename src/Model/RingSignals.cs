using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
public class RingSignals
{
	[JsonPropertyName("signals")]
	public Dictionary<string, int>? Signals { get; set; }

	public string? UpdateTime { get; set; }
}
}
