using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;

namespace LogJoint.UI.Presenters.Timeline
{
	public class Presenter : IPresenter, IPresenterEvents
	{
		public Presenter(
			IModel model,
			IView view,
			LJTraceSource tracer,
			Presenters.LogViewer.Presenter viewerPresenter,
			IStatusReportFactory statusReportFactory,
			ITabUsageTracker tabUsageTracker,
			IHeartBeatTimer heartbeat)
		{
			this.model = model;
			this.view = view;
			this.tracer = tracer;
			this.viewerPresenter = viewerPresenter;
			this.statusReportFactory = statusReportFactory;
			this.tabUsageTracker = tabUsageTracker;
			this.heartbeat = heartbeat;

			viewerPresenter.FocusedMessageChanged += delegate(object sender, EventArgs args)
			{
				view.Invalidate();
			};
			model.SourcesManager.OnLogSourceVisiblityChanged += (sender, args) =>
			{
				gapsUpdateFlag.Invalidate();
			};
			model.SourcesManager.OnLogSourceRemoved += (sender, args) =>
			{
				gapsUpdateFlag.Invalidate();
			};
			model.SourcesManager.OnLogSourceAdded += (sender, args) =>
			{
				gapsUpdateFlag.Invalidate();
			};
			heartbeat.OnTimer += (sender, args) =>
			{
				if (args.IsNormalUpdate && gapsUpdateFlag.Validate())
					UpdateTimeGaps();
			};


			view.SetPresenter(this);
		}

		public event EventHandler<EventArgs> RangeChanged;

		void IPresenter.UpdateView()
		{
			view.Update();
		}

		void IPresenter.Zoom(int delta)
		{
			view.Zoom(delta);
		}

		void IPresenter.Scroll(int delta)
		{
			view.Scroll(delta);
		}

		void IPresenter.ZoomToViewAll()
		{
			view.ZoomToViewAll();
		}

		void IPresenter.TrySwitchOnViewTailMode()
		{
			view.TrySwitchOnViewTailMode();
		}

		void IPresenter.TrySwitchOffViewTailMode()
		{
			view.TrySwitchOffViewTailMode();
		}

		bool IPresenter.AreMillisecondsVisible { get { return view.AreMillisecondsVisible; } }


		void IPresenterEvents.OnNavigate(TimeNavigateEventArgs args)
		{
			using (tracer.NewFrame)
			{
				string preferredSourceId = args.Source != null ? args.Source.ConnectionId : null;
				ILogSource preferredSource = model.SourcesManager.Items.FirstOrDefault(
						c => c.ConnectionId != null && c.ConnectionId == preferredSourceId);
				tracer.Info("----> User Command: Navigate from timeline. Date='{0}', Flags={1}, Source={2}", args.Date, args.Flags, preferredSourceId);
				model.SourcesManager.NavigateTo(args.Date, args.Flags, preferredSource);
			}
		}

		void IPresenterEvents.OnRangeChanged()
		{
			gapsUpdateFlag.Invalidate();
			if (RangeChanged != null)
				RangeChanged(this, EventArgs.Empty);
		}

		void IPresenterEvents.OnBeginTimeRangeDrag()
		{
			heartbeat.Suspend();
		}

		void IPresenterEvents.OnEndTimeRangeDrag()
		{
			heartbeat.Resume();
		}


		IEnumerable<ILogSource> IPresenterEvents.Sources
		{
			get
			{
				foreach (ILogSource s in model.SourcesManager.Items)
					if (s.Visible)
						yield return s;
			}
		}

		int IPresenterEvents.SourcesCount
		{
			get
			{
				int ret = 0;
				foreach (ILogSource ls in model.SourcesManager.Items)
					if (ls.Visible)
						++ret;
				return ret;
			}
		}

		DateTime? IPresenterEvents.CurrentViewTime { get { return viewerPresenter.FocusedMessageTime; } }

		ILogSource IPresenterEvents.CurrentSource
		{
			get
			{
				var focusedMsg = viewerPresenter.FocusedMessage;
				if (focusedMsg == null)
					return null;
				return focusedMsg.LogSource;
			}
		}

		IStatusReport IPresenterEvents.CreateNewStatusReport() { return statusReportFactory.CreateNewStatusReport(); }

		IEnumerable<IBookmark> IPresenterEvents.Bookmarks { get { return model.Bookmarks.Items; } }

		bool IPresenterEvents.FocusRectIsRequired { get { return tabUsageTracker.FocusRectIsRequired; } }

		bool IPresenterEvents.IsInViewTailMode { get { return model.SourcesManager.IsInViewTailMode; } }

		bool IPresenterEvents.IsBusy { get { return model.SourcesManager.AtLeastOneSourceIsBeingLoaded(); } }


		#region Implementation

		void UpdateTimeGaps()
		{
			foreach (var source in model.SourcesManager.Items)
				source.TimeGaps.Update(view.TimeRange);
		}

		readonly IModel model;
		readonly IView view;
		readonly LJTraceSource tracer;
		readonly Presenters.LogViewer.Presenter viewerPresenter;
		readonly IStatusReportFactory statusReportFactory;
		readonly ITabUsageTracker tabUsageTracker;
		readonly IHeartBeatTimer heartbeat;
		readonly LazyUpdateFlag gapsUpdateFlag = new LazyUpdateFlag();

		#endregion
	};
};