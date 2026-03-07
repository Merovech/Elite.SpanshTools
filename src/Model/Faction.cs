namespace Elite.SpanshTools.Model
{
	public class Faction
	{
		public required string Name { get; set; }

		public string? State { get; set; }

		public string? Government { get; set; }

		public string? Allegiance { get; set; }

		public double? Influence { get; set; }

		public List<FactionState> ActiveStates { get; set; } = [];

		public List<FactionState> PendingStates { get; set; } = [];

		public List<FactionState> RecoveringStates { get; set; } = [];
	}
}
