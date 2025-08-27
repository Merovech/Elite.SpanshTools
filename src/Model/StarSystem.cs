using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	public class StarSystem
	{
		[JsonPropertyName("id64")]
		public long Id { get; set; }

		public required string Name { get; set; }

		public required Coords Coords { get; set; }

		public string? Allegiance { get; set; }

		public string? Government { get; set; }

		public string? PrimaryEconomy { get; set; }

		public string? SecondaryEconomy { get; set; }

		public string? Security { get; set; }

		public long Population { get; set; }

		public int BodyCount { get; set; }

		public ControllingFaction? ControllingFaction { get; set; }

		public List<Faction> Factions { get; set; } = [];

		public string? ControllingPower { get; set; }

		public string? PowerState { get; set; }

		public double PowerStateControlProgress { get; set; }

		public int PowerStateReinforcement { get; set; }

		public int PowerStateUndermining { get; set; }

		public List<string> Powers { get; set; } = [];

		public Dictionary<string, DateTime> Timestamps { get; set; } = [];

		public required string Date { get; set; }

		public List<Body> Bodies { get; set; } = [];

		public List<Station> Stations { get; set; } = [];
	}
}
