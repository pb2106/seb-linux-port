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
using SafeExamBrowser.Communication.Contracts.Proxies;
using SafeExamBrowser.Communication.Hosts;

namespace SafeExamBrowser.Communication.Proxies
{
	/// <summary>
	/// Linux/cross-platform implementation of <see cref="IProxyObjectFactory"/> using
	/// Unix Domain Sockets instead of WCF named pipes.
	/// </summary>
	public class ProxyObjectFactory : IProxyObjectFactory
	{
		public IProxyObject CreateObject(string address)
		{
			var socketPath = HostObjectFactory.AddressToSocketPath(address);
			return new UdsProxyObject(socketPath);
		}

		// ----------------------------------------------------------------
		// UDS proxy: connects to the host socket and forwards ICommunication
		// calls over the binary stream, maintaining the ICommunicationObject
		// state machine expected by BaseProxy.
		// ----------------------------------------------------------------
		private class UdsProxyObject : IProxyObject
		{
			private readonly string socketPath;
			private Socket socket;
			private NetworkStream ns;
			private BinaryFormatter fmt;

			public CommunicationState State { get; private set; } = CommunicationState.Created;

			public event EventHandler Closed;
			public event EventHandler Closing;
			public event EventHandler Faulted;
			public event EventHandler Opened;
			public event EventHandler Opening;

			public UdsProxyObject(string socketPath)
			{
				this.socketPath = socketPath;
			}

			public void Open()
			{
				Opening?.Invoke(this, EventArgs.Empty);
				State = CommunicationState.Opening;

				socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
				socket.Connect(new UnixDomainSocketEndPoint(socketPath));
				ns = new NetworkStream(socket, ownsSocket: false);
#pragma warning disable SYSLIB0011
				fmt = new BinaryFormatter();
#pragma warning restore SYSLIB0011

				State = CommunicationState.Opened;
				Opened?.Invoke(this, EventArgs.Empty);
			}

			public void Close()
			{
				Closing?.Invoke(this, EventArgs.Empty);
				State = CommunicationState.Closing;
				try { ns?.Close(); } catch { }
				try { socket?.Close(); } catch { }
				State = CommunicationState.Closed;
				Closed?.Invoke(this, EventArgs.Empty);
			}

			public void Abort()
			{
				try { ns?.Close(); } catch { }
				try { socket?.Close(); } catch { }
				State = CommunicationState.Faulted;
				Faulted?.Invoke(this, EventArgs.Empty);
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

			// ICommunication forwarded over the socket
			public ConnectionResponse Connect(Guid? token = null)
				=> (ConnectionResponse)Call(new UdsConnectRequest { Token = token });

			public DisconnectionResponse Disconnect(DisconnectionMessage message)
				=> (DisconnectionResponse)Call(new UdsDisconnectRequest { Message = message });

			public Response Send(Message message)
				=> (Response)Call(new UdsSendRequest { Message = message });

			private object Call(object request)
			{
				if (State != CommunicationState.Opened)
					throw new InvalidOperationException("Proxy is not open.");

				try
				{
#pragma warning disable SYSLIB0011
					fmt.Serialize(ns, request);
					return fmt.Deserialize(ns);
#pragma warning restore SYSLIB0011
				}
				catch (Exception)
				{
					Abort();
					throw;
				}
			}
		}
	}
}
