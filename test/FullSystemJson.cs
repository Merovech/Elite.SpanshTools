namespace Elite.SpanshTools.Tests
{
	/// <summary>
	/// A single hand-built system that populates every property of every model class at least once, with
	/// distinct values so a mis-mapped property is detected.  Note that solidComposition uses PascalCase
	/// keys, as the real Spansh data does.
	/// </summary>
	internal static class FullSystemJson
	{
		internal const string Value = """
[{
  "id64": 164098653,
  "name": "Achenar",
  "coords": { "x": 67.5, "y": -119.46875, "z": 24.84375 },
  "allegiance": "Empire",
  "government": "Patronage",
  "primaryEconomy": "Refinery",
  "secondaryEconomy": "Service",
  "security": "High",
  "population": 16380054761,
  "bodyCount": 15,
  "controllingFaction": { "name": "Achenar Empire League", "government": "Patronage", "allegiance": "Empire" },
  "factions": [
    {
      "name": "Achenar Comms Co", "allegiance": "Empire", "government": "Patronage",
      "influence": 0.064286, "state": "Infrastructure Failure",
      "activeStates": [ { "state": "Boom", "trend": 1.5 }, { "state": "Expansion" } ],
      "pendingStates": [ { "state": "War", "trend": 0 } ],
      "recoveringStates": [ { "state": "Bust", "trend": -1 } ]
    },
    { "name": "Minor Faction", "influence": null }
  ],
  "controllingPower": "A. Lavigny-Duval",
  "powerState": "Stronghold",
  "powerStateControlProgress": 0.810922,
  "powerStateReinforcement": 23922,
  "powerStateUndermining": 5,
  "powers": [ "A. Lavigny-Duval", "Aisling Duval", "Denton Patreus" ],
  "powerConflictProgress": [ { "power": "Aisling Duval", "progress": 0.25 }, { "power": "Denton Patreus", "progress": 0.125 } ],
  "timestamps": { "controllingPower": "2025-06-24T21:40:39Z", "powerState": "2025-06-23T01:02:03Z" },
  "date": "2025-06-24 21:40:39+00",
  "thargoidWar": {
    "currentState": "Thargoid Controlled", "successState": "Recovery", "failureState": "Invasion",
    "progress": 0.5, "daysRemaining": 12.5, "portsRemaining": 3, "successReached": true
  },
  "bodies": [
    {
      "id64": 164098653, "bodyId": 0, "name": "Achenar", "type": "Star", "subType": "B (Blue-White) Star",
      "distanceToArrival": 0, "mainStar": true, "age": 123, "spectralClass": "B3", "luminosity": "Va",
      "absoluteMagnitude": -0.5, "solarMasses": 6.25, "solarRadius": 3.5, "surfaceTemperature": 17000,
      "rotationalPeriod": 1.5, "axialTilt": 0.25, "rotationalPeriodTidallyLocked": false,
      "updateTime": "2025-06-22 02:24:45+00",
      "belts": [
        {
          "name": "Achenar A Belt", "type": "Metal Rich", "id64": 999, "mass": 1500.5,
          "innerRadius": 500.25, "outerRadius": 900.75,
          "signals": { "signals": { "Icy": 2 }, "updateTime": "2025-06-22 02:24:45+00" }
        }
      ],
      "timestamps": { "distanceToArrival": "2025-06-22T02:24:45Z" }
    },
    {
      "id64": 164098654, "bodyId": 1, "name": "Achenar 1", "type": "Planet", "subType": "High metal content world",
      "distanceToArrival": 12.5, "isLandable": true, "gravity": 0.38, "earthMasses": 0.02, "radius": 1500.5,
      "surfacePressure": 0.5, "atmosphereType": "No atmosphere", "volcanismType": "No volcanism",
      "terraformingState": "Not terraformable", "reserveLevel": "Pristine Resources",
      "solidComposition": { "Ice": 1.5, "Metal": 60.5, "Rock": 38.0 },
      "materials": { "Iron": 34.5, "Nickel": 26.25 },
      "atmosphereComposition": { "Nitrogen": 80.5, "Oxygen": 19.5 },
      "signals": {
        "signals": { "$SAA_SignalType_Human;": 1, "$SAA_SignalType_Biological;": 2 },
        "genuses": [ "Bacterium Aurasus" ],
        "updateTime": "2025-06-23 18:29:34+00"
      },
      "parents": [ { "Star": 0 }, { "Null": 3 } ],
      "orbitalPeriod": 10.5, "semiMajorAxis": 1.5, "orbitalEccentricity": 0.01, "orbitalInclination": 2.5,
      "argOfPeriapsis": 45.5, "meanAnomaly": 90.5, "ascendingNode": 180.5,
      "rotationalPeriod": 2.5, "axialTilt": -0.5, "rotationalPeriodTidallyLocked": true, "surfaceTemperature": 300,
      "rings": [
        {
          "name": "Achenar 1 A Ring", "type": "Rocky", "id64": 777, "mass": 123.5,
          "innerRadius": 2000.5, "outerRadius": 3000.5,
          "signals": { "signals": { "Platinum": 3 }, "updateTime": "2025-06-22 03:00:00+00" }
        }
      ],
      "stations": [
        { "name": "Ground Base", "id": 42, "updateTime": "2025-06-22 14:22:09+00", "latitude": 10.5, "longitude": -20.25, "type": "Planetary Outpost" }
      ],
      "updateTime": "2025-06-22 02:24:45+00"
    }
  ],
  "stations": [
    {
      "name": "Dawes Hub", "realName": "Dawes Colony", "id": 3222038272, "updateTime": "2025-06-23 02:10:10+00",
      "carrierName": "MY CARRIER", "carrierDockingAccess": "all",
      "controllingFaction": "Achenar Empire League", "controllingFactionState": "Boom",
      "distanceToArrival": 20924.5, "primaryEconomy": "Service",
      "economies": { "Service": 75.0, "Tourism": 25.0 },
      "allegiance": "Empire", "government": "Patronage",
      "services": [ "Dock", "Market", "Outfitting" ], "type": "Orbis Starport", "state": "Normal",
      "landingPads": { "large": 9, "medium": 18, "small": 17 },
      "market": {
        "commodities": [
          { "name": "Beer", "symbol": "Beer", "category": "Legal Drugs", "commodityId": 128049214, "demand": 3240480, "supply": 0, "buyPrice": 0, "sellPrice": 836 },
          { "name": "Gold", "symbol": "Gold", "category": "Metals", "commodityId": 128049154, "demand": 1, "supply": 500, "buyPrice": 9000, "sellPrice": 8800 }
        ],
        "prohibitedCommodities": [ "Slaves" ],
        "updateTime": "2025-06-23 02:10:10+00"
      },
      "shipyard": {
        "ships": [ { "name": "Sidewinder", "symbol": "sidewinder", "shipId": 128049249 } ],
        "updateTime": "2025-06-23 02:11:11+00"
      },
      "outfitting": {
        "modules": [ { "name": "Cargo Rack", "symbol": "Int_CargoRack_Size1_Class1", "moduleId": 128064338, "class": 1, "rating": "E", "category": "internal", "ship": "Anaconda" } ],
        "updateTime": "2025-06-23 02:12:12+00"
      }
    }
  ]
}]
""";
	}
}
