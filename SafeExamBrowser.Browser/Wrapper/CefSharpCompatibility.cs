/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace System.Windows.Forms
{
	public interface IWin32Window
	{
		IntPtr Handle { get; }
	}

	[Flags]
	public enum Keys
	{
		None = 0,
		Tab = 9,
		Home = 36,
		D0 = 48,
		D1 = 49,
		F = 70,
		L = 76,
		NumPad0 = 96,
		Add = 107,
		Subtract = 109,
		F5 = 116,
		Oemplus = 187,
		OemMinus = 189
	}
}

namespace Syroot.Windows.IO
{
	public class KnownFolder
	{
		public string Path { get; set; }
		public string ExpandedPath { get; set; }
	}

	public static class KnownFolders
	{
		public static KnownFolder Downloads
		{
			get
			{
				var xdg = Environment.GetEnvironmentVariable("XDG_DOWNLOAD_DIR");
				if (!string.IsNullOrWhiteSpace(xdg))
				{
					return new KnownFolder { Path = xdg, ExpandedPath = xdg };
				}

				var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				var path = !string.IsNullOrWhiteSpace(home)
					? Path.Combine(home, "Downloads")
					: Path.GetTempPath();

				return new KnownFolder { Path = path, ExpandedPath = path };
			}
		}
	}
}

namespace CefSharp.Enums
{
	public enum CursorType
	{
		Pointer = 0
	}

	[Flags]
	public enum DragOperationsMask : uint
	{
		None = 0,
		Copy = 1,
		Link = 2,
		Generic = 4,
		Private = 8,
		Move = 16,
		Delete = 32,
		Every = uint.MaxValue
	}
}

namespace CefSharp.Structs
{
	public struct CursorInfo
	{
	}

	public struct Range
	{
		public int From { get; set; }
		public int To { get; set; }

		public Range(int from, int to)
		{
			From = from;
			To = to;
		}
	}

	public struct Size
	{
		public int Width { get; set; }
		public int Height { get; set; }

		public Size(int width, int height)
		{
			Width = width;
			Height = height;
		}
	}
}

namespace CefSharp
{
	using CefSharp.Enums;
	using CefSharp.Structs;

	[Flags]
	public enum CefEventFlags : uint
	{
		None = 0,
		CapsLockOn = 1 << 0,
		ShiftDown = 1 << 1,
		ControlDown = 1 << 2,
		AltDown = 1 << 3,
		LeftMouseButton = 1 << 4,
		MiddleMouseButton = 1 << 5,
		RightMouseButton = 1 << 6,
		CommandDown = 1 << 7,
		NumLockOn = 1 << 8,
		IsKeyPad = 1 << 9,
		IsLeft = 1 << 10,
		IsRight = 1 << 11
	}

	public enum CefErrorCode
	{
		None = 0,
		Aborted = -3,
		InternetDisconnected = -106,
		UnknownUrlScheme = -302
	}

	[Flags]
	public enum CefFileDialogMode
	{
		Open = 0,
		OpenMultiple = 1,
		OpenFolder = 2,
		Save = 3
	}

	public enum CefFocusSource
	{
		FocusSourceNavigation = 0,
		FocusSourceSystem = 1
	}

	public enum CefJsDialogType
	{
		Alert = 0,
		Confirm = 1,
		Prompt = 2
	}

	public enum CefMenuCommand
	{
		NotFound = -1,
		Back = 100,
		Forward = 101,
		Reload = 102
	}

	public enum CefReturnValue
	{
		Cancel = 0,
		Continue = 1,
		ContinueAsync = 2
	}

	public enum KeyType
	{
		RawKeyDown = 0,
		KeyDown = 1,
		KeyUp = 2,
		Char = 3
	}

	public enum LogSeverity
	{
		Default = 0,
		Verbose = 1,
		Info = 2,
		Warning = 3,
		Error = 4,
		Fatal = 5,
		Disable = 99
	}

	public enum ResourceType
	{
		MainFrame = 0,
		SubFrame = 1,
		Stylesheet = 2,
		Script = 3,
		Image = 4,
		FontResource = 5,
		SubResource = 6,
		Object = 7,
		Media = 8,
		Worker = 9,
		SharedWorker = 10,
		Prefetch = 11,
		Favicon = 12,
		Xhr = 13,
		Ping = 14,
		ServiceWorker = 15,
		CspReport = 16,
		PluginResource = 17
	}

	[Flags]
	public enum TransitionType : uint
	{
		Link = 0,
		Explicit = 1
	}

	public enum UrlRequestStatus
	{
		Unknown = 0,
		Success = 1,
		IoPending = 2,
		Canceled = 3,
		Failed = 4
	}

	public enum WindowOpenDisposition
	{
		Unknown = 0,
		CurrentTab = 1,
		SingletonTab = 2,
		NewForegroundTab = 3,
		NewBackgroundTab = 4,
		NewPopup = 5,
		NewWindow = 6,
		SaveToDisk = 7,
		OffTheRecord = 8,
		IgnoreAction = 9
	}

	public class DraggableRegion
	{
		public int X { get; set; }
		public int Y { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public bool Draggable { get; set; }
	}

	public class Cookie
	{
		public string Name { get; set; }
		public string Value { get; set; }
		public string Domain { get; set; }
		public string Path { get; set; }
		public bool Secure { get; set; }
		public bool HttpOnly { get; set; }
		public DateTime? Expires { get; set; }
		public DateTime Creation { get; set; }
		public DateTime LastAccess { get; set; }
	}

	public class DownloadItem
	{
		public int Id { get; set; }
		public bool IsInProgress { get; set; }
		public bool IsComplete { get; set; }
		public bool IsCancelled { get; set; }
		public long CurrentSpeed { get; set; }
		public int PercentComplete { get; set; }
		public long TotalBytes { get; set; }
		public long ReceivedBytes { get; set; }
		public DateTime? StartTime { get; set; }
		public DateTime? EndTime { get; set; }
		public string FullPath { get; set; }
		public string Url { get; set; }
		public string OriginalUrl { get; set; }
		public string SuggestedFileName { get; set; }
		public string ContentDisposition { get; set; }
		public string MimeType { get; set; }
	}

	public class JavascriptResponse
	{
		public string Message { get; set; }
		public object Result { get; set; }
		public bool Success { get; set; }
	}

	public class JavascriptException
	{
		public string Message { get; set; }
		public JavascriptStackFrame[] StackTrace { get; set; }
	}

	public class JavascriptStackFrame
	{
		public string FunctionName { get; set; }
		public int LineNumber { get; set; }
		public int ColumnNumber { get; set; }
		public string SourceName { get; set; }
	}

	public class JavascriptMessageReceivedEventArgs : EventArgs
	{
		public IBrowser Browser { get; set; }
		public IFrame Frame { get; set; }
		public object Message { get; set; }

		public T ConvertMessageTo<T>()
		{
			if (Message is T typed)
			{
				return typed;
			}

			if (Message != null)
			{
				var json = Newtonsoft.Json.JsonConvert.SerializeObject(Message);
				return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
			}

			return default;
		}
	}

	public class AddressChangedEventArgs : EventArgs
	{
		public IBrowser Browser { get; }
		public string Address { get; }

		public AddressChangedEventArgs(IBrowser browser, string address)
		{
			Browser = browser;
			Address = address;
		}
	}

	public class ConsoleMessageEventArgs : EventArgs
	{
		public LogSeverity Level { get; }
		public string Message { get; }
		public string Source { get; }
		public int Line { get; }

		public ConsoleMessageEventArgs(LogSeverity level, string message, string source, int line)
		{
			Level = level;
			Message = message;
			Source = source;
			Line = line;
		}
	}

	public class LoadErrorEventArgs : EventArgs
	{
		public IBrowser Browser { get; }
		public IFrame Frame { get; }
		public CefErrorCode ErrorCode { get; }
		public string ErrorText { get; }
		public string FailedUrl { get; }

		public LoadErrorEventArgs(IBrowser browser, IFrame frame, CefErrorCode errorCode, string errorText, string failedUrl)
		{
			Browser = browser;
			Frame = frame;
			ErrorCode = errorCode;
			ErrorText = errorText;
			FailedUrl = failedUrl;
		}
	}

	public class LoadingStateChangedEventArgs : EventArgs
	{
		public IBrowser Browser { get; }
		public bool CanGoBack { get; }
		public bool CanGoForward { get; }
		public bool CanReload { get; }
		public bool IsLoading { get; }

		public LoadingStateChangedEventArgs(IBrowser browser, bool canGoBack, bool canGoForward, bool isLoading)
		{
			Browser = browser;
			CanGoBack = canGoBack;
			CanGoForward = canGoForward;
			CanReload = !isLoading;
			IsLoading = isLoading;
		}
	}

	public class StatusMessageEventArgs : EventArgs
	{
		public IBrowser Browser { get; }
		public string Value { get; }

		public StatusMessageEventArgs(IBrowser browser, string value)
		{
			Browser = browser;
			Value = value;
		}
	}

	public class TitleChangedEventArgs : EventArgs
	{
		public IBrowser Browser { get; }
		public string Title { get; }

		public TitleChangedEventArgs(IBrowser browser, string title)
		{
			Browser = browser;
			Title = title;
		}
	}

	public interface IApp
	{
	}

	public interface IDomNode
	{
	}

	public interface IDragData : IDisposable
	{
		bool IsFragment { get; set; }
		bool IsFile { get; set; }
		bool IsLink { get; set; }
	}

	public interface IAuthCallback : IDisposable
	{
		void Continue(string username, string password);
		void Cancel();
	}

	public interface IBeforeDownloadCallback : IDisposable
	{
		void Continue(string downloadPath, bool showDialog);
	}

	public interface IDownloadItemCallback : IDisposable
	{
		void Cancel();
		void Pause();
		void Resume();
	}

	public interface IFileDialogCallback : IDisposable
	{
		void Continue(List<string> filePaths);
		void Cancel();
	}

	public interface IJsDialogCallback : IDisposable
	{
		void Continue(bool success, string userInput = null);
	}

	public interface IRequestCallback : IDisposable
	{
		void Continue(bool allow);
		void Cancel();
	}

	public interface IRunContextMenuCallback : IDisposable
	{
		void Continue(CefMenuCommand commandId, CefEventFlags eventFlags);
		void Cancel();
	}

	public interface IContextMenuParams
	{
	}

	public interface IMenuModel
	{
		bool Clear();
	}

	public interface IFrame : IDisposable
	{
		bool IsMain { get; }
		string Url { get; }
		void ExecuteJavaScriptAsync(string code, string scriptUrl = "about:blank", int startLine = 1);
		Task<JavascriptResponse> EvaluateScriptAsync(string script, string scriptUrl = "about:blank", int startLine = 1, TimeSpan? timeout = null, bool useImmediatelyInvokedFuncExpression = false);
		void LoadUrl(string url);
	}

	public interface IBrowserHost : IDisposable
	{
		void CloseDevTools();
		void Find(string searchText, bool forward, bool matchCase, bool findNext);
		void SetFocus(bool focus);
		void ShowDevTools(IWindowInfo windowInfo = null, int inspectElementAtX = 0, int inspectElementAtY = 0);
		void StartDownload(string url);
		void StopFinding(bool clearSelection);
	}

	public interface IWindowInfo : IDisposable
	{
	}

	public interface IBrowser : IDisposable
	{
		bool CanGoBack { get; }
		bool CanGoForward { get; }
		bool IsDisposed { get; }
		bool IsLoading { get; }
		bool IsPopup { get; }
		IFrame MainFrame { get; }
		int Identifier { get; }

		Task<JavascriptResponse> EvaluateScriptAsync(string script, TimeSpan? timeout = null, bool useImmediatelyInvokedFuncExpression = false);
		IBrowserHost GetHost();
		List<long> GetFrameIdentifiers();
		IFrame GetFrame(long identifier);
		void GoBack();
		void GoForward();
		void Reload(bool ignoreCache = false);
		void SetZoomLevel(double zoomLevel);
		void ShowDevTools(IWindowInfo windowInfo = null, int inspectElementAtX = 0, int inspectElementAtY = 0);
	}

	public interface IRequest : IDisposable
	{
		NameValueCollection Headers { get; set; }
		ResourceType ResourceType { get; }
		TransitionType TransitionType { get; }
		string Url { get; set; }
		string Method { get; }
	}

	public interface IResponse : IDisposable
	{
		NameValueCollection Headers { get; set; }
		int StatusCode { get; set; }
		string MimeType { get; set; }
	}

	public interface IResourceHandler : IDisposable
	{
	}

	public class ResourceHandler : IResourceHandler
	{
		public string Content { get; set; }
		public Encoding Encoding { get; set; }
		public string MimeType { get; set; }

		public static IResourceHandler FromString(string text, Encoding encoding = null, bool includePreamble = true, string mimeType = "text/html")
		{
			return new ResourceHandler
			{
				Content = text,
				Encoding = encoding ?? Encoding.UTF8,
				MimeType = mimeType
			};
		}

		public void Dispose()
		{
		}
	}

	public interface IResourceRequestHandler : IDisposable
	{
		IResourceHandler GetResourceHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request);
		CefReturnValue OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback);
		bool OnProtocolExecution(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request);
		void OnResourceRedirect(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl);
		bool OnResourceResponse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response);
	}

	public interface IContextMenuHandler
	{
		void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model);
		bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags);
		void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame);
		bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback);
	}

	public interface IDialogHandler
	{
		bool OnFileDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFileDialogMode mode, string title, string defaultFilePath, IReadOnlyCollection<string> acceptFilters, IReadOnlyCollection<string> acceptExtensions, IReadOnlyCollection<string> acceptDescriptions, IFileDialogCallback callback);
	}

	public interface IDisplayHandler
	{
		void OnAddressChanged(IWebBrowser chromiumWebBrowser, AddressChangedEventArgs addressChangedArgs);
		bool OnAutoResize(IWebBrowser chromiumWebBrowser, IBrowser browser, Size newSize);
		bool OnConsoleMessage(IWebBrowser chromiumWebBrowser, ConsoleMessageEventArgs consoleMessageArgs);
		bool OnCursorChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr cursor, CursorType type, CursorInfo customCursorInfo);
		void OnFaviconUrlChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IList<string> urls);
		void OnFullscreenModeChange(IWebBrowser chromiumWebBrowser, IBrowser browser, bool fullscreen);
		void OnLoadingProgressChange(IWebBrowser chromiumWebBrowser, IBrowser browser, double progress);
		void OnStatusMessage(IWebBrowser chromiumWebBrowser, StatusMessageEventArgs statusMessageArgs);
		void OnTitleChanged(IWebBrowser chromiumWebBrowser, TitleChangedEventArgs titleChangedArgs);
		bool OnTooltipChanged(IWebBrowser chromiumWebBrowser, ref string text);
	}

	public interface IDownloadHandler
	{
		bool CanDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, string url, string requestMethod);
		bool OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback);
		void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback);
	}

	public interface IDragHandler
	{
		bool OnDragEnter(IWebBrowser chromiumWebBrowser, IBrowser browser, IDragData dragData, DragOperationsMask mask);
		void OnDraggableRegionsChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IList<DraggableRegion> regions);
	}

	public interface IFocusHandler
	{
		void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser);
		bool OnSetFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFocusSource source);
		void OnTakeFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, bool next);
	}

	public interface IJsDialogHandler
	{
		bool OnBeforeUnloadDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback);
		void OnDialogClosed(IWebBrowser chromiumWebBrowser, IBrowser browser);
		bool OnJSDialog(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage);
		void OnResetDialogState(IWebBrowser chromiumWebBrowser, IBrowser browser);
	}

	public interface IKeyboardHandler
	{
		bool OnKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey);
		bool OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut);
	}

	public interface ILifeSpanHandler
	{
	}

	public interface IRenderProcessMessageHandler
	{
		void OnContextCreated(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame);
		void OnContextReleased(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame);
		void OnFocusedNodeChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IDomNode node);
		void OnUncaughtException(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, JavascriptException exception);
	}

	public interface IRequestHandler
	{
		bool GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback);
		IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling);
		bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect);
		bool OnOpenUrlFromTab(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture);
	}

	public interface ICookieVisitor : IDisposable
	{
		bool Visit(Cookie cookie, int count, int total, ref bool deleteCookie);
	}

	public interface IDeleteCookiesCallback : IDisposable
	{
		void OnComplete(int numDeleted);
	}

	public class TaskDeleteCookiesCallback : IDeleteCookiesCallback
	{
		public const int InvalidNoOfCookiesDeleted = -1;
		private readonly TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

		public Task<int> Task => tcs.Task;

		public void OnComplete(int numDeleted)
		{
			tcs.TrySetResult(numDeleted);
		}

		public void Dispose()
		{
		}
	}

	public interface ICookieManager
	{
		bool DeleteCookies(string url = null, string name = null, IDeleteCookiesCallback callback = null);
		bool VisitAllCookies(ICookieVisitor visitor);
	}

	internal class DefaultCookieManager : ICookieManager
	{
		public bool DeleteCookies(string url = null, string name = null, IDeleteCookiesCallback callback = null)
		{
			callback?.OnComplete(0);
			return true;
		}

		public bool VisitAllCookies(ICookieVisitor visitor)
		{
			return true;
		}
	}

	public interface IRequestContext : IDisposable
	{
		bool SetPreference(string name, object value, out string error);
	}

	internal class DefaultRequestContext : IRequestContext
	{
		public bool SetPreference(string name, object value, out string error)
		{
			error = null;
			return true;
		}

		public void Dispose()
		{
		}
	}

	public class CommandLineArgDictionary : Dictionary<string, string>
	{
		public void Add(string arg)
		{
			this[arg] = string.Empty;
		}
	}

	public class CefSettingsBase
	{
		public string AcceptLanguageList { get; set; }
		public string CachePath { get; set; }
		public CommandLineArgDictionary CefCommandLineArgs { get; } = new CommandLineArgDictionary();
		public string LogFile { get; set; }
		public LogSeverity LogSeverity { get; set; }
		public bool PersistSessionCookies { get; set; }
		public string UserAgent { get; set; }
	}

	public static class Cef
	{
		public static string ChromiumVersion => "120.0.0.0";
		public static string CefVersion => "120.0.0";
		public static string CefSharpVersion => "120.0.0";
		public static TaskFactory UIThreadTaskFactory { get; } = Task.Factory;

		public static bool Initialize(CefSettingsBase settings, bool performDependencyCheck = true, IApp cefApp = null)
		{
			return true;
		}

		public static int GetExitCode()
		{
			return 0;
		}

		public static ICookieManager GetGlobalCookieManager()
		{
			return new DefaultCookieManager();
		}

		public static IRequestContext GetGlobalRequestContext()
		{
			return new DefaultRequestContext();
		}

		public static void Shutdown()
		{
		}
	}

	public interface IChromiumWebBrowserBase : IDisposable
	{
		IBrowser BrowserCore { get; }
		bool IsBrowserInitialized { get; }
		bool IsDisposed { get; }
		event EventHandler<ConsoleMessageEventArgs> ConsoleMessage;
		event EventHandler<LoadingStateChangedEventArgs> LoadingStateChanged;
		event EventHandler<LoadErrorEventArgs> LoadError;
		void LoadUrl(string url);
	}

	public interface IWebBrowser : IChromiumWebBrowserBase
	{
		string Address { get; }
		IDialogHandler DialogHandler { get; set; }
		IDisplayHandler DisplayHandler { get; set; }
		IDownloadHandler DownloadHandler { get; set; }
		IDragHandler DragHandler { get; set; }
		IFocusHandler FocusHandler { get; set; }
		IJsDialogHandler JsDialogHandler { get; set; }
		IKeyboardHandler KeyboardHandler { get; set; }
		ILifeSpanHandler LifeSpanHandler { get; set; }
		IContextMenuHandler MenuHandler { get; set; }
		IRenderProcessMessageHandler RenderProcessMessageHandler { get; set; }
		IRequestHandler RequestHandler { get; set; }

		event EventHandler<JavascriptMessageReceivedEventArgs> JavascriptMessageReceived;
	}

	public static class WebBrowserExtensions
	{
		public static void CloseDevTools(this IChromiumWebBrowserBase browser)
		{
			browser?.BrowserCore?.GetHost()?.CloseDevTools();
		}

		public static void Find(this IChromiumWebBrowserBase browser, string searchText, bool forward, bool matchCase, bool findNext)
		{
			browser?.BrowserCore?.GetHost()?.Find(searchText, forward, matchCase, findNext);
		}

		public static List<IFrame> GetAllFrames(this IBrowser browser)
		{
			var frames = new List<IFrame>();
			if (browser != null)
			{
				var ids = browser.GetFrameIdentifiers();
				if (ids != null)
				{
					foreach (var id in ids)
					{
						var frame = browser.GetFrame(id);
						if (frame != null)
						{
							frames.Add(frame);
						}
					}
				}
				else if (browser.MainFrame != null)
				{
					frames.Add(browser.MainFrame);
				}
			}
			return frames;
		}
	}
}

namespace CefSharp.Handler
{
	public class ResourceRequestHandler : IResourceRequestHandler
	{
		IResourceHandler IResourceRequestHandler.GetResourceHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request)
			=> GetResourceHandler(chromiumWebBrowser, browser, frame, request);

		CefReturnValue IResourceRequestHandler.OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
			=> OnBeforeResourceLoad(chromiumWebBrowser, browser, frame, request, callback);

		bool IResourceRequestHandler.OnProtocolExecution(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request)
			=> OnProtocolExecution(chromiumWebBrowser, browser, frame, request);

		void IResourceRequestHandler.OnResourceRedirect(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl)
			=> OnResourceRedirect(chromiumWebBrowser, browser, frame, request, response, ref newUrl);

		bool IResourceRequestHandler.OnResourceResponse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
			=> OnResourceResponse(chromiumWebBrowser, browser, frame, request, response);

		protected virtual IResourceHandler GetResourceHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request)
		{
			return null;
		}

		protected virtual CefReturnValue OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
		{
			return CefReturnValue.Continue;
		}

		protected virtual bool OnProtocolExecution(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request)
		{
			return false;
		}

		protected virtual void OnResourceRedirect(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl)
		{
		}

		protected virtual bool OnResourceResponse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
		{
			return false;
		}

		public virtual void Dispose()
		{
		}
	}

	public class RequestHandler : IRequestHandler
	{
		bool IRequestHandler.GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
			=> GetAuthCredentials(chromiumWebBrowser, browser, originUrl, isProxy, host, port, realm, scheme, callback);

		IResourceRequestHandler IRequestHandler.GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
			=> GetResourceRequestHandler(chromiumWebBrowser, browser, frame, request, isNavigation, isDownload, requestInitiator, ref disableDefaultHandling);

		bool IRequestHandler.OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
			=> OnBeforeBrowse(chromiumWebBrowser, browser, frame, request, userGesture, isRedirect);

		bool IRequestHandler.OnOpenUrlFromTab(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
			=> OnOpenUrlFromTab(chromiumWebBrowser, browser, frame, targetUrl, targetDisposition, userGesture);

		protected virtual bool GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
		{
			return false;
		}

		protected virtual IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
		{
			return null;
		}

		protected virtual bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
		{
			return false;
		}

		protected virtual bool OnOpenUrlFromTab(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
		{
			return false;
		}
	}
}

namespace CefSharp.WinForms.Host
{
	public class ChromiumHostControl : Component, IChromiumWebBrowserBase, System.Windows.Forms.IWin32Window, ISynchronizeInvoke
	{
		public IBrowser BrowserCore { get; protected set; }
		public bool IsBrowserInitialized { get; protected set; }
		public bool IsDisposed { get; private set; }
		public bool IsHandleCreated => true;
		public IntPtr Handle => IntPtr.Zero;
		public string Address { get; protected set; }
		public bool InvokeRequired => false;

		public event EventHandler<AddressChangedEventArgs> AddressChanged;
		public event EventHandler<ConsoleMessageEventArgs> ConsoleMessage;
		public event EventHandler IsBrowserInitializedChanged;
		public event EventHandler<LoadingStateChangedEventArgs> LoadingStateChanged;
		public event EventHandler<LoadErrorEventArgs> LoadError;
		public event EventHandler<TitleChangedEventArgs> TitleChanged;

		public static ChromiumHostControl FromBrowser(IBrowser browser)
		{
			return null;
		}

		public virtual void LoadUrl(string url)
		{
			Address = url;
			AddressChanged?.Invoke(this, new AddressChangedEventArgs(BrowserCore, url));
		}

		protected override void Dispose(bool disposing)
		{
			IsDisposed = true;
			base.Dispose(disposing);
		}

		public IAsyncResult BeginInvoke(Delegate method, object[] args)
		{
			var task = Task.Run(() => method?.DynamicInvoke(args));
			return task;
		}

		public object EndInvoke(IAsyncResult result)
		{
			if (result is Task<object> task)
			{
				return task.GetAwaiter().GetResult();
			}
			return null;
		}

		public object Invoke(Delegate method, object[] args)
		{
			return method?.DynamicInvoke(args);
		}
	}
}

namespace CefSharp.WinForms.Handler
{
	using CefSharp.Enums;
	using CefSharp.Structs;
	using CefSharp.WinForms.Host;

	public enum PopupCreation
	{
		Cancel = 0,
		Continue = 1,
		ContinueWithJavascriptDisabled = 2
	}

	public class LifeSpanHandlerBuilder
	{
		public LifeSpanHandlerBuilder OnBeforePopupCreated(Func<IWebBrowser, IBrowser, IFrame, string, string, WindowOpenDisposition, bool, IBrowserSettings, PopupCreation> handler)
		{
			return this;
		}

		public LifeSpanHandlerBuilder OnPopupCreated(Action<ChromiumHostControl, string> handler)
		{
			return this;
		}

		public LifeSpanHandlerBuilder OnPopupDestroyed(Action<ChromiumHostControl, IBrowser> handler)
		{
			return this;
		}

		public ILifeSpanHandler Build()
		{
			return new LifeSpanHandler();
		}
	}

	public interface IBrowserSettings
	{
	}

	public class LifeSpanHandler : ILifeSpanHandler
	{
		public static LifeSpanHandlerBuilder Create(Func<ChromiumHostControl> createPopupHost)
		{
			return new LifeSpanHandlerBuilder();
		}
	}

	public class DisplayHandler : IDisplayHandler
	{
		void IDisplayHandler.OnAddressChanged(IWebBrowser chromiumWebBrowser, AddressChangedEventArgs addressChangedArgs) => OnAddressChanged(chromiumWebBrowser, addressChangedArgs);
		bool IDisplayHandler.OnAutoResize(IWebBrowser chromiumWebBrowser, IBrowser browser, Size newSize) => OnAutoResize(chromiumWebBrowser, browser, newSize);
		bool IDisplayHandler.OnConsoleMessage(IWebBrowser chromiumWebBrowser, ConsoleMessageEventArgs consoleMessageArgs) => OnConsoleMessage(chromiumWebBrowser, consoleMessageArgs);
		bool IDisplayHandler.OnCursorChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr cursor, CursorType type, CursorInfo customCursorInfo) => OnCursorChange(chromiumWebBrowser, browser, cursor, type, customCursorInfo);
		void IDisplayHandler.OnFaviconUrlChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IList<string> urls) => OnFaviconUrlChange(chromiumWebBrowser, browser, urls);
		void IDisplayHandler.OnFullscreenModeChange(IWebBrowser chromiumWebBrowser, IBrowser browser, bool fullscreen) => OnFullscreenModeChange(chromiumWebBrowser, browser, fullscreen);
		void IDisplayHandler.OnLoadingProgressChange(IWebBrowser chromiumWebBrowser, IBrowser browser, double progress) => OnLoadingProgressChange(chromiumWebBrowser, browser, progress);
		void IDisplayHandler.OnStatusMessage(IWebBrowser chromiumWebBrowser, StatusMessageEventArgs statusMessageArgs) => OnStatusMessage(chromiumWebBrowser, statusMessageArgs);
		void IDisplayHandler.OnTitleChanged(IWebBrowser chromiumWebBrowser, TitleChangedEventArgs titleChangedArgs) => OnTitleChanged(chromiumWebBrowser, titleChangedArgs);
		bool IDisplayHandler.OnTooltipChanged(IWebBrowser chromiumWebBrowser, ref string text) => OnTooltipChanged(chromiumWebBrowser, ref text);

		protected virtual void OnAddressChanged(IWebBrowser chromiumWebBrowser, AddressChangedEventArgs addressChangedArgs) { }
		protected virtual bool OnAutoResize(IWebBrowser chromiumWebBrowser, IBrowser browser, Size newSize) => false;
		protected virtual bool OnConsoleMessage(IWebBrowser chromiumWebBrowser, ConsoleMessageEventArgs consoleMessageArgs) => false;
		protected virtual bool OnCursorChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr cursor, CursorType type, CursorInfo customCursorInfo) => false;
		protected virtual void OnFaviconUrlChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IList<string> urls) { }
		protected virtual void OnFullscreenModeChange(IWebBrowser chromiumWebBrowser, IBrowser browser, bool fullscreen) { }
		protected virtual void OnLoadingProgressChange(IWebBrowser chromiumWebBrowser, IBrowser browser, double progress) { }
		protected virtual void OnStatusMessage(IWebBrowser chromiumWebBrowser, StatusMessageEventArgs statusMessageArgs) { }
		protected virtual void OnTitleChanged(IWebBrowser chromiumWebBrowser, TitleChangedEventArgs titleChangedArgs) { }
		protected virtual bool OnTooltipChanged(IWebBrowser chromiumWebBrowser, ref string text) => false;
	}
}

namespace CefSharp.WinForms
{
	using CefSharp.WinForms.Host;

	public class CefSettings : CefSettingsBase
	{
	}

	public interface IWinFormsChromiumWebBrowser : IChromiumWebBrowserBase
	{
		string Address { get; }
		event EventHandler<AddressChangedEventArgs> AddressChanged;
		event EventHandler IsBrowserInitializedChanged;
		event EventHandler<TitleChangedEventArgs> TitleChanged;
	}

	public class ChromiumWebBrowser : ChromiumHostControl, IWinFormsChromiumWebBrowser, IWebBrowser
	{
		public IDialogHandler DialogHandler { get; set; }
		public IDisplayHandler DisplayHandler { get; set; }
		public IDownloadHandler DownloadHandler { get; set; }
		public IDragHandler DragHandler { get; set; }
		public IFocusHandler FocusHandler { get; set; }
		public IJsDialogHandler JsDialogHandler { get; set; }
		public IKeyboardHandler KeyboardHandler { get; set; }
		public ILifeSpanHandler LifeSpanHandler { get; set; }
		public IContextMenuHandler MenuHandler { get; set; }
		public IRenderProcessMessageHandler RenderProcessMessageHandler { get; set; }
		public IRequestHandler RequestHandler { get; set; }

		public event EventHandler<JavascriptMessageReceivedEventArgs> JavascriptMessageReceived;

		public ChromiumWebBrowser(string address)
		{
			Address = address;
		}

		public new static ChromiumWebBrowser FromBrowser(IBrowser browser)
		{
			return null;
		}

		public void Load(string url)
		{
			LoadUrl(url);
		}
	}
}
