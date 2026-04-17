namespace Elite.SpanshTools.Model
{
	/// <summary>Represents a commodity available at a station market.</summary>
	public class Commodity
	{
		/// <summary>Name of the commodity. Required.</summary>
		public required string Name { get; set; }

		/// <summary>Frontier symbol identifier. Required.</summary>
		public required string Symbol { get; set; }

		/// <summary>Category of the commodity. Required.</summary>
		public required string Category { get; set; }

		/// <summary>Frontier numeric identifier.</summary>
		public int CommodityId { get; set; }

		/// <summary>Demand for the commodity.</summary>
		public int Demand { get; set; }

		/// <summary>Supply for the commodity.</summary>
		public int Supply { get; set; }

		/// <summary>Price player can buy for.</summary>
		public int BuyPrice { get; set; }

		/// <summary>Price player can sell for.</summary>
		public int SellPrice { get; set; }
	}
}
