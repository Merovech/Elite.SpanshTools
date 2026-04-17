using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	/// <summary>Represents an asteroid belt associated with a body.</summary>
	public class Belt
	{
		/// <summary>Name of the belt or ring. Required.</summary>
		public required string Name { get; set; }

		/// <summary>Type of the ring or belt. Required.</summary>
		public required string Type { get; set; }

		/// <summary>ID64 of the belt or ring.</summary>
		[JsonPropertyName("id64")]
		public long Id { get; set; }

		/// <summary>Mass of the belt or ring, in Megatonnes.</summary>
		public double Mass{get; set;}

		/// <summary>Inner radius of belt or ring, in Kilometres.</summary>
		public double InnerRadius{get; set;}

		/// <summary>Outer radius of belt or ring, in Kilometres.</summary>
		public double OuterRadius{get; set;}

		/// <summary>The signals that can be found within.</summary>
		public Signals? Signals { get; set; }
	}
}
