namespace Elite.SpanshTools.Model
{
	/// <summary>Represents the commodity market at a station.</summary>
	public class Market
	{
		/// <summary>Commodities bought and sold. Defaults to an empty list.</summary>
		public List<Commodity> Commodities { get; set; } = [];

		/// <summary>Commodities that are illegal. Defaults to an empty list.</summary>
		public List<string> ProhibitedCommodities { get; set; } = [];

		/// <summary>Timestamp of marketplace update, in UTC. Required.</summary>
		public required string UpdateTime { get; set; }
	}
}
