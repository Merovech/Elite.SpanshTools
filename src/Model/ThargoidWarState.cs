namespace Elite.SpanshTools.Model
{
	public class ThargoidWarState
	{
		public string? CurrentState { get; set; }
		public string? SuccessState { get; set; }
		public string? FailureState { get; set; }
		public float Progress { get; set; }
		public float PortsRemaining { get; set; }
		public bool SuccessReached { get; set; }
	}
}
