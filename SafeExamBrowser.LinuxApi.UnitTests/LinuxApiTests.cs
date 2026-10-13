using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SafeExamBrowser.LinuxApi;
using SafeExamBrowser.LinuxApi.Processes;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.WindowsApi.Contracts;

namespace SafeExamBrowser.LinuxApi.UnitTests
{
	[TestClass]
	public class LinuxApiTests
	{
		private static Mock<IModuleLogger> Logger()
		{
			var logger = new Mock<IModuleLogger>();

			logger.Setup(l => l.CloneFor(It.IsAny<string>())).Returns(logger.Object);

			return logger;
		}

		[TestMethod]
		public void PackageSignatureMustBeStableAndLowerCaseHex()
		{
			var a = PackageSignature.Hash("safeexambrowser");
			var b = PackageSignature.Hash("safeexambrowser");

			Assert.AreEqual(a, b);
			Assert.AreEqual(40, a.Length);
			Assert.AreEqual(a.ToLower(), a);
			Assert.AreNotEqual(a, PackageSignature.Hash("other"));
		}

		[TestMethod]
		public void PackageSignatureMustResolveShell()
		{
			// /bin/sh is always owned by a package on Debian (dash)
			var signature = PackageSignature.Compute("/bin/sh");

			Assert.IsNotNull(signature);
		}

		[TestMethod]
		public void ProcessFactoryMustStartAndTerminateProcess()
		{
			var sut = new ProcessFactory(Logger().Object);
			var process = sut.StartNew("/bin/sleep", "30");

			Assert.AreEqual("sleep", process.Name);
			Assert.IsFalse(process.HasTerminated);
			Assert.IsTrue(sut.TryGetById(process.Id, out var found));
			Assert.AreEqual(process.Id, found.Id);
			Assert.IsTrue(process.TryKill(2000));
			Assert.IsTrue(process.HasTerminated);
		}

		[TestMethod]
		public void ProcessFactoryMustListRunningProcesses()
		{
			var sut = new ProcessFactory(Logger().Object);
			var all = sut.GetAllRunning();

			Assert.IsTrue(all.Count > 0);
			Assert.IsTrue(System.Linq.Enumerable.Any(all, p => p.Id == Environment.ProcessId));
		}

		[TestMethod]
		public void NativeMethodsMustNotFailWithoutDisplay()
		{
			var sut = new NativeMethods();

			Assert.IsNotNull(sut.GetAllWindows());
			Assert.IsNotNull(sut.GetInteractiveWindows());
			Assert.IsNotNull(sut.GetWorkingArea());
		}

		[TestMethod]
		public void NativeMethodsMustQueryX11WhenAvailable()
		{
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
			{
				Assert.Inconclusive("No X server available.");
			}

			var sut = new NativeMethods();
			var area = sut.GetWorkingArea();
			var (x, y) = sut.GetCursorPosition();

			Console.WriteLine($"Work area: {area.Left},{area.Top} - {area.Right},{area.Bottom}; cursor: {x},{y}");
			Console.WriteLine($"Shell pid: {sut.GetShellProcessId()}");

			foreach (var window in sut.GetInteractiveWindows())
			{
				Console.WriteLine($"Window {window}: '{sut.GetWindowTitle(window)}' pid={sut.GetProcessIdFor(window)} minimized={sut.IsMinimizedWindow(window)} visible={sut.GetWindowStyle(window).IsVisible}");
			}

			Assert.IsTrue(area.Right > area.Left);
		}
	}
}
