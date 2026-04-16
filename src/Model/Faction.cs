namespace Elite.SpanshTools.Model
{
	/// <summary>Represents a minor faction with influence in a star system.</summary>
	public class Faction
	{
		/// <summary>Name of the faction. Required.</summary>
		public required string Name { get; set; }

		/// <summary>State of the faction.</summary>
		public string? State { get; set; }

		/// <summary>Government type of faction.</summary>
		public string? Government { get; set; }

		/// <summary>Allegiance of the faction.</summary>
		public string? Allegiance { get; set; }

		/// <summary>Influence of the faction.</summary>
		public double? Influence { get; set; }

		/// <summary>Currently active states. Defaults to an empty list.</summary>
		public List<FactionState> ActiveStates { get; set; } = [];

		/// <summary>Pending states for faction. Defaults to an empty list.</summary>
		public List<FactionState> PendingStates { get; set; } = [];

		/// <summary>Recovering states for faction. Defaults to an empty list.</summary>
		public List<FactionState> RecoveringStates { get; set; } = [];
	}
}
