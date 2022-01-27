﻿using QSB.Messaging;
using QSB.Tools.TranslatorTool.TranslationSync.WorldObjects;

namespace QSB.Tools.TranslatorTool.TranslationSync.Messages
{
	internal class SetAsTranslatedMessage : QSBWorldObjectMessage<QSBNomaiText, int>
	{
		public SetAsTranslatedMessage(int textId) => Value = textId;

		public override void OnReceiveRemote() => WorldObject.SetAsTranslated(Value);
	}
}