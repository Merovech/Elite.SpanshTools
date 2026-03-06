namespace Elite.SpanshTools.Model
{
	public class ControllingFaction
	{
		public required string Name { get; set; }

		public string? State { get; set; }

		public string? Government { get; set; }

		public string? Allegiance { get; set; }

		public List<FactionState> ActiveStates { get; set; } = [];

		public List<FactionState> PendingStates { get; set; } = [];
	}
}
