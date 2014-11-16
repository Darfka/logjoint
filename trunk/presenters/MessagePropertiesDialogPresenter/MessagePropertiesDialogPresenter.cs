using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LogJoint.UI.Presenters.MessagePropertiesDialog
{
	public class Presenter : IPresenter, IPresenterEvents, IMessagePropertiesFormHost
	{
		public Presenter(
			IModel model,
			IView view,
			LogViewer.Presenter viewerPresenter,
			IPresentersFacade navHandler)
		{
			this.model = model;
			this.view = view;
			this.viewerPresenter = viewerPresenter;
			this.navHandler = navHandler;

			viewerPresenter.FocusedMessageChanged += delegate(object sender, EventArgs args)
			{
				if (GetPropertiesForm() != null)
					GetPropertiesForm().UpdateView(viewerPresenter.FocusedMessage);
			};
		}

		void IPresenter.ShowDialog()
		{
			if (GetPropertiesForm() == null)
			{
				propertiesForm = view.CreateDialog(this);
			}
			propertiesForm.UpdateView(viewerPresenter.FocusedMessage);
			propertiesForm.Show();
		}

		IPresentersFacade IMessagePropertiesFormHost.UINavigationHandler
		{
			get { return navHandler; }
		}

		bool IMessagePropertiesFormHost.BookmarksSupported
		{
			get { return model.Bookmarks != null; }
		}

		bool IMessagePropertiesFormHost.NavigationOverHighlightedIsEnabled
		{
			get
			{
				return model.HighlightFilters.FilteringEnabled && model.HighlightFilters.Count > 0;
			}
		}

		void IMessagePropertiesFormHost.ToggleBookmark(IMessage line)
		{
			viewerPresenter.ToggleBookmark(line);
		}

		void IMessagePropertiesFormHost.FindBegin(IFrameEnd end)
		{
			viewerPresenter.GoToParentFrame();
		}

		void IMessagePropertiesFormHost.FindEnd(IFrameBegin begin)
		{
			viewerPresenter.GoToEndOfFrame();
		}

		void IMessagePropertiesFormHost.ShowLine(IBookmark msg, BookmarkNavigationOptions options)
		{
			navHandler.ShowLine(msg, options);
		}

		void IMessagePropertiesFormHost.Next()
		{
			viewerPresenter.Next();
		}

		void IMessagePropertiesFormHost.Prev()
		{
			viewerPresenter.Prev();
		}

		void IMessagePropertiesFormHost.NextHighlighted()
		{
			viewerPresenter.GoToNextHighlightedMessage();
		}

		void IMessagePropertiesFormHost.PrevHighlighted()
		{
			viewerPresenter.GoToPrevHighlightedMessage();
		}

		#region Implementation

		IDialog GetPropertiesForm()
		{
			if (propertiesForm != null)
				if (propertiesForm.IsDisposed)
					propertiesForm = null;
			return propertiesForm;
		}


		readonly IModel model;
		readonly IView view;
		readonly LogViewer.Presenter viewerPresenter;
		readonly IPresentersFacade navHandler;
		IDialog propertiesForm;

		#endregion
	};
};