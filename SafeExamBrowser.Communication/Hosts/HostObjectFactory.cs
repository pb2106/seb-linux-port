/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using SafeExamBrowser.Communication;
using SafeExamBrowser.Communication.Contracts;
using SafeExamBrowser.Communication.Contracts.Data;
using SafeExamBrowser.Communication.Contracts.Hosts;

namespace SafeExamBrowser.Communication.Hosts
{
	/// <summary>
	/// Linux/cross-platform implementation of <see cref="IHostObjectFactory"/> using
	/// Unix Domain Sockets instead of WCF named pipes.
	/// The "net.pipe://localhost/…" address is mapped to a socket path under /tmp.
	/// </summary>
	public class HostObjectFactory : IHostObjectFactory
	{
		public IHostObject CreateObject(string address, ICommunication communicationObject)
		{
			var socketPath = AddressToSocketPath(address);
			return new UdsHostObject(socketPath, communicationObject);
		}

		internal static string AddressToSocketPath(string address)
		{
			// net.pipe://localhost/safeexambrowser/… -> /tmp/seb-<hash-of-path>.sock
			var uri = new Uri(address);
			var key = uri.AbsolutePath.Replace('/', '_').TrimStart('_');
			return Path.Combine(Path.GetTempPath(), $"seb-{key}.sock");
		}

		// ----------------------------------------------------------------
		// Inner host: listens on a Unix Domain Socket, deserialises calls
		// to ICommunication and sends back the serialised response.
		// ----------------------------------------------------------------
		private class UdsHostObject : IHostObject
		{
			private readonly string socketPath;
			private readonly ICommunication service;
			private Socket serverSocket;
			private Thread listenerThread;
			private CancellationTokenSource cts;

			public CommunicationState State { get; private set; } = CommunicationState.Created;

			public event EventHandler Closed;
			public event EventHandler Closing;
			public event EventHandler Faulted;
			public event EventHandler Opened;
			public event EventHandler Opening;

			public UdsHostObject(string socketPath, ICommunication service)
			{
				this.socketPath = socketPath;
				this.service = service;
			}

			public void Open()
			{
				Opening?.Invoke(this, EventArgs.Empty);
				State = CommunicationState.Opening;

				if (File.Exists(socketPath))
					File.Delete(socketPath);

				serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
				serverSocket.Bind(new UnixDomainSocketEndPoint(socketPath));
				serverSocket.Listen(8);

				cts = new CancellationTokenSource();
				listenerThread = new Thread(Listen) { IsBackground = true, Name = "UDS-Host:" + socketPath };
				listenerThread.Start();

				State = CommunicationState.Opened;
				Opened?.Invoke(this, EventArgs.Empty);
			}

			public void Close()
			{
				Closing?.Invoke(this, EventArgs.Empty);
				State = CommunicationState.Closing;
				Abort();
				State = CommunicationState.Closed;
				Closed?.Invoke(this, EventArgs.Empty);
			}

			public void Abort()
			{
				cts?.Cancel();
				try { serverSocket?.Close(); } catch { }
				try { if (File.Exists(socketPath)) File.Delete(socketPath); } catch { }
			}

			public void Open(TimeSpan timeout) => Open();
			public void Close(TimeSpan timeout) => Close();

			public IAsyncResult BeginOpen(AsyncCallback callback, object state)
				=> Task.Run((Action)Open).AsApm(callback, state);
			public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
				=> BeginOpen(callback, state);
			public void EndOpen(IAsyncResult result) => ((Task)result).Wait();

			public IAsyncResult BeginClose(AsyncCallback callback, object state)
				=> Task.Run((Action)Close).AsApm(callback, state);
			public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state)
				=> BeginClose(callback, state);
			public void EndClose(IAsyncResult result) => ((Task)result).Wait();

			private void Listen()
			{
				while (!cts.IsCancellationRequested)
				{
					Socket client;
					try { client = serverSocket.Accept(); }
					catch { break; }
					Task.Run(() => HandleClient(client));
				}
			}

			private void HandleClient(Socket client)
			{
				try
				{
					using var ns = new NetworkStream(client, ownsSocket: true);
					var fmt = new BinaryFormatter();
#pragma warning disable SYSLIB0011
					while (client.Connected)
					{
						object request;
						try { request = fmt.Deserialize(ns); }
						catch { break; }

						object response = null;

						if (request is UdsConnectRequest cr)
							response = service.Connect(cr.Token);
						else if (request is UdsDisconnectRequest dr)
							response = service.Disconnect(dr.Message);
						else if (request is UdsSendRequest sr)
							response = service.Send(sr.Message);

						fmt.Serialize(ns, response ?? new object());
					}
#pragma warning restore SYSLIB0011
				}
				catch { /* client disconnected */ }
			}
		}
	}
}
