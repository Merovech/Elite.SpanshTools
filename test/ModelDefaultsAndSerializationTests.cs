using System.Text.Json;
using Elite.SpanshTools.Model;

namespace Elite.SpanshTools.Tests
{
	/// <summary>
	/// Covers the model classes directly: constructor defaults (the docs promise "defaults to an empty
	/// list/dictionary"), and the JSON name attributes, which only matter when the types are serialized.
	/// </summary>
	[TestClass]
	public class ModelDefaultsAndSerializationTests
	{
		private static StarSystem NewSystem() => new() { Name = "S", Coords = new Coords() };

		private static Body NewBody() => new() { UpdateTime = "t" };

		private static Station NewStation() => new() { Name = "St", UpdateTime = "t" };

		// ---- Defaults ---------------------------------------------------------------------------------------

		[TestMethod]
		public void StarSystem_Collections_Default_To_Empty_Not_Null()
		{
			var s = NewSystem();

			Assert.IsNotNull(s.Factions);
			Assert.IsNotNull(s.Powers);
			Assert.IsNotNull(s.PowerConflictProgress);
			Assert.IsNotNull(s.Timestamps);
			Assert.IsNotNull(s.Bodies);
			Assert.IsNotNull(s.Stations);
			Assert.AreEqual(0, s.Factions.Count + s.Powers.Count + s.PowerConflictProgress.Count
				+ s.Timestamps.Count + s.Bodies.Count + s.Stations.Count);
		}

		[TestMethod]
		public void StarSystem_Optional_Members_Default_To_Null_Or_Zero()
		{
			var s = NewSystem();

			Assert.IsNull(s.Allegiance);
			Assert.IsNull(s.Government);
			Assert.IsNull(s.PrimaryEconomy);
			Assert.IsNull(s.SecondaryEconomy);
			Assert.IsNull(s.Security);
			Assert.IsNull(s.ControllingFaction);
			Assert.IsNull(s.ControllingPower);
			Assert.IsNull(s.PowerState);
			Assert.IsNull(s.Date);
			Assert.IsNull(s.ThargoidWarState);
			Assert.AreEqual(0L, s.Id);
			Assert.AreEqual(0L, s.Population);
			Assert.AreEqual(0, s.BodyCount);
			Assert.AreEqual(0.0, s.PowerStateControlProgress);
			Assert.AreEqual(0, s.PowerStateReinforcement);
			Assert.AreEqual(0, s.PowerStateUndermining);
		}

		[TestMethod]
		public void Body_Collections_Default_To_Empty_And_Nullable_Members_To_Null()
		{
			var b = NewBody();

			Assert.AreEqual(0, b.Belts.Count);
			Assert.AreEqual(0, b.Timestamps.Count);
			Assert.AreEqual(0, b.Stations.Count);
			Assert.AreEqual(0, b.Parents.Count);
			Assert.AreEqual(0, b.AtmosphereComposition.Count);
			Assert.AreEqual(0, b.Rings.Count);

			// These are intentionally nullable, to distinguish "absent" from "empty".
			Assert.IsNull(b.Materials);
			Assert.IsNull(b.SolidComposition);
			Assert.IsNull(b.Signals);
			Assert.IsNull(b.RotationalPeriodTidallyLocked);
			Assert.IsFalse(b.IsLandable);
			Assert.IsFalse(b.MainStar);
		}

		[TestMethod]
		public void Station_Collections_Default_To_Empty_And_Sections_To_Null()
		{
			var st = NewStation();

			Assert.AreEqual(0, st.Economies.Count);
			Assert.AreEqual(0, st.Services.Count);
			Assert.IsNull(st.LandingPads);
			Assert.IsNull(st.Market);
			Assert.IsNull(st.Shipyard);
			Assert.IsNull(st.Outfitting);
		}

		[TestMethod]
		public void Faction_State_Lists_Default_To_Empty_And_Influence_To_Null()
		{
			var f = new Faction { Name = "F" };

			Assert.AreEqual(0, f.ActiveStates.Count);
			Assert.AreEqual(0, f.PendingStates.Count);
			Assert.AreEqual(0, f.RecoveringStates.Count);
			Assert.IsNull(f.Influence);
		}

		[TestMethod]
		public void Market_Shipyard_Outfitting_Signals_Lists_Default_To_Empty()
		{
			Assert.AreEqual(0, new Market { UpdateTime = "t" }.Commodities.Count);
			Assert.AreEqual(0, new Market { UpdateTime = "t" }.ProhibitedCommodities.Count);
			Assert.AreEqual(0, new Shipyard { UpdateTime = "t" }.Ships.Count);
			Assert.AreEqual(0, new Outfitting { UpdateTime = "t" }.Modules.Count);

			var signals = new Signals();
			Assert.AreEqual(0, signals.Genuses.Count);
			Assert.IsNull(signals.SignalsMap);
			Assert.IsNull(signals.UpdateTime);
		}

		[TestMethod]
		public void ThargoidWarState_Defaults_Are_Zero_False_And_Null()
		{
			var w = new ThargoidWarState();

			Assert.IsNull(w.CurrentState);
			Assert.IsNull(w.SuccessState);
			Assert.IsNull(w.FailureState);
			Assert.AreEqual(0f, w.Progress);
			Assert.AreEqual(0f, w.DaysRemaining);
			Assert.AreEqual(0f, w.PortsRemaining);
			Assert.IsFalse(w.SuccessReached);
		}

		[TestMethod]
		public void Default_Collections_Are_Not_Shared_Between_Instances()
		{
			var a = NewSystem();
			var b = NewSystem();
			a.Powers.Add("Someone");
			a.Bodies.Add(NewBody());
			a.Timestamps["k"] = DateTime.UtcNow;

			Assert.AreEqual(0, b.Powers.Count);
			Assert.AreEqual(0, b.Bodies.Count);
			Assert.AreEqual(0, b.Timestamps.Count);

			var bodyA = NewBody();
			var bodyB = NewBody();
			bodyA.Rings.Add(new Ring { Name = "r", Type = "t" });
			Assert.AreEqual(0, bodyB.Rings.Count);
		}

		[TestMethod]
		public void ThargoidWarState_Properties_Are_Independent()
		{
			var w = new ThargoidWarState { DaysRemaining = 3.5f, PortsRemaining = 9f, Progress = 0.75f };

			Assert.AreEqual(3.5f, w.DaysRemaining);
			Assert.AreEqual(9f, w.PortsRemaining);
			Assert.AreEqual(0.75f, w.Progress);
		}

		// ---- JSON names -------------------------------------------------------------------------------------

		[TestMethod]
		public void StarSystem_Serializes_Id_As_id64_And_ThargoidWarState_As_thargoidWar()
		{
			var s = NewSystem();
			s.Id = 42;
			s.ThargoidWarState = new ThargoidWarState { DaysRemaining = 2f };

			using var doc = JsonDocument.Parse(JsonSerializer.Serialize(s));
			var root = doc.RootElement;

			Assert.IsTrue(root.TryGetProperty("id64", out var id));
			Assert.AreEqual(42L, id.GetInt64());
			Assert.IsFalse(root.TryGetProperty("Id", out _), "The C# member name must not leak into the JSON.");
			Assert.IsTrue(root.TryGetProperty("thargoidWar", out var war));
			Assert.AreEqual(2f, war.GetProperty("DaysRemaining").GetSingle());
			Assert.IsFalse(root.TryGetProperty("ThargoidWarState", out _));
		}

		[TestMethod]
		public void Body_Belt_And_Ring_Serialize_Id_As_id64()
		{
			var body = NewBody();
			body.Id = 1;
			var belt = new Belt { Name = "b", Type = "t", Id = 2 };
			var ring = new Ring { Name = "r", Type = "t", Id = 3 };

			foreach (var (obj, expected) in new (object, long)[] { (body, 1), (belt, 2), (ring, 3) })
			{
				using var doc = JsonDocument.Parse(JsonSerializer.Serialize(obj, obj.GetType()));
				Assert.IsTrue(doc.RootElement.TryGetProperty("id64", out var id), $"{obj.GetType().Name} is missing 'id64'.");
				Assert.AreEqual(expected, id.GetInt64());
			}
		}

		[TestMethod]
		public void Signals_And_RingSignals_Serialize_Map_As_signals()
		{
			var signals = new Signals { SignalsMap = new Dictionary<string, int> { ["Human"] = 1 } };
			var ringSignals = new RingSignals { Signals = new Dictionary<string, int> { ["Platinum"] = 2 } };

			using var a = JsonDocument.Parse(JsonSerializer.Serialize(signals));
			using var b = JsonDocument.Parse(JsonSerializer.Serialize(ringSignals));

			Assert.AreEqual(1, a.RootElement.GetProperty("signals").GetProperty("Human").GetInt32());
			Assert.AreEqual(2, b.RootElement.GetProperty("signals").GetProperty("Platinum").GetInt32());
		}

		[TestMethod]
		public async Task Fully_Populated_System_Survives_A_Serialize_Then_Parse_Round_Trip()
		{
			var original = await TestHelpers.ParseSingleAsync(FullSystemJson.Value);

			var reserialized = JsonSerializer.Serialize(new[] { original });
			var roundTripped = await TestHelpers.ParseSingleAsync(reserialized);

			// Compare through a canonical serialization rather than field-by-field; that way a property
			// added to the model later is covered automatically.
			Assert.AreEqual(JsonSerializer.Serialize(original), JsonSerializer.Serialize(roundTripped));
			Assert.AreEqual(original.ThargoidWarState!.DaysRemaining, roundTripped.ThargoidWarState!.DaysRemaining);
			Assert.AreEqual(original.Bodies[1].Rings[0].Id, roundTripped.Bodies[1].Rings[0].Id);
		}
	}
}
