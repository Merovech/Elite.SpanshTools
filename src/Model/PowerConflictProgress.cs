namespace Elite.SpanshTools.Model
{
	/// <summary>Represents the conflict progress of a power in a star system.</summary>
	public class PowerConflictProgress
	{
		/// <summary>Name of the power. Required.</summary>
		public required string Power { get; set; }

		/// <summary>Progress of power in conflict.</summary>
		public double Progress { get; set; }
	}
}
