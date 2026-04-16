namespace Elite.SpanshTools.Model
{
	/// <summary>Represents a space station or surface port in the galaxy.</summary>
	public class Station
	{
		/// <summary>Name of the station. Required.</summary>
		public required string Name { get; set; }

		/// <summary>Real name for colonisation stations.</summary>
		public string? RealName { get; set; }

		/// <summary>Market ID of the station.</summary>
		public long Id { get; set; }

		/// <summary>Timestamp of last station update, in UTC. Required.</summary>
		public required string UpdateTime { get; set; }

		/// <summary>Player given name for fleet carriers.</summary>
		public string? CarrierName { get; set; }

		/// <summary>Carrier access control.</summary>
		public string? CarrierDockingAccess { get; set; }

		/// <summary>Controlling faction of the station.</summary>
		public string? ControllingFaction { get; set; }

		/// <summary>State of the controlling faction.</summary>
		public string? ControllingFactionState { get; set; }

		/// <summary>Distance to arrival point, in Kilometres.</summary>
		public double DistanceToArrival { get; set; }

		/// <summary>Primary economy for the station.</summary>
		public string? PrimaryEconomy { get; set; }

		/// <summary>All economies as proportion of station. Defaults to an empty dictionary.</summary>
		public Dictionary<string, float> Economies { get; set; } = [];

		/// <summary>Allegiance of the station.</summary>
		public string? Allegiance { get; set; }

		/// <summary>Government type of the station.</summary>
		public string? Government { get; set; }

		/// <summary>Services provided by the station. Defaults to an empty list.</summary>
		public List<string> Services { get; set; } = [];

		/// <summary>Type of the station.</summary>
		public string? Type { get; set; }

		/// <summary>State of the station.</summary>
		public string? State { get; set; }

		/// <summary>Latitude of planetary station.</summary>
		public double Latitude { get; set; }

		/// <summary>Longitude of planetary station.</summary>
		public double Longitude { get; set; }

		/// <summary>Breakdown of landing pad numbers.</summary>
		public LandingPads? LandingPads { get; set; }

		/// <summary>Commodity market information.</summary>
		public Market? Market { get; set; }

		/// <summary>Shipyard information.</summary>
		public Shipyard? Shipyard { get; set; }

		/// <summary>Outfitting information.</summary>
		public Outfitting? Outfitting { get; set; }
	}
}
