using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	/// <summary>Represents signals detected within a planetary ring.</summary>
	public class RingSignals
	{
		/// <summary>Signals mapping.</summary>
		[JsonPropertyName("signals")]
		public Dictionary<string, int>? Signals { get; set; }

		/// <summary>Timestamp signals last updated, in UTC.</summary>
		public string? UpdateTime { get; set; }
	}
}
