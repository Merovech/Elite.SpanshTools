using System.Text.Json;
using Elite.SpanshTools.Model;

namespace Elite.SpanshTools.Tests
{
	/// <summary>
	/// Verifies that parsed JSON ends up in the right model properties.  The pre-existing parser tests only
	/// check that parsing doesn't throw (or counts systems); these check the data itself.
	/// </summary>
	[TestClass]
	public class ModelDeserializationTests
	{
		private static async Task<StarSystem> ParseFullAsync() => await TestHelpers.ParseSingleAsync(FullSystemJson.Value);

		// ---- StarSystem -------------------------------------------------------------------------------------

		[TestMethod]
		public async Task System_Scalar_Properties_Map_Correctly()
		{
			var s = await ParseFullAsync();

			Assert.AreEqual(164098653L, s.Id);
			Assert.AreEqual("Achenar", s.Name);
			Assert.AreEqual("Empire", s.Allegiance);
			Assert.AreEqual("Patronage", s.Government);
			Assert.AreEqual("Refinery", s.PrimaryEconomy);
			Assert.AreEqual("Service", s.SecondaryEconomy);
			Assert.AreEqual("High", s.Security);
			Assert.AreEqual(15, s.BodyCount);
			Assert.AreEqual("A. Lavigny-Duval", s.ControllingPower);
			Assert.AreEqual("Stronghold", s.PowerState);
			Assert.AreEqual(0.810922, s.PowerStateControlProgress, 1e-9);
			Assert.AreEqual(23922, s.PowerStateReinforcement);
			Assert.AreEqual(5, s.PowerStateUndermining);
			Assert.AreEqual("2025-06-24 21:40:39+00", s.Date);
		}

		[TestMethod]
		public async Task System_Population_Exceeding_Int32_Is_Preserved()
		{
			var s = await ParseFullAsync();
			Assert.AreEqual(16380054761L, s.Population);
		}

		[TestMethod]
		public async Task System_Id_And_Population_Accept_Int64_Maximum()
		{
			var json = TestHelpers.ArrayOf(
				$"{{\"id64\":{long.MaxValue},\"name\":\"Big\",\"population\":{long.MaxValue},\"coords\":{{\"x\":0,\"y\":0,\"z\":0}}}}");
			var s = await TestHelpers.ParseSingleAsync(json);

			Assert.AreEqual(long.MaxValue, s.Id);
			Assert.AreEqual(long.MaxValue, s.Population);
		}

		[TestMethod]
		public async Task System_Coords_Map_Correctly()
		{
			var s = await ParseFullAsync();

			Assert.AreEqual(67.5, s.Coords.X, 1e-9);
			Assert.AreEqual(-119.46875, s.Coords.Y, 1e-9);
			Assert.AreEqual(24.84375, s.Coords.Z, 1e-9);
		}

		[TestMethod]
		public async Task System_ControllingFaction_Maps_And_Has_Empty_State_Lists()
		{
			var s = await ParseFullAsync();

			Assert.IsNotNull(s.ControllingFaction);
			Assert.AreEqual("Achenar Empire League", s.ControllingFaction.Name);
			Assert.AreEqual("Patronage", s.ControllingFaction.Government);
			Assert.AreEqual("Empire", s.ControllingFaction.Allegiance);
			Assert.IsNull(s.ControllingFaction.Influence);
			Assert.IsNull(s.ControllingFaction.State);
			Assert.AreEqual(0, s.ControllingFaction.ActiveStates.Count);
			Assert.AreEqual(0, s.ControllingFaction.PendingStates.Count);
			Assert.AreEqual(0, s.ControllingFaction.RecoveringStates.Count);
		}

		[TestMethod]
		public async Task System_Factions_Map_With_States_And_Trends()
		{
			var s = await ParseFullAsync();

			Assert.AreEqual(2, s.Factions.Count);

			var f = s.Factions[0];
			Assert.AreEqual("Achenar Comms Co", f.Name);
			Assert.AreEqual("Empire", f.Allegiance);
			Assert.AreEqual("Patronage", f.Government);
			Assert.AreEqual("Infrastructure Failure", f.State);
			Assert.AreEqual(0.064286, f.Influence!.Value, 1e-9);

			Assert.AreEqual(2, f.ActiveStates.Count);
			Assert.AreEqual("Boom", f.ActiveStates[0].State);
			Assert.AreEqual(1.5f, f.ActiveStates[0].Trend);
			Assert.AreEqual("Expansion", f.ActiveStates[1].State);
			Assert.IsNull(f.ActiveStates[1].Trend, "A state with no trend must have a null (not zero) trend.");

			Assert.AreEqual(1, f.PendingStates.Count);
			Assert.AreEqual("War", f.PendingStates[0].State);
			Assert.AreEqual(0f, f.PendingStates[0].Trend, "An explicit zero trend must stay distinct from a missing trend.");

			Assert.AreEqual(1, f.RecoveringStates.Count);
			Assert.AreEqual("Bust", f.RecoveringStates[0].State);
			Assert.AreEqual(-1f, f.RecoveringStates[0].Trend);
		}

		[TestMethod]
		public async Task Faction_Explicit_Null_Influence_Is_Null()
		{
			var s = await ParseFullAsync();
			Assert.AreEqual("Minor Faction", s.Factions[1].Name);
			Assert.IsNull(s.Factions[1].Influence);
		}

		[TestMethod]
		public async Task System_Powers_And_ConflictProgress_Map_In_Order()
		{
			var s = await ParseFullAsync();

			CollectionAssert.AreEqual(new[] { "A. Lavigny-Duval", "Aisling Duval", "Denton Patreus" }, s.Powers);

			Assert.AreEqual(2, s.PowerConflictProgress.Count);
			Assert.AreEqual("Aisling Duval", s.PowerConflictProgress[0].Power);
			Assert.AreEqual(0.25, s.PowerConflictProgress[0].Progress, 1e-9);
			Assert.AreEqual("Denton Patreus", s.PowerConflictProgress[1].Power);
			Assert.AreEqual(0.125, s.PowerConflictProgress[1].Progress, 1e-9);
		}

		[TestMethod]
		public async Task System_Timestamps_Parse_As_Utc_DateTimes()
		{
			var s = await ParseFullAsync();

			Assert.AreEqual(2, s.Timestamps.Count);
			Assert.AreEqual(new DateTime(2025, 6, 24, 21, 40, 39, DateTimeKind.Utc), s.Timestamps["controllingPower"]);
			Assert.AreEqual(new DateTime(2025, 6, 23, 1, 2, 3, DateTimeKind.Utc), s.Timestamps["powerState"]);
			Assert.AreEqual(DateTimeKind.Utc, s.Timestamps["controllingPower"].Kind);
		}

		// ---- Thargoid war -----------------------------------------------------------------------------------

		[TestMethod]
		public async Task ThargoidWar_Maps_Every_Property_Including_DaysRemaining()
		{
			var s = await ParseFullAsync();
			var war = s.ThargoidWarState;

			Assert.IsNotNull(war, "JSON key 'thargoidWar' must map to StarSystem.ThargoidWarState.");
			Assert.AreEqual("Thargoid Controlled", war.CurrentState);
			Assert.AreEqual("Recovery", war.SuccessState);
			Assert.AreEqual("Invasion", war.FailureState);
			Assert.AreEqual(0.5f, war.Progress);
			Assert.AreEqual(12.5f, war.DaysRemaining);
			Assert.AreEqual(3f, war.PortsRemaining);
			Assert.IsTrue(war.SuccessReached);
		}

		[TestMethod]
		public async Task ThargoidWar_Absent_Yields_Null()
		{
			var s = await TestHelpers.ParseSingleAsync(TestHelpers.ArrayOf(TestHelpers.SystemJson()));
			Assert.IsNull(s.ThargoidWarState);
		}

		[TestMethod]
		public async Task ThargoidWar_Explicit_Null_Yields_Null()
		{
			var json = "[{\"id64\":1,\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"thargoidWar\":null}]";
			var s = await TestHelpers.ParseSingleAsync(json);
			Assert.IsNull(s.ThargoidWarState);
		}

		[TestMethod]
		public async Task ThargoidWar_Without_DaysRemaining_Defaults_To_Zero()
		{
			// Older data dumps predate the daysRemaining field.
			var json = "[{\"id64\":1,\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0}," +
				"\"thargoidWar\":{\"currentState\":\"Alert\",\"progress\":0.1,\"portsRemaining\":2,\"successReached\":false}}]";
			var s = await TestHelpers.ParseSingleAsync(json);

			Assert.IsNotNull(s.ThargoidWarState);
			Assert.AreEqual(0f, s.ThargoidWarState.DaysRemaining);
			Assert.AreEqual("Alert", s.ThargoidWarState.CurrentState);
			Assert.IsNull(s.ThargoidWarState.SuccessState);
			Assert.IsNull(s.ThargoidWarState.FailureState);
			Assert.IsFalse(s.ThargoidWarState.SuccessReached);
		}

		[TestMethod]
		[DataRow("0", 0f)]
		[DataRow("7", 7f)]
		[DataRow("0.25", 0.25f)]
		[DataRow("365.5", 365.5f)]
		public async Task ThargoidWar_DaysRemaining_Accepts_Integer_And_Fractional_Values(string raw, float expected)
		{
			var json = "[{\"id64\":1,\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0}," +
				$"\"thargoidWar\":{{\"daysRemaining\":{raw}}}}}]";
			var s = await TestHelpers.ParseSingleAsync(json);
			Assert.AreEqual(expected, s.ThargoidWarState!.DaysRemaining);
		}

		// ---- Bodies -----------------------------------------------------------------------------------------

		[TestMethod]
		public async Task Star_Body_Properties_Map_Correctly()
		{
			var s = await ParseFullAsync();
			Assert.AreEqual(2, s.Bodies.Count);
			var star = s.Bodies[0];

			Assert.AreEqual(164098653L, star.Id);
			Assert.AreEqual(0, star.BodyId);
			Assert.AreEqual("Achenar", star.Name);
			Assert.AreEqual("Star", star.Type);
			Assert.AreEqual("B (Blue-White) Star", star.SubType);
			Assert.AreEqual(0.0, star.DistanceToArrival);
			Assert.IsTrue(star.MainStar);
			Assert.AreEqual(123, star.Age);
			Assert.AreEqual("B3", star.SpectralClass);
			Assert.AreEqual("Va", star.Luminosity);
			Assert.AreEqual(-0.5, star.AbsoluteMagnitude, 1e-9);
			Assert.AreEqual(6.25, star.SolarMasses, 1e-9);
			Assert.AreEqual(3.5, star.SolarRadius, 1e-9);
			Assert.AreEqual(17000.0, star.SurfaceTemperature, 1e-9);
			Assert.AreEqual(1.5, star.RotationalPeriod, 1e-9);
			Assert.AreEqual(0.25, star.AxialTilt, 1e-9);
			Assert.AreEqual(false, star.RotationalPeriodTidallyLocked);
			Assert.AreEqual("2025-06-22 02:24:45+00", star.UpdateTime);
			Assert.AreEqual("2025-06-22T02:24:45Z", star.Timestamps["distanceToArrival"]);
		}

		[TestMethod]
		public async Task Star_Body_Planet_Only_Properties_Take_Defaults()
		{
			var star = (await ParseFullAsync()).Bodies[0];

			Assert.IsFalse(star.IsLandable);
			Assert.AreEqual(0.0, star.Gravity);
			Assert.IsNull(star.AtmosphereType);
			Assert.IsNull(star.SolidComposition);
			Assert.IsNull(star.Materials);
			Assert.IsNull(star.Signals);
			Assert.AreEqual(0, star.Rings.Count);
			Assert.AreEqual(0, star.Parents.Count);
			Assert.AreEqual(0, star.AtmosphereComposition.Count);
			Assert.AreEqual(0, star.Stations.Count);
		}

		[TestMethod]
		public async Task Belt_Properties_Map_Correctly()
		{
			var star = (await ParseFullAsync()).Bodies[0];

			Assert.AreEqual(1, star.Belts.Count);
			var belt = star.Belts[0];
			Assert.AreEqual("Achenar A Belt", belt.Name);
			Assert.AreEqual("Metal Rich", belt.Type);
			Assert.AreEqual(999L, belt.Id);
			Assert.AreEqual(1500.5, belt.Mass, 1e-9);
			Assert.AreEqual(500.25, belt.InnerRadius, 1e-9);
			Assert.AreEqual(900.75, belt.OuterRadius, 1e-9);
			Assert.IsNotNull(belt.Signals);
			Assert.AreEqual(2, belt.Signals.SignalsMap!["Icy"]);
			Assert.AreEqual("2025-06-22 02:24:45+00", belt.Signals.UpdateTime);
		}

		[TestMethod]
		public async Task Planet_Physical_And_Orbital_Properties_Map_Correctly()
		{
			var planet = (await ParseFullAsync()).Bodies[1];

			Assert.AreEqual(164098654L, planet.Id);
			Assert.AreEqual(1, planet.BodyId);
			Assert.AreEqual("Planet", planet.Type);
			Assert.AreEqual("High metal content world", planet.SubType);
			Assert.AreEqual(12.5, planet.DistanceToArrival, 1e-9);
			Assert.IsTrue(planet.IsLandable);
			Assert.AreEqual(0.38, planet.Gravity, 1e-9);
			Assert.AreEqual(0.02, planet.EarthMasses, 1e-9);
			Assert.AreEqual(1500.5, planet.Radius, 1e-9);
			Assert.AreEqual(0.5, planet.SurfacePressure, 1e-9);
			Assert.AreEqual("No atmosphere", planet.AtmosphereType);
			Assert.AreEqual("No volcanism", planet.VolcanismType);
			Assert.AreEqual("Not terraformable", planet.TerraformingState);
			Assert.AreEqual("Pristine Resources", planet.ReserveLevel);
			Assert.AreEqual(300.0, planet.SurfaceTemperature, 1e-9);
			Assert.AreEqual(2.5, planet.RotationalPeriod, 1e-9);
			Assert.AreEqual(-0.5, planet.AxialTilt, 1e-9);
			Assert.AreEqual(true, planet.RotationalPeriodTidallyLocked);
			Assert.AreEqual(10.5, planet.OrbitalPeriod, 1e-9);
			Assert.AreEqual(1.5, planet.SemiMajorAxis, 1e-9);
			Assert.AreEqual(0.01, planet.OrbitalEccentricity, 1e-9);
			Assert.AreEqual(2.5, planet.OrbitalInclination, 1e-9);
			Assert.AreEqual(45.5, planet.ArgOfPeriapsis, 1e-9);
			Assert.AreEqual(90.5, planet.MeanAnomaly, 1e-9);
			Assert.AreEqual(180.5, planet.AscendingNode, 1e-9);
		}

		[TestMethod]
		public async Task Planet_SolidComposition_Maps_From_PascalCase_Keys()
		{
			var planet = (await ParseFullAsync()).Bodies[1];

			Assert.IsNotNull(planet.SolidComposition);
			Assert.AreEqual(1.5, planet.SolidComposition.Ice, 1e-9);
			Assert.AreEqual(60.5, planet.SolidComposition.Metal, 1e-9);
			Assert.AreEqual(38.0, planet.SolidComposition.Rock, 1e-9);
		}

		[TestMethod]
		public async Task Planet_Materials_And_AtmosphereComposition_Map_Correctly()
		{
			var planet = (await ParseFullAsync()).Bodies[1];

			Assert.IsNotNull(planet.Materials);
			Assert.AreEqual(2, planet.Materials.Count);
			Assert.AreEqual(34.5f, planet.Materials["Iron"]);
			Assert.AreEqual(26.25f, planet.Materials["Nickel"]);

			Assert.AreEqual(2, planet.AtmosphereComposition.Count);
			Assert.AreEqual(80.5f, planet.AtmosphereComposition["Nitrogen"]);
			Assert.AreEqual(19.5f, planet.AtmosphereComposition["Oxygen"]);
		}

		[TestMethod]
		public async Task Planet_Parents_Preserve_Order_And_Keys()
		{
			var planet = (await ParseFullAsync()).Bodies[1];

			Assert.AreEqual(2, planet.Parents.Count);
			Assert.AreEqual(0, planet.Parents[0]["Star"]);
			Assert.AreEqual(3, planet.Parents[1]["Null"]);
		}

		[TestMethod]
		public async Task Planet_Signals_Map_Counts_And_Genuses()
		{
			var signals = (await ParseFullAsync()).Bodies[1].Signals;

			Assert.IsNotNull(signals);
			Assert.AreEqual(2, signals.SignalsMap!.Count);
			Assert.AreEqual(1, signals.SignalsMap["$SAA_SignalType_Human;"]);
			Assert.AreEqual(2, signals.SignalsMap["$SAA_SignalType_Biological;"]);
			Assert.AreEqual("2025-06-23 18:29:34+00", signals.UpdateTime);

			Assert.AreEqual(1, signals.Genuses.Count);
			// Genuses is List<object>, so System.Text.Json hands back a JsonElement.
			Assert.IsInstanceOfType(signals.Genuses[0], typeof(JsonElement));
			Assert.AreEqual("Bacterium Aurasus", ((JsonElement)signals.Genuses[0]).GetString());
		}

		[TestMethod]
		public async Task Planet_Ring_Properties_Map_Correctly()
		{
			var planet = (await ParseFullAsync()).Bodies[1];

			Assert.AreEqual(1, planet.Rings.Count);
			var ring = planet.Rings[0];
			Assert.AreEqual("Achenar 1 A Ring", ring.Name);
			Assert.AreEqual("Rocky", ring.Type);
			Assert.AreEqual(777L, ring.Id);
			Assert.AreEqual(123.5, ring.Mass, 1e-9);
			Assert.AreEqual(2000.5, ring.InnerRadius, 1e-9);
			Assert.AreEqual(3000.5, ring.OuterRadius, 1e-9);
			Assert.IsNotNull(ring.Signals);
			Assert.AreEqual(3, ring.Signals.Signals!["Platinum"]);
			Assert.AreEqual("2025-06-22 03:00:00+00", ring.Signals.UpdateTime);
		}

		[TestMethod]
		public async Task Planet_Surface_Station_Maps_Coordinates()
		{
			var planet = (await ParseFullAsync()).Bodies[1];

			Assert.AreEqual(1, planet.Stations.Count);
			var station = planet.Stations[0];
			Assert.AreEqual("Ground Base", station.Name);
			Assert.AreEqual(42L, station.Id);
			Assert.AreEqual(10.5, station.Latitude, 1e-9);
			Assert.AreEqual(-20.25, station.Longitude, 1e-9);
			Assert.AreEqual("Planetary Outpost", station.Type);
		}

		[TestMethod]
		public async Task Body_Without_TidalLock_Value_Is_Null()
		{
			var body = TestHelpers.BodyJson(5);
			var s = await TestHelpers.ParseSingleAsync(TestHelpers.ArrayOf(TestHelpers.SystemJson(1, "S", 0, body)));
			Assert.IsNull(s.Bodies[0].RotationalPeriodTidallyLocked);
		}

		// ---- Stations ---------------------------------------------------------------------------------------

		[TestMethod]
		public async Task Station_Scalar_Properties_Map_Correctly()
		{
			var s = await ParseFullAsync();
			Assert.AreEqual(1, s.Stations.Count);
			var st = s.Stations[0];

			Assert.AreEqual("Dawes Hub", st.Name);
			Assert.AreEqual("Dawes Colony", st.RealName);
			Assert.AreEqual(3222038272L, st.Id);
			Assert.AreEqual("2025-06-23 02:10:10+00", st.UpdateTime);
			Assert.AreEqual("MY CARRIER", st.CarrierName);
			Assert.AreEqual("all", st.CarrierDockingAccess);
			Assert.AreEqual("Achenar Empire League", st.ControllingFaction);
			Assert.AreEqual("Boom", st.ControllingFactionState);
			Assert.AreEqual(20924.5, st.DistanceToArrival, 1e-9);
			Assert.AreEqual("Service", st.PrimaryEconomy);
			Assert.AreEqual("Empire", st.Allegiance);
			Assert.AreEqual("Patronage", st.Government);
			Assert.AreEqual("Orbis Starport", st.Type);
			Assert.AreEqual("Normal", st.State);
		}

		[TestMethod]
		public async Task Station_Economies_Services_And_LandingPads_Map_Correctly()
		{
			var st = (await ParseFullAsync()).Stations[0];

			Assert.AreEqual(2, st.Economies.Count);
			Assert.AreEqual(75.0f, st.Economies["Service"]);
			Assert.AreEqual(25.0f, st.Economies["Tourism"]);
			CollectionAssert.AreEqual(new[] { "Dock", "Market", "Outfitting" }, st.Services);

			Assert.IsNotNull(st.LandingPads);
			Assert.AreEqual(9, st.LandingPads.Large);
			Assert.AreEqual(18, st.LandingPads.Medium);
			Assert.AreEqual(17, st.LandingPads.Small);
		}

		[TestMethod]
		public async Task Station_Market_Maps_Commodities_And_Prohibited_List()
		{
			var market = (await ParseFullAsync()).Stations[0].Market;

			Assert.IsNotNull(market);
			Assert.AreEqual("2025-06-23 02:10:10+00", market.UpdateTime);
			CollectionAssert.AreEqual(new[] { "Slaves" }, market.ProhibitedCommodities);
			Assert.AreEqual(2, market.Commodities.Count);

			var beer = market.Commodities[0];
			Assert.AreEqual("Beer", beer.Name);
			Assert.AreEqual("Beer", beer.Symbol);
			Assert.AreEqual("Legal Drugs", beer.Category);
			Assert.AreEqual(128049214, beer.CommodityId);
			Assert.AreEqual(3240480, beer.Demand);
			Assert.AreEqual(0, beer.Supply);
			Assert.AreEqual(0, beer.BuyPrice);
			Assert.AreEqual(836, beer.SellPrice);

			var gold = market.Commodities[1];
			Assert.AreEqual("Gold", gold.Name);
			Assert.AreEqual(500, gold.Supply);
			Assert.AreEqual(9000, gold.BuyPrice);
			Assert.AreEqual(8800, gold.SellPrice);
		}

		[TestMethod]
		public async Task Station_Shipyard_Maps_Ships()
		{
			var yard = (await ParseFullAsync()).Stations[0].Shipyard;

			Assert.IsNotNull(yard);
			Assert.AreEqual("2025-06-23 02:11:11+00", yard.UpdateTime);
			Assert.AreEqual(1, yard.Ships.Count);
			Assert.AreEqual("Sidewinder", yard.Ships[0].Name);
			Assert.AreEqual("sidewinder", yard.Ships[0].Symbol);
			Assert.AreEqual(128049249, yard.Ships[0].ShipId);
		}

		[TestMethod]
		public async Task Station_Outfitting_Maps_Modules_Including_Reserved_Word_Class()
		{
			var outfitting = (await ParseFullAsync()).Stations[0].Outfitting;

			Assert.IsNotNull(outfitting);
			Assert.AreEqual("2025-06-23 02:12:12+00", outfitting.UpdateTime);
			Assert.AreEqual(1, outfitting.Modules.Count);

			var module = outfitting.Modules[0];
			Assert.AreEqual("Cargo Rack", module.Name);
			Assert.AreEqual("Int_CargoRack_Size1_Class1", module.Symbol);
			Assert.AreEqual(128064338, module.ModuleId);
			Assert.AreEqual(1, module.Class);
			Assert.AreEqual("E", module.Rating);
			Assert.AreEqual("internal", module.Category);
			Assert.AreEqual("Anaconda", module.Ship);
		}

		[TestMethod]
		public async Task Station_Without_Optional_Sections_Has_Null_Sections_And_Empty_Collections()
		{
			var s = await TestHelpers.ParseSingleAsync(TestHelpers.ArrayOf(TestHelpers.SystemJson(1, "S", 1)));
			var st = s.Stations[0];

			Assert.IsNull(st.Market);
			Assert.IsNull(st.Shipyard);
			Assert.IsNull(st.Outfitting);
			Assert.IsNull(st.LandingPads);
			Assert.AreEqual(0, st.Economies.Count);
			Assert.AreEqual(0, st.Services.Count);
			Assert.IsNull(st.CarrierName);
			Assert.AreEqual(0.0, st.Latitude);
			Assert.AreEqual(0.0, st.Longitude);
		}

		// ---- Tolerance / defaults ---------------------------------------------------------------------------

		[TestMethod]
		public async Task System_With_Only_Required_Members_Takes_Defaults_For_The_Rest()
		{
			var s = await TestHelpers.ParseSingleAsync(
				"[{\"name\":\"Bare\",\"coords\":{\"x\":1,\"y\":2,\"z\":3}}]");

			Assert.AreEqual("Bare", s.Name);
			Assert.AreEqual(0L, s.Id);
			Assert.AreEqual(0L, s.Population);
			Assert.AreEqual(0, s.BodyCount);
			Assert.IsNull(s.Allegiance);
			Assert.IsNull(s.ControllingFaction);
			Assert.IsNull(s.ControllingPower);
			Assert.IsNull(s.Date);
			Assert.IsNull(s.ThargoidWarState);
			Assert.AreEqual(0, s.Factions.Count);
			Assert.AreEqual(0, s.Powers.Count);
			Assert.AreEqual(0, s.PowerConflictProgress.Count);
			Assert.AreEqual(0, s.Timestamps.Count);
			Assert.AreEqual(0, s.Bodies.Count);
			Assert.AreEqual(0, s.Stations.Count);
		}

		[TestMethod]
		public async Task Property_Names_Are_Matched_Case_Insensitively()
		{
			var s = await TestHelpers.ParseSingleAsync(
				"[{\"ID64\":7,\"NAME\":\"Shouty\",\"Coords\":{\"X\":1.5,\"Y\":2.5,\"Z\":3.5},\"PrimaryEconomy\":\"Tourism\"," +
				"\"THARGOIDWAR\":{\"DaysRemaining\":4}}]");

			Assert.AreEqual(7L, s.Id);
			Assert.AreEqual("Shouty", s.Name);
			Assert.AreEqual(1.5, s.Coords.X, 1e-9);
			Assert.AreEqual(3.5, s.Coords.Z, 1e-9);
			Assert.AreEqual("Tourism", s.PrimaryEconomy);
			Assert.AreEqual(4f, s.ThargoidWarState!.DaysRemaining);
		}

		[TestMethod]
		public async Task Unknown_Properties_Are_Ignored()
		{
			var s = await TestHelpers.ParseSingleAsync(
				"[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"someFutureField\":{\"nested\":[1,2,3]},\"another\":\"x\"}]");
			Assert.AreEqual("S", s.Name);
		}

		[TestMethod]
		public async Task Unicode_And_Escaped_Characters_In_Names_Are_Preserved()
		{
			var s = await TestHelpers.ParseSingleAsync(
				"[{\"name\":\"Caf\\u00e9 ñ 中文 \\\"quoted\\\"\",\"coords\":{\"x\":0,\"y\":0,\"z\":0}}]");
			Assert.AreEqual("Café ñ 中文 \"quoted\"", s.Name);
		}

		[TestMethod]
		public async Task Null_Array_Element_Yields_Null_Item()
		{
			var results = await TestHelpers.ParseStringAsync("[null]");
			Assert.AreEqual(1, results.Count);
			Assert.IsNull(results[0]);
		}

		[TestMethod]
		public async Task Mixed_Null_And_Valid_Elements_Preserve_Position()
		{
			var json = $"[{TestHelpers.SystemJson(1, "First")},null,{TestHelpers.SystemJson(2, "Third")}]";
			var results = await TestHelpers.ParseStringAsync(json);

			Assert.AreEqual(3, results.Count);
			Assert.AreEqual("First", results[0]!.Name);
			Assert.IsNull(results[1]);
			Assert.AreEqual("Third", results[2]!.Name);
		}

		// ---- Invalid data -----------------------------------------------------------------------------------

		[TestMethod]
		[DataRow("[{\"id64\":1,\"coords\":{\"x\":0,\"y\":0,\"z\":0}}]", DisplayName = "System missing name")]
		[DataRow("[{\"id64\":1,\"name\":\"S\"}]", DisplayName = "System missing coords")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"bodies\":[{\"id64\":1,\"name\":\"B\"}]}]", DisplayName = "Body missing updateTime")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"stations\":[{\"id\":1,\"updateTime\":\"t\"}]}]", DisplayName = "Station missing name")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"stations\":[{\"name\":\"X\",\"id\":1}]}]", DisplayName = "Station missing updateTime")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"stations\":[{\"name\":\"X\",\"updateTime\":\"t\",\"market\":{\"commodities\":[]}}]}]", DisplayName = "Market missing updateTime")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"stations\":[{\"name\":\"X\",\"updateTime\":\"t\",\"market\":{\"updateTime\":\"t\",\"commodities\":[{\"name\":\"N\",\"category\":\"C\"}]}}]}]", DisplayName = "Commodity missing symbol")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"stations\":[{\"name\":\"X\",\"updateTime\":\"t\",\"shipyard\":{\"ships\":[{\"name\":\"N\"}],\"updateTime\":\"t\"}}]}]", DisplayName = "Ship missing symbol")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"stations\":[{\"name\":\"X\",\"updateTime\":\"t\",\"outfitting\":{\"modules\":[{\"name\":\"N\",\"symbol\":\"S\",\"category\":\"c\"}],\"updateTime\":\"t\"}}]}]", DisplayName = "Module missing rating")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"powerConflictProgress\":[{\"progress\":0.5}]}]", DisplayName = "Power conflict missing power")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"factions\":[{\"influence\":0.1}]}]", DisplayName = "Faction missing name")]
		public async Task Missing_Required_Member_Throws_JsonException(string json)
		{
			await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseStringAsync(json));
		}

		[TestMethod]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"population\":\"lots\"}]", DisplayName = "String where number expected")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"bodyCount\":1.5}]", DisplayName = "Fractional value for int")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"timestamps\":{\"a\":\"not-a-date\"}}]", DisplayName = "Unparseable timestamp")]
		[DataRow("[{\"name\":\"S\",\"coords\":\"nope\"}]", DisplayName = "String where object expected")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0},\"powers\":\"A\"}]", DisplayName = "String where array expected")]
		[DataRow("[1]", DisplayName = "Number element in array")]
		[DataRow("[\"text\"]", DisplayName = "String element in array")]
		[DataRow("[{\"name\":\"S\",\"coords\":{\"x\":0,\"y\":0,\"z\":0}", DisplayName = "Truncated JSON")]
		public async Task Wrongly_Typed_Or_Malformed_Data_Throws_JsonException(string json)
		{
			await Assert.ThrowsExceptionAsync<JsonException>(() => TestHelpers.ParseStringAsync(json));
		}
	}
}
