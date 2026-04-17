using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	/// <summary>Represents a star system in the Elite Dangerous galaxy.</summary>
	public class StarSystem
	{
		/// <summary>ID64 of the system, a 64 bit unsigned integer.</summary>
		[JsonPropertyName("id64")]
		public long Id { get; set; }

		/// <summary>Name of the system. Required.</summary>
		public required string Name { get; set; }

		/// <summary>Coordinates of the system. Required.</summary>
		public required Coords Coords { get; set; }

		/// <summary>Allegiance of the system.</summary>
		public string? Allegiance { get; set; }

		/// <summary>Government type of the system.</summary>
		public string? Government { get; set; }

		/// <summary>Primary economy for the system.</summary>
		public string? PrimaryEconomy { get; set; }

		/// <summary>Secondary economy for the system.</summary>
		public string? SecondaryEconomy { get; set; }

		/// <summary>Security rating of the system.</summary>
		public string? Security { get; set; }

		/// <summary>Population of the system.</summary>
		public long Population { get; set; }

		/// <summary>Total number of planets and stars in the system.</summary>
		public int BodyCount { get; set; }

		/// <summary>Controlling faction of the system.</summary>
		public Faction? ControllingFaction { get; set; }

		/// <summary>All factions with influence in the system. Defaults to an empty list.</summary>
		public List<Faction> Factions { get; set; } = [];

		/// <summary>Name of the controlling power.</summary>
		public string? ControllingPower { get; set; }

		/// <summary>State of the controlling power of the system.</summary>
		public string? PowerState { get; set; }

		/// <summary>Progress in controlling the system.</summary>
		public double PowerStateControlProgress { get; set; }

		/// <summary>Reinforcement progress of controlling power.</summary>
		public int PowerStateReinforcement { get; set; }

		/// <summary>Progress of power in losing the system.</summary>
		public int PowerStateUndermining { get; set; }

		/// <summary>Powers with influence in the system. Defaults to an empty list.</summary>
		public List<string> Powers { get; set; } = [];

		/// <summary>Conflict progress for powers in system. Defaults to an empty list.</summary>
		public List<PowerConflictProgress> PowerConflictProgress { get; set; } = [];

		/// <summary>Update timestamps for individual attributes. Defaults to an empty dictionary.</summary>
		public Dictionary<string, DateTime> Timestamps { get; set; } = [];

		/// <summary>Timestamp of the last update of the system, in UTC.</summary>
		public string? Date { get; set; }

		/// <summary>All bodies of the system. Defaults to an empty list.</summary>
		public List<Body> Bodies { get; set; } = [];

		/// <summary>All space based stations of the system. Defaults to an empty list.</summary>
		public List<Station> Stations { get; set; } = [];

		/// <summary>State of the Thargoid war in this system.</summary>
		[JsonPropertyName("thargoidWar")]
		public ThargoidWarState? ThargoidWarState { get; set; }
	}
}
