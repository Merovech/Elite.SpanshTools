namespace Elite.SpanshTools.Model
{
	/// <summary>Represents a state that a faction is in, transitioning to, or recovering from.</summary>
	public class FactionState
	{
		/// <summary>Actual state. Required.</summary>
		public required string State { get; set; }

		/// <summary>Trend of the state.</summary>
		public float? Trend { get; set; }
	}
}
