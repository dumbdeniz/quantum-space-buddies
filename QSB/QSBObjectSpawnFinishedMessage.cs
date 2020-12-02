﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.Networking;

namespace QSB
{
	class QSBObjectSpawnFinishedMessage : MessageBase
	{
		public override void Deserialize(NetworkReader reader)
		{
			state = reader.ReadPackedUInt32();
		}

		public override void Serialize(NetworkWriter writer)
		{
			writer.WritePackedUInt32(state);
		}

		public uint state;
	}
}
