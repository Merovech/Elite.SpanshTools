namespace Elite.SpanshTools.Model
{
	public class Commodity
	{
		public required string Name { get; set; }

		public required string Symbol { get; set; }

		public required string Category { get; set; }

		public int CommodityId { get; set; }

		public int Demand { get; set; }

		public int Supply { get; set; }

		public int BuyPrice { get; set; }

		public int SellPrice { get; set; }
	}
}
