using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	public class Signals
	{
		// The JSON uses an inner "signals" object whose keys vary (Monazite, Painite,
		// or $SAA_SignalType_*). Deserialize that inner object into a dictionary so
		// we accept any key names.
		[JsonPropertyName("signals")]
		public Dictionary<string, int>? SignalsMap { get; set; }

		public List<object> Genuses { get; set; } = [];

		public string? UpdateTime { get; set; }
	}
}
