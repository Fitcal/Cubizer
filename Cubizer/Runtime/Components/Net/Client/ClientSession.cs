﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Cubizer.Net.Protocol;
using Cubizer.Net.Protocol.Serialization;

namespace Cubizer.Net.Client
{
	public sealed class ClientSession : IDisposable
	{
		private readonly int _port;
		private readonly string _hostname;
		private readonly IPacketRouter _packRouter;
		private readonly IPacketCompress _packetCompress;

		private int _sendTimeout = 0;
		private int _receiveTimeout = 0;

		private Task _tcpTask;
		private TcpClient _tcpClient;

		public OnStartTcpListener onStartClientListener { get; set; }
		public OnStopTcpListener onStopClientListener { get; set; }

		public int sendTimeout
		{
			set
			{
				if (_sendTimeout != value)
				{
					if (_tcpClient != null)
						_tcpClient.SendTimeout = value;

					_sendTimeout = value;
				}
			}
			get
			{
				return _sendTimeout;
			}
		}

		public int receiveTimeout
		{
			set
			{
				if (_receiveTimeout != value)
				{
					if (_tcpClient != null)
						_tcpClient.ReceiveTimeout = value;

					_receiveTimeout = value;
				}
			}
			get
			{
				return _receiveTimeout;
			}
		}

		public bool connected
		{
			get
			{
				return _tcpClient != null ? _tcpClient.Connected : false;
			}
		}

		public ClientSession(string hostname, int port, IPacketRouter packetRouter, IPacketCompress packetCompress = null)
		{
			System.Diagnostics.Debug.Assert(packetRouter != null);

			_port = port;
			_hostname = hostname;
			_packRouter = packetRouter;
			_packetCompress = packetCompress ?? new PacketCompress();
		}

		~ClientSession()
		{
			this.Close();
		}

		public bool Connect()
		{
			System.Diagnostics.Debug.Assert(_tcpClient == null);

			try
			{
				_tcpClient = new TcpClient();
				_tcpClient.SendTimeout = _sendTimeout;
				_tcpClient.ReceiveTimeout = _receiveTimeout;
				_tcpClient.Connect(_hostname, _port);

				return _tcpClient.Connected;
			}
			catch (Exception)
			{
				_tcpClient.Close();
				_tcpClient = null;
				return false;
			}
		}

		public Task Start(CancellationToken cancellationToken)
		{
			if (!_tcpClient.Connected)
				throw new InvalidOperationException("Please connect the server before Start()");

			_tcpTask = Task.Run(async () =>
			{
				using (var stream = _tcpClient.GetStream())
				{
					try
					{
						if (onStartClientListener != null)
							onStartClientListener.Invoke();

						while (!cancellationToken.IsCancellationRequested)
							await DispatchIncomingPacket(stream);
					}
					catch (Exception e)
					{
						UnityEngine.Debug.LogException(e);
					}
					finally
					{
						if (onStopClientListener != null)
							onStopClientListener.Invoke();
					}
				}
			});

			return _tcpTask;
		}

		public void Close()
		{
			try
			{
				if (_tcpTask != null)
					_tcpTask.Wait();
			}
			catch (Exception e)
			{
				UnityEngine.Debug.LogException(e);
			}
			finally
			{
				_tcpTask = null;

				if (_tcpClient != null)
				{
					_tcpClient.Close();
					_tcpClient = null;
				}
			}
		}

		public void Dispose()
		{
			this.Close();
		}

		public async Task SendOutcomingPacket(IPacketSerializable packet)
		{
			if (packet == null)
				await SendOutcomingUncompressedPacket(null);
			else
			{
				using (var stream = new MemoryStream())
				{
					using (var bw = new NetworkWrite(stream))
						packet.Serialize(bw);

					await SendOutcomingUncompressedPacket(new UncompressedPacket(packet.packetId, new ArraySegment<byte>(stream.ToArray())));
				}
			}
		}

		public async Task SendOutcomingUncompressedPacket(UncompressedPacket packet)
		{
			if (packet == null)
				_tcpClient.Client.Shutdown(SocketShutdown.Send);
			else
			{
				var newPacket = _packetCompress.Compress(packet);
				await newPacket.SerializeAsync(_tcpClient.GetStream());
			}
		}

		public async Task SendIncomingPacket(IPacketSerializable packet)
		{
			if (packet != null)
			{
				using (var stream = new MemoryStream())
				{
					using (var bw = new NetworkWrite(stream))
						packet.Serialize(bw);

					await SendIncomingUncompressedPacket(new UncompressedPacket(packet.packetId, new ArraySegment<byte>(stream.ToArray())));
				}
			}
		}

		public async Task SendIncomingUncompressedPacket(UncompressedPacket packet)
		{
			await _packRouter.DispatchIncomingPacket(packet);
		}

		private async Task DispatchIncomingPacket(Stream stream)
		{
			var compressedPacket = new CompressedPacket();

			int count = await compressedPacket.DeserializeAsync(stream);
			if (count > 0)
				await SendIncomingUncompressedPacket(_packetCompress.Decompress(compressedPacket));
		}
	}
}