namespace Elite.SpanshTools.Model
{
	public class Module
	{
		public required string Name{get; set;}
		
		public required string Symbol{get; set;}
		
		public int ModuleId{get; set;}
		
		public int Class{get; set;}
		
		public required string Rating{get; set;}
		
		public required string Category{get; set;}
		
		public string? Ship{get; set;}
	}
}
