namespace Elite.SpanshTools.Model
{
	public class Market
	{
		public List<Commodity> Commodities { get; set; } = [];

		public List<string> ProhibitedCommodities { get; set; } = [];

		public required string UpdateTime { get; set; }
	}
}
