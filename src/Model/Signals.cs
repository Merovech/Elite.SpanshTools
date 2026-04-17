using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	/// <summary>Represents signals detected on a planetary body.</summary>
	public class Signals
	{
		/// <summary>Signals mapping.</summary>
		/// <remarks>The JSON uses an inner "signals" object whose keys vary (Monazite, 
		/// Painite, $SAA_SignalType_*, etc.). This gets deserializes that inner object into a 
		/// dictionary so that we can accept any key names.</remarks>
		[JsonPropertyName("signals")]
		public Dictionary<string, int>? SignalsMap { get; set; }

		/// <summary>Genuses which can be found on planet. Defaults to an empty list.</summary>
		public List<object> Genuses { get; set; } = [];

		/// <summary>Timestamp signals last updated, in UTC.</summary>
		public string? UpdateTime { get; set; }
	}
}
