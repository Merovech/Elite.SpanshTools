using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	public class Signals
	{
		[JsonPropertyName("signals")]
		public required SignalsInternal SignalsInternal { get; set; }

		public List<object> Genuses { get; set; } = [];

		public required string UpdateTime { get; set; }
	}

	public class SignalsInternal
	{
		[JsonPropertyName("$SAA_SignalType_Human;")]
		public int SAA_SignalType_Human { get; set; }

		[JsonPropertyName("$SAA_SignalType_Other;")]
		public int? SAA_SignalType_Other { get; set; }

		[JsonPropertyName("$SAA_SignalType_Geological;")]
		public int? SAA_SignalType_Geological { get; set; }

		[JsonPropertyName("SAA_SignalType_Biological;")]
		public int? SAA_SignalType_Biological { get; set; }

		public int Opal { get; set; }


	}
}
