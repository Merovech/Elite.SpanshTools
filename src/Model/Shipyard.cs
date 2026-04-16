namespace Elite.SpanshTools.Model
{
	/// <summary>Represents the shipyard at a station.</summary>
	public class Shipyard
	{
		/// <summary>Ships that can be bought. Defaults to an empty list.</summary>
		public List<Ship> Ships { get; set; } = [];

		/// <summary>Timestamp of shipyard update, in UTC. Required.</summary>
		public required string UpdateTime { get; set; }
	}
}
