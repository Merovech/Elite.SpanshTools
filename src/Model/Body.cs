using System.Text.Json.Serialization;

namespace Elite.SpanshTools.Model
{
	/// <summary>Represents a star or planet body within a star system.</summary>
	public class Body
	{
		/// <summary>The ID64 of the body, a 64 bit unsigned integer.</summary>
		[JsonPropertyName("id64")]
		public long Id { get; set; }

		/// <summary>The numeric ID of the body within the system.</summary>
		public int BodyId { get; set; }

		/// <summary>The name of the body.</summary>
		public string? Name { get; set; }

		/// <summary>The type of the body.</summary>
		public string? Type { get; set; }

		/// <summary>Subtype of the body.</summary>
		public string? SubType { get; set; }

		/// <summary>Distance to system arrival point, in Kilometres.</summary>
		public double DistanceToArrival { get; set; }

		/// <summary>Whether this is the main star of the system.</summary>
		public bool MainStar { get; set; }

		/// <summary>The age of the star, in solar years.</summary>
		public int Age { get; set; }

		/// <summary>Spectral class of the star.</summary>
		public string? SpectralClass { get; set; }

		/// <summary>Luminosity of the star.</summary>
		public string? Luminosity { get; set; }

		/// <summary>Absolute magnitude of the star.</summary>
		public double AbsoluteMagnitude { get; set; }

		/// <summary>Mass of the star as ratio of Sol's mass.</summary>
		public double SolarMasses { get; set; }

		/// <summary>Radius of the star as ratio of Sol's radius.</summary>
		public double SolarRadius { get; set; }

		/// <summary>Surface temperature of the body, in Kelvin.</summary>
		public double SurfaceTemperature { get; set; }

		/// <summary>Rotational period of the body, in Days.</summary>
		public double RotationalPeriod { get; set; }

		/// <summary>Axial tilt of the body, in radians.</summary>
		public double AxialTilt { get; set; }

		/// <summary>Belts associated with the body. Defaults to an empty list.</summary>
		public List<Belt> Belts { get; set; } = [];

		/// <summary>Timestamps of the last update of some fields. Defaults to an empty dictionary.</summary>
		public Dictionary<string, string> Timestamps { get; set; } = [];

		/// <summary>Stations located on the body. Defaults to an empty list.</summary>
		public List<Station> Stations { get; set; } = [];

		/// <summary>Timestamp of the last update of the body, in UTC. Required.</summary>
		public required string UpdateTime { get; set; }

		/// <summary>Whether the planet is landable.</summary>
		public bool IsLandable { get; set; }

		/// <summary>Gravity of the planet as ratio of Earth.</summary>
		public double Gravity { get; set; }

		/// <summary>Mass of planet as ratio of Earth's mass.</summary>
		public double EarthMasses { get; set; }

		/// <summary>Radius of the planet, in Kilometres.</summary>
		public double Radius { get; set; }

		/// <summary>Surface pressure of planet, in atmospheres.</summary>
		public double SurfacePressure { get; set; }

		/// <summary>Atmosphere type of the planet.</summary>
		public string? AtmosphereType { get; set; }

		/// <summary>Solid composition mapping elements.</summary>
		public SolidComposition? SolidComposition { get; set; }

		/// <summary>Terraforming state of the planet.</summary>
		public string? TerraformingState { get; set; }

		/// <summary>Materials found on planet by percentage.</summary>
		public Dictionary<string, float>? Materials { get; set; }

		/// <summary>The signals that can be found on planet.</summary>
		public Signals? Signals { get; set; }

		/// <summary>Whether rotation is locked to tides.</summary>
		public bool? RotationalPeriodTidallyLocked { get; set; }

		/// <summary>Parents of the body, mapping parent type to ID. Defaults to an empty list.</summary>
		public List<Dictionary<string, int>> Parents { get; set; } = [];

		/// <summary>The orbital period of the body, measured in days.</summary>
		public double OrbitalPeriod { get; set; }

		/// <summary>The semi major axis of the body, in Kilometers.</summary>
		public double SemiMajorAxis { get; set; }

		/// <summary>The orbital eccentricity of the body.</summary>
		public double OrbitalEccentricity { get; set; }

		/// <summary>The orbital inclination of the body, in degrees.</summary>
		public double OrbitalInclination { get; set; }

		/// <summary>The argument of periapsis the body, in degrees.</summary>
		public double ArgOfPeriapsis { get; set; }

		/// <summary>The mean of anomaly the body, in degrees.</summary>
		public double MeanAnomaly { get; set; }

		/// <summary>The ascending node of the body, in degrees.</summary>
		public double AscendingNode { get; set; }

		/// <summary>Volcanism type of the planet.</summary>
		public string? VolcanismType { get; set; }

		/// <summary>Atmosphere composition mapping elements. Defaults to an empty dictionary.</summary>
		public Dictionary<string, float> AtmosphereComposition { get; set; } = [];

		/// <summary>Reserve level of the planet.</summary>
		public string? ReserveLevel { get; set; }

		/// <summary>Rings associated with the body. Defaults to an empty list.</summary>
		public List<Ring> Rings { get; set; } = [];
	}






















}
