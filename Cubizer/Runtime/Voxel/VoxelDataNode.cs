﻿using System;

namespace Cubizer
{
	[Serializable]
	public sealed class VoxelDataNode<_Tx, _Ty>
		where _Tx : struct
		where _Ty : class
	{
		public readonly _Tx position;
		public _Ty value;

		public VoxelDataNode()
		{
			value = null;
		}

		public VoxelDataNode(_Tx x, _Ty value)
		{
			this.position = x;
			this.value = value;
		}

		public bool is_empty()
		{
			return value == null;
		}
	}
}