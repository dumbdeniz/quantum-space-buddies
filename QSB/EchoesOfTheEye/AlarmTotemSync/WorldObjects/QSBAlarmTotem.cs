﻿using Cysharp.Threading.Tasks;
using QSB.EchoesOfTheEye.AlarmTotemSync.Messages;
using QSB.Messaging;
using QSB.Player;
using QSB.Utility;
using QSB.WorldSync;
using System.Collections.Generic;
using System.Threading;

namespace QSB.EchoesOfTheEye.AlarmTotemSync.WorldObjects;

public class QSBAlarmTotem : WorldObject<AlarmTotem>
{
	public readonly List<uint> VisibleFor = new();

	public override void SendInitialState(uint to)
	{
		this.SendMessage(new SetFaceOpenMessage(AttachedObject._isFaceOpen) { To = to });
		this.SendMessage(new SetEnabledMessage(AttachedObject.enabled) { To = to });
		this.SendMessage(new VisibleForMessage(VisibleFor) { To = to });
	}

	public override async UniTask Init(CancellationToken ct)
	{
		QSBPlayerManager.OnRemovePlayer += OnPlayerLeave;

		Delay.RunWhen(() => QSBWorldSync.AllObjectsReady, () =>
		{
			if (AttachedObject._isPlayerVisible)
			{
				this.SendMessage(new LocallyVisibleMessage(true));
			}
		});
	}

	public override void OnRemoval() =>
		QSBPlayerManager.OnRemovePlayer -= OnPlayerLeave;

	private void OnPlayerLeave(PlayerInfo player) =>
		VisibleFor.QuickRemove(player.PlayerId);

	public void SetLocallyVisible(uint playerId, bool visible)
	{
		if (visible)
		{
			VisibleFor.SafeAdd(playerId);
		}
		else
		{
			VisibleFor.QuickRemove(playerId);
		}

		UpdateVisible();
	}

	public void UpdateVisible()
	{
		if (AttachedObject._isPlayerVisible && VisibleFor.Count < 1)
		{
			Locator.GetAlarmSequenceController().IncreaseAlarmCounter();
			AttachedObject._simTotemMaterials[0] = AttachedObject._simAlarmMaterial;
			AttachedObject._simTotemRenderer.sharedMaterials = AttachedObject._simTotemMaterials;
			AttachedObject._simVisionConeRenderer.SetColor(AttachedObject._simAlarmColor);
			if (AttachedObject._isTutorialTotem)
			{
				GlobalMessenger.FireEvent("TutorialAlarmTotemTriggered");
			}
		}
		else if (!AttachedObject._isPlayerVisible && VisibleFor.Count >= 1)
		{
			Locator.GetAlarmSequenceController().DecreaseAlarmCounter();
			AttachedObject._simTotemMaterials[0] = AttachedObject._origSimEyeMaterial;
			AttachedObject._simTotemRenderer.sharedMaterials = AttachedObject._simTotemMaterials;
			AttachedObject._simVisionConeRenderer.SetColor(AttachedObject._simVisionConeRenderer.GetOriginalColor());
			AttachedObject._pulseLightController.FadeTo(0f, 0.5f);
		}
	}

	public void SetEnabled(bool enabled)
	{
		if (AttachedObject.enabled == enabled)
		{
			return;
		}

		if (!enabled &&
			AttachedObject._sector &&
			AttachedObject._sector.ContainsOccupant(DynamicOccupant.Player))
		{
			// local player is in sector, do not disable
			return;
		}

		AttachedObject.enabled = enabled;

		if (!enabled)
		{
			AttachedObject._pulseLightController.SetIntensity(0f);
			AttachedObject._simTotemMaterials[0] = AttachedObject._origSimEyeMaterial;
			AttachedObject._simTotemRenderer.sharedMaterials = AttachedObject._simTotemMaterials;
			AttachedObject._simVisionConeRenderer.SetColor(AttachedObject._simVisionConeRenderer.GetOriginalColor());
			if (AttachedObject._isPlayerVisible)
			{
				AttachedObject._isPlayerVisible = false;
				Locator.GetAlarmSequenceController().DecreaseAlarmCounter();
			}
		}
	}
}
