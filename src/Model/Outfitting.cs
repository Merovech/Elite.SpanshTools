namespace Elite.SpanshTools.Model
{
	/// <summary>Represents the outfitting services at a station.</summary>
	public class Outfitting
	{
		/// <summary>Outfitting modules available. Defaults to an empty list.</summary>
		public List<Module> Modules { get; set; } = [];

		/// <summary>Timestamp of outfitting update, in UTC. Required.</summary>
		public required string UpdateTime { get; set; }
	}
}
