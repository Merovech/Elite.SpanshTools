namespace Elite.SpanshTools.Model
{
	/// <summary>Represents the state of the Thargoid war in a star system.</summary>
	public class ThargoidWarState
	{
		/// <summary>Current Thargoid state in system.</summary>
		public string? CurrentState { get; set; }

		/// <summary>State if players win war.</summary>
		public string? SuccessState { get; set; }

		/// <summary>State if players lose war.</summary>
		public string? FailureState { get; set; }

		/// <summary>Progress of the war (0-1).</summary>
		public float Progress { get; set; }

		/// <summary>Ports remaining in war.</summary>
		public float PortsRemaining { get; set; }

		/// <summary>Have players won the war.</summary>
		public bool SuccessReached { get; set; }
	}
}
