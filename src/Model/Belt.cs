namespace Elite.SpanshTools.Model
{
	public class Belt
	{
		public required string Name { get; set; }

		public required string Type { get; set; }
		
		public double Mass{get; set;}
		
		public double InnerRadius{get; set;}
		
		public double OuterRadius{get; set;}
	}
}
