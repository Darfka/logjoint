using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;

namespace LogJoint.UI.Presenters.MessagePropertiesDialog
{
	public interface IPresenter
	{
		void ShowDialog();
	};


	public interface IView // todo: bad view interface. move presentation logic to presenter. get rid of IMessagePropertiesFormHost.
	{
		IDialog CreateDialog(IMessagePropertiesFormHost host);
	};

	public interface IDialog
	{
		void UpdateView(MessageBase msg);
		void Show();
		bool IsDisposed { get; }
	};

	public interface IPresenterEvents
	{
	};

	public interface IMessagePropertiesFormHost
	{
		IUINavigationHandler UINavigationHandler { get; }
		bool BookmarksSupported { get; }
		bool NavigationOverHighlightedIsEnabled { get; }
		void ToggleBookmark(MessageBase line);
		void FindBegin(FrameEnd end);
		void FindEnd(FrameBegin begin);
		void ShowLine(IBookmark msg, BookmarkNavigationOptions options = BookmarkNavigationOptions.Default);
		void Next();
		void Prev();
		void NextHighlighted();
		void PrevHighlighted();
	};
};