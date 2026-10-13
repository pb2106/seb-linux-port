/*
 * Copyright (c) 2025 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Runtime.InteropServices;

namespace SafeExamBrowser.LinuxApi.Native
{
	internal static class LibC
	{
		internal const int O_RDONLY = 0;
		internal const int O_WRONLY = 1;
		internal const int O_RDWR = 2;
		internal const int O_NONBLOCK = 0x800;
		internal const int POLLIN = 1;
		internal const int SIGTERM = 15;
		internal const int SIGKILL = 9;
		internal const int SIGSTOP = 19;
		internal const int SIGCONT = 18;
		internal const int PTRACE_SEIZE = 0x4206;
		internal const int PTRACE_INTERRUPT = 0x4207;
		internal const int PTRACE_DETACH = 17;
		internal const int __WALL = 0x40000000;

		[StructLayout(LayoutKind.Sequential)]
		internal struct PollFd
		{
			public int fd;
			public short events;
			public short revents;
		}

		[DllImport("libc", SetLastError = true)]
		internal static extern int open([MarshalAs(UnmanagedType.LPStr)] string path, int flags);

		[DllImport("libc", SetLastError = true)]
		internal static extern int close(int fd);

		[DllImport("libc", SetLastError = true)]
		internal static extern IntPtr read(int fd, byte[] buffer, UIntPtr count);

		[DllImport("libc", SetLastError = true)]
		internal static extern IntPtr write(int fd, byte[] buffer, UIntPtr count);

		[DllImport("libc", SetLastError = true)]
		internal static extern int ioctl(int fd, ulong request, byte[] data);

		[DllImport("libc", SetLastError = true)]
		internal static extern int ioctl(int fd, ulong request, int value);

		[DllImport("libc", SetLastError = true)]
		internal static extern int poll([In, Out] PollFd[] fds, ulong nfds, int timeout);

		[DllImport("libc", SetLastError = true)]
		internal static extern int kill(int pid, int signal);

		[DllImport("libc", SetLastError = true)]
		internal static extern long ptrace(long request, int pid, IntPtr addr, IntPtr data);

		[DllImport("libc", SetLastError = true)]
		internal static extern int waitpid(int pid, out int status, int options);

		[DllImport("libc", SetLastError = true)]
		internal static extern uint geteuid();

		[DllImport("libc", SetLastError = true)]
		internal static extern int setenv([MarshalAs(UnmanagedType.LPStr)] string name, [MarshalAs(UnmanagedType.LPStr)] string value, int overwrite);

		[DllImport("libc", SetLastError = true)]
		internal static extern int unsetenv([MarshalAs(UnmanagedType.LPStr)] string name);

		// _IOC(dir, type, nr, size) as defined in <asm-generic/ioctl.h>
		internal static ulong IOC(uint dir, uint type, uint nr, uint size)
		{
			return ((ulong) dir << 30) | ((ulong) size << 16) | ((ulong) type << 8) | nr;
		}

		internal static ulong IOR(char type, uint nr, uint size) => IOC(2, type, nr, size);
		internal static ulong IOW(char type, uint nr, uint size) => IOC(1, type, nr, size);
		internal static ulong IO(char type, uint nr) => IOC(0, type, nr, 0);
	}
}
