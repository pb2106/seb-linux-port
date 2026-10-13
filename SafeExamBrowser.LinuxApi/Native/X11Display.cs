/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SafeExamBrowser.LinuxApi.Native
{
	/// <summary>
	/// Thin, thread-safe wrapper around an Xlib connection which offers the EWMH helpers required by the SEB window management.
	/// </summary>
	internal class X11Display : IDisposable
	{
		internal const int PropertyChangeMask = 1 << 22;
		internal const int SubstructureNotifyMask = 1 << 19;
		internal const int SubstructureRedirectMask = 1 << 20;
		internal const int PropertyNotify = 28;
		internal const int MapNotify = 19;
		internal const int CreateNotify = 16;
		internal const int IsViewable = 2;
		internal const int PropModeReplace = 0;
		internal const int ClientMessage = 33;
		internal const int IconicState = 3;

		private static readonly object InitLock = new object();
		private static bool initialized;
		private static XErrorHandler errorHandler;
		private static volatile bool errorOccurred;

		private readonly object sync = new object();
		private readonly Dictionary<string, IntPtr> atoms = new Dictionary<string, IntPtr>();

		internal IntPtr Handle { get; private set; }
		internal IntPtr Root { get; private set; }
		internal object SyncRoot => sync;

		private X11Display(IntPtr handle)
		{
			Handle = handle;
			Root = XDefaultRootWindow(handle);
		}

		/// <summary>
		/// Attempts to open a connection to the X server, honoring DISPLAY (or XWayland on Wayland sessions).
		/// </summary>
		internal static bool TryOpen(out X11Display display, string name = null)
		{
			display = default;

			try
			{
				lock (InitLock)
				{
					if (!initialized)
					{
						XInitThreads();
						errorHandler = (d, e) => { errorOccurred = true; return 0; };
						XSetErrorHandler(errorHandler);
						initialized = true;
					}
				}

				var handle = XOpenDisplay(name);

				if (handle != IntPtr.Zero)
				{
					display = new X11Display(handle);
				}
			}
			catch (DllNotFoundException)
			{
			}
			catch (EntryPointNotFoundException)
			{
			}

			return display != default;
		}

		internal IntPtr Atom(string name)
		{
			lock (sync)
			{
				if (!atoms.TryGetValue(name, out var atom))
				{
					atoms[name] = atom = XInternAtom(Handle, name, false);
				}

				return atom;
			}
		}

		internal string AtomName(IntPtr atom)
		{
			lock (sync)
			{
				var ptr = XGetAtomName(Handle, atom);

				if (ptr == IntPtr.Zero)
				{
					return null;
				}

				var name = Marshal.PtrToStringAnsi(ptr);

				XFree(ptr);

				return name;
			}
		}

		internal bool Exists(IntPtr window)
		{
			lock (sync)
			{
				errorOccurred = false;

				var attributes = default(XWindowAttributes);
				var result = XGetWindowAttributes(Handle, window, out attributes);

				XSync(Handle, false);

				return result != 0 && !errorOccurred;
			}
		}

		internal bool TryGetAttributes(IntPtr window, out XWindowAttributes attributes)
		{
			lock (sync)
			{
				errorOccurred = false;

				var result = XGetWindowAttributes(Handle, window, out attributes);

				XSync(Handle, false);

				return result != 0 && !errorOccurred;
			}
		}

		/// <summary>
		/// Reads the raw 32-bit items of a property (returned as native longs, i.e. IntPtr-sized values).
		/// </summary>
		internal long[] GetLongProperty(IntPtr window, string property, long maxItems = 1024)
		{
			var data = GetProperty(window, property, maxItems, out var format, out var count, out var type);

			if (data == null || format != 32)
			{
				return new long[0];
			}

			var items = new long[count];

			for (var i = 0; i < count; i++)
			{
				items[i] = BitConverter.ToInt64(data, i * IntPtr.Size);
			}

			return items;
		}

		internal string GetStringProperty(IntPtr window, string property)
		{
			var data = GetProperty(window, property, 4096, out var format, out var count, out _);

			if (data == null || format != 8)
			{
				return null;
			}

			return Encoding.UTF8.GetString(data, 0, (int) count).TrimEnd('\0');
		}

		internal byte[] GetProperty(IntPtr window, string property, long maxItems, out int format, out long count, out IntPtr type)
		{
			lock (sync)
			{
				errorOccurred = false;

				var result = XGetWindowProperty(Handle, window, Atom(property), 0, new IntPtr(maxItems), false, IntPtr.Zero, out type,
					out format, out var items, out var remaining, out var prop);

				XSync(Handle, false);

				count = items.ToInt64();

				if (result != 0 || errorOccurred || prop == IntPtr.Zero)
				{
					if (prop != IntPtr.Zero)
					{
						XFree(prop);
					}

					return null;
				}

				var size = format == 32 ? IntPtr.Size : format / 8;
				var bytes = new byte[count * size];

				if (bytes.Length > 0)
				{
					Marshal.Copy(prop, bytes, 0, bytes.Length);
				}

				XFree(prop);

				return bytes;
			}
		}

		internal void SetLongProperty(IntPtr window, string property, string typeName, params long[] values)
		{
			lock (sync)
			{
				var buffer = Marshal.AllocHGlobal(values.Length * IntPtr.Size);

				for (var i = 0; i < values.Length; i++)
				{
					Marshal.WriteIntPtr(buffer, i * IntPtr.Size, new IntPtr(values[i]));
				}

				XChangeProperty(Handle, window, Atom(property), Atom(typeName), 32, PropModeReplace, buffer, values.Length);
				XFlush(Handle);
				Marshal.FreeHGlobal(buffer);
			}
		}

		internal void SetAtomProperty(IntPtr window, string property, params string[] atomNames)
		{
			var values = new long[atomNames.Length];

			for (var i = 0; i < atomNames.Length; i++)
			{
				values[i] = Atom(atomNames[i]).ToInt64();
			}

			SetLongProperty(window, property, "ATOM", values);
		}

		internal IntPtr[] Children(IntPtr window)
		{
			lock (sync)
			{
				if (XQueryTree(Handle, window, out _, out _, out var children, out var count) == 0 || children == IntPtr.Zero)
				{
					return new IntPtr[0];
				}

				var result = new IntPtr[count];

				for (var i = 0; i < count; i++)
				{
					result[i] = Marshal.ReadIntPtr(children, i * IntPtr.Size);
				}

				XFree(children);

				return result;
			}
		}

		internal void SendClientMessage(IntPtr window, string message, long l0 = 0, long l1 = 0, long l2 = 0, long l3 = 0, long l4 = 0)
		{
			lock (sync)
			{
				var ev = new XClientMessageEvent
				{
					type = ClientMessage,
					send_event = 1,
					display = Handle,
					window = window,
					message_type = Atom(message),
					format = 32,
					l0 = new IntPtr(l0),
					l1 = new IntPtr(l1),
					l2 = new IntPtr(l2),
					l3 = new IntPtr(l3),
					l4 = new IntPtr(l4)
				};
				var buffer = Marshal.AllocHGlobal(192);

				try
				{
					for (var i = 0; i < 192; i++)
					{
						Marshal.WriteByte(buffer, i, 0);
					}

					Marshal.StructureToPtr(ev, buffer, false);
					XSendEvent(Handle, Root, false, new IntPtr(SubstructureNotifyMask | SubstructureRedirectMask), buffer);
					XFlush(Handle);
				}
				finally
				{
					Marshal.FreeHGlobal(buffer);
				}
			}
		}

		internal void Flush()
		{
			lock (sync)
			{
				XFlush(Handle);
			}
		}

		public void Dispose()
		{
			lock (sync)
			{
				if (Handle != IntPtr.Zero)
				{
					XCloseDisplay(Handle);
					Handle = IntPtr.Zero;
				}
			}
		}

		internal delegate int XErrorHandler(IntPtr display, IntPtr errorEvent);

		[StructLayout(LayoutKind.Sequential)]
		internal struct XClientMessageEvent
		{
			public int type;
			public IntPtr serial;
			public int send_event;
			public IntPtr display;
			public IntPtr window;
			public IntPtr message_type;
			public int format;
			public IntPtr l0, l1, l2, l3, l4;
		}

		[StructLayout(LayoutKind.Sequential)]
		internal struct XWindowAttributes
		{
			public int x, y, width, height, border_width, depth;
			public IntPtr visual, root;
			public int @class, bit_gravity, win_gravity, backing_store;
			public IntPtr backing_planes, backing_pixel;
			public int save_under;
			public IntPtr colormap;
			public int map_installed, map_state;
			public IntPtr all_event_masks, your_event_mask, do_not_propagate_mask;
			public int override_redirect;
			public IntPtr screen;
		}

		private const string X11 = "libX11.so.6";

		[DllImport(X11)] internal static extern int XInitThreads();
		[DllImport(X11)] internal static extern IntPtr XOpenDisplay([MarshalAs(UnmanagedType.LPStr)] string name);
		[DllImport(X11)] internal static extern int XCloseDisplay(IntPtr display);
		[DllImport(X11)] internal static extern IntPtr XDefaultRootWindow(IntPtr display);
		[DllImport(X11)] internal static extern IntPtr XSetErrorHandler(XErrorHandler handler);
		[DllImport(X11)] internal static extern IntPtr XInternAtom(IntPtr display, [MarshalAs(UnmanagedType.LPStr)] string name, bool onlyIfExists);
		[DllImport(X11)] internal static extern IntPtr XGetAtomName(IntPtr display, IntPtr atom);
		[DllImport(X11)] internal static extern int XFree(IntPtr data);
		[DllImport(X11)] internal static extern int XFlush(IntPtr display);
		[DllImport(X11)] internal static extern int XSync(IntPtr display, bool discard);
		[DllImport(X11)] internal static extern int XPending(IntPtr display);
		[DllImport(X11)] internal static extern int XConnectionNumber(IntPtr display);
		[DllImport(X11)] internal static extern int XNextEvent(IntPtr display, IntPtr eventReturn);
		[DllImport(X11)] internal static extern int XSelectInput(IntPtr display, IntPtr window, IntPtr mask);
		[DllImport(X11)] internal static extern int XMapWindow(IntPtr display, IntPtr window);
		[DllImport(X11)] internal static extern int XUnmapWindow(IntPtr display, IntPtr window);
		[DllImport(X11)] internal static extern int XDestroyWindow(IntPtr display, IntPtr window);
		[DllImport(X11)] internal static extern int XDisplayWidth(IntPtr display, int screen);
		[DllImport(X11)] internal static extern int XDisplayHeight(IntPtr display, int screen);
		[DllImport(X11)] internal static extern int XDefaultScreen(IntPtr display);
		[DllImport(X11)] internal static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attributes);
		[DllImport(X11)] internal static extern int XQueryTree(IntPtr display, IntPtr window, out IntPtr root, out IntPtr parent, out IntPtr children, out uint count);
		[DllImport(X11)] internal static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate, IntPtr mask, IntPtr ev);
		[DllImport(X11)] internal static extern int XQueryPointer(IntPtr display, IntPtr window, out IntPtr root, out IntPtr child, out int rootX, out int rootY, out int winX, out int winY, out uint mask);
		[DllImport(X11)] internal static extern IntPtr XCreateSimpleWindow(IntPtr display, IntPtr parent, int x, int y, uint width, uint height, uint borderWidth, IntPtr border, IntPtr background);
		[DllImport(X11)] internal static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type, int format, int mode, IntPtr data, int count);
		[DllImport(X11)] internal static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr offset, IntPtr length, bool delete, IntPtr reqType, out IntPtr actualType, out int actualFormat, out IntPtr itemCount, out IntPtr bytesAfter, out IntPtr prop);
		[DllImport(X11)] internal static extern int XStoreName(IntPtr display, IntPtr window, [MarshalAs(UnmanagedType.LPStr)] string name);
		[DllImport(X11)] internal static extern int XSetSelectionOwner(IntPtr display, IntPtr selection, IntPtr owner, IntPtr time);
	}
}
