﻿using HarmonyLib;
using QSB.EchoesOfTheEye.AirlockSync.Messages;
using QSB.EchoesOfTheEye.AirlockSync.WorldObjects;
using QSB.Messaging;
using QSB.Patches;
using QSB.WorldSync;
using System.Linq;

namespace QSB.EchoesOfTheEye.AirlockSync.Patches;

internal class AirlockPatches : QSBPatch
{
	public override QSBPatchTypes Type => QSBPatchTypes.OnClientConnect;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AirlockInterface), nameof(AirlockInterface.OnCallToOpenFront))]
	public static bool Front(AirlockInterface __instance)
	{
		var worldObject = QSBWorldSync.GetWorldObjects<QSBGhostAirlock>().First(x => x.AttachedObject._interface == __instance);
		worldObject.SendMessage(new AirlockCallToOpenMessage(true));
		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(AirlockInterface), nameof(AirlockInterface.OnCallToOpenBack))]
	public static bool Back(AirlockInterface __instance)
	{
		var worldObject = QSBWorldSync.GetWorldObjects<QSBGhostAirlock>().First(x => x.AttachedObject._interface == __instance);
		worldObject.SendMessage(new AirlockCallToOpenMessage(false));
		return true;
	}
}
