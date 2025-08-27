namespace Elite.SpanshTools.Model
{
	public class Shipyard
	{
		public List<Ship> Ships { get; set; } = [];

		public required string UpdateTime { get; set; }
	}
}
