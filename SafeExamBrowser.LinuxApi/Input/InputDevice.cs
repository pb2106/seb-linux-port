/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SafeExamBrowser.LinuxApi.Native;

namespace SafeExamBrowser.LinuxApi.Input
{
	internal enum InputDeviceKind
	{
		Keyboard,
		Pointer,
		Touch
	}

	/// <summary>
	/// A physical evdev input device which is exclusively grabbed (EVIOCGRAB) and whose events are filtered before being
	/// re-injected through a cloned uinput device. This is the Linux counterpart of a Windows low-level hook: it works on
	/// X11, Wayland and the console, and allows to block individual events.
	/// </summary>
	internal class InputDevice : IDisposable
	{
		private const int EV_SYN = 0, EV_KEY = 1, EV_REL = 2, EV_ABS = 3, EV_MSC = 4, EV_SW = 5, EV_LED = 17, EV_SND = 18;
		private const int BTN_LEFT = 0x110, BTN_TOUCH = 0x14a, BTN_TOOL_PEN = 0x140;
		private const int INPUT_EVENT_SIZE = 24;
		internal const string VirtualPrefix = "SEB Virtual ";

		private static readonly ulong EVIOCGRAB = LibC.IOW('E', 0x90, 4);
		private static readonly ulong EVIOCGID = LibC.IOR('E', 0x02, 8);
		private static readonly ulong UI_DEV_CREATE = LibC.IO('U', 1);
		private static readonly ulong UI_DEV_DESTROY = LibC.IO('U', 2);
		private static readonly ulong UI_DEV_SETUP = LibC.IOW('U', 3, 92);
		private static readonly ulong UI_ABS_SETUP = LibC.IOW('U', 4, 28);

		private int fd = -1;
		private int uinput = -1;
		private bool grabbed;
		private readonly HashSet<int> forwarded = new HashSet<int>();
		private readonly HashSet<int> blocked = new HashSet<int>();

		internal string Path { get; }
		internal string Name { get; private set; }
		internal int FileDescriptor => fd;
		internal bool IsVirtual { get; private set; }
		internal InputDeviceKind Kind { get; private set; }
		internal bool IsGrabbed => grabbed;
		internal int AbsMaxX { get; private set; } = 1;
		internal int AbsMaxY { get; private set; } = 1;

		private InputDevice(string path)
		{
			Path = path;
		}

		/// <summary>
		/// Opens and classifies the device. Returns false for devices which are not relevant (e.g. SEB's own virtual devices).
		/// </summary>
		internal static bool TryOpen(string path, out InputDevice device)
		{
			device = new InputDevice(path);
			device.fd = LibC.open(path, LibC.O_RDWR | LibC.O_NONBLOCK);

			if (device.fd < 0)
			{
				device.fd = LibC.open(path, LibC.O_RDONLY | LibC.O_NONBLOCK);
			}

			if (device.fd < 0 || !device.Classify())
			{
				device.Dispose();
				device = default;

				return false;
			}

			return true;
		}

		internal bool Grab()
		{
			if (grabbed)
			{
				return true;
			}

			if (Kind == InputDeviceKind.Touch)
			{
				return false;
			}

			if (!CreateClone())
			{
				return false;
			}

			if (Kind == InputDeviceKind.Keyboard)
			{
				WaitForKeyRelease();
			}

			grabbed = LibC.ioctl(fd, EVIOCGRAB, 1) == 0;

			return grabbed;
		}

		internal void Ungrab()
		{
			if (!grabbed)
			{
				return;
			}

			LibC.ioctl(fd, EVIOCGRAB, 0);
			grabbed = false;

			foreach (var code in forwarded.ToList())
			{
				Write(EV_KEY, code, 0);
			}

			Write(EV_SYN, 0, 0);
			forwarded.Clear();
			blocked.Clear();

			if (uinput >= 0)
			{
				LibC.ioctl(uinput, UI_DEV_DESTROY, 0);
				LibC.close(uinput);
				uinput = -1;
			}
		}

		/// <summary>
		/// Reads all pending events and returns them as (type, code, value) triples.
		/// </summary>
		internal List<(int type, int code, int value)> ReadEvents()
		{
			var events = new List<(int, int, int)>();
			var buffer = new byte[INPUT_EVENT_SIZE * 64];

			while (true)
			{
				var count = (long) LibC.read(fd, buffer, (UIntPtr) buffer.Length);

				if (count <= 0)
				{
					break;
				}

				for (var offset = 0; offset + INPUT_EVENT_SIZE <= count; offset += INPUT_EVENT_SIZE)
				{
					events.Add((BitConverter.ToUInt16(buffer, offset + 16), BitConverter.ToUInt16(buffer, offset + 18), BitConverter.ToInt32(buffer, offset + 20)));
				}
			}

			return events;
		}

		/// <summary>
		/// Forwards an event to the clone device. Key releases of keys whose press was forwarded are always forwarded to avoid stuck keys.
		/// </summary>
		internal void Forward(int type, int code, int value)
		{
			if (type == EV_KEY)
			{
				if (value == 0 && blocked.Remove(code))
				{
					return;
				}

				if (value == 0)
				{
					forwarded.Remove(code);
				}
				else if (value == 1)
				{
					forwarded.Add(code);
				}
			}

			Write(type, code, value);
		}

		/// <summary>
		/// Marks a key as blocked, i.e. the press (and the subsequent repeats and the release) are dropped.
		/// </summary>
		internal void Block(int code, int value)
		{
			if (value == 1)
			{
				blocked.Add(code);
			}
		}

		internal bool IsBlocked(int code)
		{
			return blocked.Contains(code);
		}

		internal bool WasForwarded(int code)
		{
			return forwarded.Contains(code);
		}

		private void Write(int type, int code, int value)
		{
			if (uinput < 0)
			{
				return;
			}

			var buffer = new byte[INPUT_EVENT_SIZE];

			BitConverter.GetBytes((ushort) type).CopyTo(buffer, 16);
			BitConverter.GetBytes((ushort) code).CopyTo(buffer, 18);
			BitConverter.GetBytes(value).CopyTo(buffer, 20);
			LibC.write(uinput, buffer, (UIntPtr) buffer.Length);
		}

		private bool Classify()
		{
			var name = new byte[256];

			if (LibC.ioctl(fd, LibC.IOC(2, 'E', 0x06, 256), name) >= 0)
			{
				Name = Encoding.UTF8.GetString(name).TrimEnd('\0');
			}

			if (Name != null && Name.StartsWith(VirtualPrefix))
			{
				return false;
			}

			var id = new byte[8];

			if (LibC.ioctl(fd, EVIOCGID, id) >= 0)
			{
				IsVirtual = BitConverter.ToUInt16(id, 0) == 0x06; // BUS_VIRTUAL
			}

			var types = Bits(0, 4);
			var keys = Bits(EV_KEY, 96);
			var hasKeys = Has(types, EV_KEY);
			var hasRel = Has(types, EV_REL);
			var hasAbs = Has(types, EV_ABS);

			if (hasKeys && Has(keys, 30) && Has(keys, 28))
			{
				Kind = InputDeviceKind.Keyboard;
			}
			else if (hasKeys && hasAbs && Has(keys, BTN_TOUCH) && !Has(keys, BTN_LEFT) && !Has(keys, BTN_TOOL_PEN))
			{
				Kind = InputDeviceKind.Touch;
				LoadAbsRange();
			}
			else if (hasKeys && Has(keys, BTN_LEFT) && (hasRel || hasAbs))
			{
				Kind = InputDeviceKind.Pointer;
			}
			else
			{
				return false;
			}

			return true;
		}

		private void LoadAbsRange()
		{
			var info = new byte[24];

			if (LibC.ioctl(fd, LibC.IOR('E', 0x40 + 0, 24), info) >= 0)
			{
				AbsMaxX = Math.Max(1, BitConverter.ToInt32(info, 8));
			}

			if (LibC.ioctl(fd, LibC.IOR('E', 0x40 + 1, 24), info) >= 0)
			{
				AbsMaxY = Math.Max(1, BitConverter.ToInt32(info, 8));
			}
		}

		private byte[] Bits(int type, int size)
		{
			var bits = new byte[size];

			LibC.ioctl(fd, LibC.IOC(2, 'E', (uint) (0x20 + type), (uint) size), bits);

			return bits;
		}

		private static bool Has(byte[] bits, int bit)
		{
			return bit / 8 < bits.Length && (bits[bit / 8] & (1 << (bit % 8))) != 0;
		}

		private void WaitForKeyRelease()
		{
			var started = DateTime.Now;
			var state = new byte[96];

			while (DateTime.Now - started < TimeSpan.FromSeconds(3))
			{
				LibC.ioctl(fd, LibC.IOC(2, 'E', 0x18, 96), state);

				if (state.All(b => b == 0))
				{
					return;
				}

				System.Threading.Thread.Sleep(20);
			}
		}

		private bool CreateClone()
		{
			uinput = LibC.open("/dev/uinput", LibC.O_WRONLY | LibC.O_NONBLOCK);

			if (uinput < 0)
			{
				uinput = LibC.open("/dev/input/uinput", LibC.O_WRONLY | LibC.O_NONBLOCK);
			}

			if (uinput < 0)
			{
				return false;
			}

			var setupBit = new Dictionary<int, (int ev, int size, uint request)>
			{
				{ EV_KEY, (EV_KEY, 96, 101) }, { EV_REL, (EV_REL, 2, 102) }, { EV_ABS, (EV_ABS, 8, 103) },
				{ EV_MSC, (EV_MSC, 1, 104) }, { EV_LED, (EV_LED, 1, 105) }, { EV_SND, (EV_SND, 1, 106) }, { EV_SW, (EV_SW, 2, 109) }
			};
			var types = Bits(0, 4);

			foreach (var (ev, size, request) in setupBit.Values)
			{
				if (!Has(types, ev))
				{
					continue;
				}

				LibC.ioctl(uinput, LibC.IOW('U', 100, 4), ev);

				var bits = Bits(ev, size);

				for (var code = 0; code < size * 8; code++)
				{
					if (Has(bits, code))
					{
						LibC.ioctl(uinput, LibC.IOW('U', request, 4), code);

						if (ev == EV_ABS)
						{
							var info = new byte[24];
							var setup = new byte[28];

							LibC.ioctl(fd, LibC.IOR('E', (uint) (0x40 + code), 24), info);
							BitConverter.GetBytes((ushort) code).CopyTo(setup, 0);
							info.CopyTo(setup, 4);
							LibC.ioctl(uinput, UI_ABS_SETUP, setup);
						}
					}
				}
			}

			var props = new byte[4];

			if (LibC.ioctl(fd, LibC.IOC(2, 'E', 0x09, 4), props) >= 0)
			{
				for (var prop = 0; prop < 32; prop++)
				{
					if (Has(props, prop))
					{
						LibC.ioctl(uinput, LibC.IOW('U', 110, 4), prop);
					}
				}
			}

			var dev = new byte[92];
			var cloneName = Encoding.UTF8.GetBytes(VirtualPrefix + (Name ?? "Device"));

			BitConverter.GetBytes((ushort) 0x06).CopyTo(dev, 0); // BUS_VIRTUAL
			BitConverter.GetBytes((ushort) 0x5EB).CopyTo(dev, 2);
			BitConverter.GetBytes((ushort) 1).CopyTo(dev, 4);
			Array.Copy(cloneName, 0, dev, 8, Math.Min(cloneName.Length, 79));

			if (LibC.ioctl(uinput, UI_DEV_SETUP, dev) < 0 || LibC.ioctl(uinput, UI_DEV_CREATE, 0) < 0)
			{
				LibC.close(uinput);
				uinput = -1;

				return false;
			}

			// Give the input stack some time to pick up the clone before the original device gets grabbed.
			System.Threading.Thread.Sleep(250);

			return true;
		}

		public void Dispose()
		{
			Ungrab();

			if (fd >= 0)
			{
				LibC.close(fd);
				fd = -1;
			}
		}
	}
}
