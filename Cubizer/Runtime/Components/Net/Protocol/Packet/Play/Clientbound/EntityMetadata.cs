﻿using Cubizer.Net.Protocol.Serialization;

namespace Cubizer.Net.Protocol.Play.Clientbound
{
	[Packet(Packet)]
	public class EntityMetadata : IPacketSerializable
	{
		public const int Packet = 0x3B;

		public uint packetId
		{
			get
			{
				return Packet;
			}
		}

		public object Clone()
		{
			return new EntityMetadata();
		}

		public void Deserialize(NetworkReader br)
		{
			throw new System.NotImplementedException();
		}

		public void Serialize(NetworkWrite bw)
		{
			throw new System.NotImplementedException();
		}
	}
}