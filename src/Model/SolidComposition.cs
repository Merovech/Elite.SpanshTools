namespace Elite.SpanshTools.Model
{
	/// <summary>Represents the solid composition of a planetary body.</summary>
	public class SolidComposition
	{
		/// <summary>Proportion of ice in the solid composition.</summary>
		public double Ice { get; set; }

		/// <summary>Proportion of metal in the solid composition.</summary>
		public double Metal { get; set; }

		/// <summary>Proportion of rock in the solid composition.</summary>
		public double Rock { get; set; }
	}
}
