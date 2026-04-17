namespace Elite.SpanshTools.Model
{
	/// <summary>Represents a ship module available at an outfitting station.</summary>
	public class Module
	{
		/// <summary>Name of the module. Required.</summary>
		public required string Name{get; set;}

		/// <summary>Frontier symbol identifier. Required.</summary>
		public required string Symbol{get; set;}

		/// <summary>Frontier numeric identifier.</summary>
		public int ModuleId{get; set;}

		/// <summary>Class of the module.</summary>
		public int Class{get; set;}

		/// <summary>Rating of the module. Required.</summary>
		public required string Rating{get; set;}

		/// <summary>Category of the module. Required.</summary>
		public required string Category{get; set;}

		/// <summary>Ship restrictions for the module.</summary>
		public string? Ship{get; set;}
	}
}
