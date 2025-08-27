namespace Elite.SpanshTools.Model
{
	public class Station
	{
		public required string Name{get; set;}
		
		public long Id{get; set;}
		
		public required string UpdateTime{get; set;}
		
		public string? ControllingFaction{get; set;}
		
		public string? ControllingFactionState{get; set;}
		
		public double DistanceToArrival{get; set;}
		
		public string? PrimaryEconomy{get; set;}
		
		public Dictionary<string, float> Economies { get; set; } = [];
		
		public string? Allegiance{get; set;}
		
		public string? Government{get; set;}
		
		public List<string> Services { get; set; } = [];
		
		public string? Type{get; set;}
		
		public string? State{get; set;}
		
		public double Latitude{get; set;}
		
		public double Longitude{get; set;}
		
		public LandingPads? LandingPads{get; set;}
		
		public Market? Market{get; set;}
		
		public Shipyard? Shipyard{get; set;}
		
		public Outfitting? Outfitting{get; set;}
	}
}
