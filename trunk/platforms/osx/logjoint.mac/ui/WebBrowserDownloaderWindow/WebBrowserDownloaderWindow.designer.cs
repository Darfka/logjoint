// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using MonoMac.Foundation;
using System.CodeDom.Compiler;

namespace LogJoint.UI
{
	[Register ("WebBrowserDownloaderWindowController")]
	partial class WebBrowserDownloaderWindowController
	{
		[Outlet]
		MonoMac.WebKit.WebView webView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (webView != null) {
				webView.Dispose ();
				webView = null;
			}
		}
	}

	[Register ("WebBrowserDownloaderWindow")]
	partial class WebBrowserDownloaderWindow
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
