namespace Elite.SpanshTools.Model
{
	/// <summary>Represents a ship available for purchase at a shipyard.</summary>
	public class Ship
	{
		/// <summary>Ship name/type. Required.</summary>
		public required string Name { get; set; }

		/// <summary>Frontier symbol identifier. Required.</summary>
		public required string Symbol { get; set; }

		/// <summary>Frontier numeric identifier.</summary>
		public int ShipId { get; set; }
	}
}
