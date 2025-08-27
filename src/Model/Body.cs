using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	public class Body
	{
		[JsonPropertyName("id64")]
		public long Id { get; set; }

		public int BodyId { get; set; }

		public string? Name { get; set; }

		public string? Type { get; set; }

		public string? SubType { get; set; }

		public double DistanceToArrival { get; set; }

		public bool MainStar { get; set; }

		public int Age { get; set; }

		public string? SpectralClass { get; set; }

		public string? Luminosity { get; set; }

		public double AbsoluteMagnitude { get; set; }

		public double SolarMasses { get; set; }

		public double SolarRadius { get; set; }

		public double SurfaceTemperature { get; set; }

		public double RotationalPeriod { get; set; }

		public double AxialTilt { get; set; }

		public List<Belt> Belts { get; set; } = [];

		public Dictionary<string, string> Timestamps { get; set; } = [];

		public List<Station> Stations { get; set; } = [];

		public required string UpdateTime { get; set; }

		public bool IsLandable { get; set; }

		public double Gravity { get; set; }

		public double EarthMasses { get; set; }

		public double Radius { get; set; }

		public double SurfacePressure { get; set; }

		public string? AtmosphereType { get; set; }

		public SolidComposition? SolidComposition { get; set; }

		public string? TerraformingState { get; set; }

		public Dictionary<string, float>? Materials { get; set; }

		public Signals? Signals { get; set; }

		public bool? RotationalPeriodTidallyLocked { get; set; }

		public List<Dictionary<string, int>> Parents { get; set; } = [];

		public double OrbitalPeriod { get; set; }

		public double SemiMajorAxis { get; set; }

		public double OrbitalEccentricity { get; set; }

		public double OrbitalInclination { get; set; }

		public double ArgOfPeriapsis { get; set; }

		public double MeanAnomaly { get; set; }

		public double AscendingNode { get; set; }

		public string? VolcanismType { get; set; }

		public Dictionary<string, float> AtmosphereComposition { get; set; } = [];

		public string? ReserveLevel { get; set; }

		public List<Ring> Rings { get; set; } = [];
	}






















}
