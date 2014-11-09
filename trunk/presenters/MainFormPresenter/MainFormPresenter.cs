using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using LogJoint.Preprocessing;
using System.Diagnostics;
using System.IO;

namespace LogJoint.UI.Presenters.MainForm
{
	public class Presenter: IPresenter, IPresenterEvents
	{
		public Presenter( // todo: refactor to reduce the nr of dependencies
			IModel model,
			IView view,
			LJTraceSource tracer,
			UI.Presenters.LogViewer.Presenter viewerPresenter,
			UI.Presenters.SearchResult.Presenter searchResultPresenter,
			UI.Presenters.SearchPanel.IPresenter searchPanelPresenter,
			UI.Presenters.SourcesList.IPresenter sourcesListPresenter,
			UI.Presenters.SourcesManager.IPresenter sourcesManagerPresenter,
			UI.Presenters.Timeline.IPresenter timelinePresenter,
			MessagePropertiesDialog.IPresenter messagePropertiesDialogPresenter,
			UI.Presenters.LoadedMessages.IPresenter loadedMessagesPresenter,
			Preprocessing.IPreprocessingUserRequests preprocessingUserRequests,
			UI.Presenters.BookmarksManager.IPresenter bookmarksManagerPresenter,
			IHeartBeatTimer heartBeatTimer,
			ITabUsageTracker tabUsageTracker,
			IStatusReportFactory statusReportFactory,
			IDragDropHandler dragDropHandler,
			IDisposable pluginsManager,
			IUINavigationHandler navHandler, // todo: remove this dependency
			Options.Dialog.IPresenter optionsDialogPresenter
		)
		{
			this.model = model;
			this.view = view;
			this.tracer = tracer;
			this.tabUsageTracker = tabUsageTracker;
			this.statusReportFactory = statusReportFactory;
			this.preprocessingUserRequests = preprocessingUserRequests;
			this.searchPanelPresenter = searchPanelPresenter;
			this.bookmarksManagerPresenter = bookmarksManagerPresenter;
			this.viewerPresenter = viewerPresenter;
			this.loadedMessagesPresenter = loadedMessagesPresenter;
			this.searchResultPresenter = searchResultPresenter;
			this.timelinePresenter = timelinePresenter;
			this.pluginsManager = pluginsManager;
			this.navHandler = navHandler;
			this.dragDropHandler = dragDropHandler;
			this.optionsDialogPresenter = optionsDialogPresenter;

			view.SetPresenter(this);

			viewerPresenter.ManualRefresh += delegate(object sender, EventArgs args)
			{
				using (tracer.NewFrame)
				{
					tracer.Info("----> User Command: Refresh");
					model.SourcesManager.Refresh();
				}
			};
			viewerPresenter.BeginShifting += delegate(object sender, EventArgs args)
			{
				SetWaitState(true);
				statusReportFactory.CreateNewStatusReport().ShowStatusText("Moving in-memory window...", false);
				view.SetCancelLongRunningControlsVisibility(true);
				longRunningProcessCancellationRoutine = model.SourcesManager.CancelShifting;
			};
			viewerPresenter.EndShifting += delegate(object sender, EventArgs args)
			{
				longRunningProcessCancellationRoutine = null;
				view.SetCancelLongRunningControlsVisibility(false);
				statusReportFactory.CreateNewStatusReport().Dispose();
				SetWaitState(false);
			};
			viewerPresenter.FocusedMessageChanged += delegate(object sender, EventArgs args)
			{
				model.SourcesManager.OnCurrentViewPositionChanged(viewerPresenter.FocusedMessageTime);
				searchResultPresenter.MasterFocusedMessage = viewerPresenter.FocusedMessage;
			};
			viewerPresenter.DefaultFocusedMessageActionCaption = "Show properties...";
			viewerPresenter.DefaultFocusedMessageAction += (s, e) =>
			{
				messagePropertiesDialogPresenter.ShowDialog();
			};

			searchResultPresenter.OnClose += (sender, args) => searchPanelPresenter.CollapseSearchResultPanel();
			searchResultPresenter.OnResizingStarted += (sender, args) => view.BeginSplittingSearchResults();

			sourcesListPresenter.OnBusyState += (_, evt) => SetWaitState(evt.BusyStateRequired);

			sourcesManagerPresenter.OnBusyState += (_, evt) => SetWaitState(evt.BusyStateRequired);

			timelinePresenter.RangeChanged += delegate(object sender, EventArgs args)
			{
				UpdateMillisecondsAvailability();
			};

			searchPanelPresenter.InputFocusAbandoned += delegate(object sender, EventArgs args)
			{
				loadedMessagesPresenter.Focus();
			};

			model.SourcesManager.OnSearchStarted += (sender, args) =>
			{
				SetWaitState(true);
				statusReportFactory.CreateNewStatusReport().ShowStatusText("Searching...", false);
				view.SetCancelLongRunningControlsVisibility(true);
				longRunningProcessCancellationRoutine = model.SourcesManager.CancelSearch;
			};
			model.SourcesManager.OnSearchCompleted += (sender, args) =>
			{
				longRunningProcessCancellationRoutine = null;
				view.SetCancelLongRunningControlsVisibility(false);
				statusReportFactory.CreateNewStatusReport().Dispose();
				SetWaitState(false);
			};

			heartBeatTimer.OnTimer += (sender, e) =>
			{
				if (e.IsRareUpdate)
					SetAnalizingIndication(model.SourcesManager.Items.Any(s => s.TimeGaps.IsWorking));
			};
			sourcesManagerPresenter.OnViewUpdated += (sender, evt) =>
			{
				UpdateRawViewAvailability();
				UpdateMillisecondsAvailability();
			};
		}

		void IPresenter.ExecuteThreadPropertiesDialog(IThread thread)
		{
			view.ExecuteThreadPropertiesDialog(thread, navHandler);
		}

		void IPresenter.ActivateTab(string tabId)
		{
			view.ActivateTab(tabId);
		}

		void IPresenterEvents.OnClosing()
		{
			using (tracer.NewFrame)
			{
				SetWaitState(true);
				try
				{
					model.Dispose();
					pluginsManager.Dispose();
				}
				finally
				{
					SetWaitState(false);
				}
			}
		}

		void IPresenterEvents.OnLoad()
		{
			string[] args = Environment.GetCommandLineArgs();

			if (args.Length > 1)
			{
				model.LogSourcesPreprocessings.Preprocess(
					args.Skip(1).Select(f => new Preprocessing.FormatDetectionStep(f)),
					preprocessingUserRequests
				);
			}
		}

		void IPresenterEvents.OnTabPressed()
		{
			tabUsageTracker.OnTabPressed();
		}

		void IPresenterEvents.OnCancelLongRunningProcessButtonClicked()
		{
			CancelLongRunningProcess();
		}

		void CancelLongRunningProcess()
		{
			tracer.Info("----> User Command: Cancel long running process");
			if (longRunningProcessCancellationRoutine != null)
				longRunningProcessCancellationRoutine();
		}

		void IPresenterEvents.OnKeyPressed(KeyCode key, bool shift, bool control)
		{
			if (longRunningProcessCancellationRoutine != null && key == KeyCode.Escape)
				CancelLongRunningProcess();
			if ((key == KeyCode.F) && control)
			{
				view.ActivateTab(TabIDs.Search);
				searchPanelPresenter.ReceiveInputFocus(forceSearchAllOccurencesMode: shift);
			}
			else if (key == KeyCode.F3 && !shift)
			{
				searchPanelPresenter.PerformSearch();
			}
			else if (key == KeyCode.F3 && shift)
			{
				searchPanelPresenter.PerformReversedSearch();
			}
			else if (key == KeyCode.K && control)
			{
				bookmarksManagerPresenter.ToggleBookmark();
			}
			else if (key == KeyCode.F2 && !shift)
			{
				bookmarksManagerPresenter.ShowNextBookmark();
			}
			else if (key == KeyCode.F2 && shift)
			{
				bookmarksManagerPresenter.ShowPrevBookmark();
			}
		}

		void IPresenterEvents.OnOptionsLinkClicked()
		{
			view.ShowOptionsMenu();
		}

		void IPresenterEvents.OnAboutMenuClicked()
		{
			view.ShowAboutBox();
		}

		void IPresenterEvents.OnConfigurationMenuClicked()
		{
			optionsDialogPresenter.ShowDialog();
		}

		bool IPresenterEvents.OnDragOver(object data)
		{
			return dragDropHandler.ShouldAcceptDragDrop(data);
		}

		void IPresenterEvents.OnDragDrop(object data)
		{
			dragDropHandler.AcceptDragDrop(data);
		}

		void IPresenterEvents.OnRawViewButtonClicked()
		{
			viewerPresenter.ShowRawMessages = !viewerPresenter.ShowRawMessages;
		}

		#region Implementation

		void UpdateRawViewAvailability()
		{
			bool rawViewAllowed = model.SourcesManager.Items.Any(s => !s.IsDisposed && s.Visible && s.Provider.Factory.ViewOptions.RawViewAllowed);
			loadedMessagesPresenter.RawViewAllowed = rawViewAllowed;
			searchResultPresenter.RawViewAllowed = rawViewAllowed;
		}

		void UpdateMillisecondsAvailability()
		{
			bool timeLineWantsMilliseconds = timelinePresenter.AreMillisecondsVisible;
			bool atLeastOneSourceWantMillisecondsAlways = model.SourcesManager.Items.Any(s => !s.IsDisposed && s.Visible && s.Provider.Factory.ViewOptions.AlwaysShowMilliseconds);
			viewerPresenter.ShowMilliseconds = timeLineWantsMilliseconds || atLeastOneSourceWantMillisecondsAlways;
		}

		void SetWaitState(bool wait)
		{
			using (tracer.NewFrame)
			{
				if (wait)
				{
					tracer.Info("Setting wait state");
					inputFocusBeforeWaitState = view.CaptureInputFocusState();
				}
				else
				{
					tracer.Info("Exiting from wait state");
				}
				view.EnableFormControls(!wait);
				if (!wait)
				{
					inputFocusBeforeWaitState.Restore();
				}
			}
		}

		void SetAnalizingIndication(bool analizing)
		{
			if (isAnalizing == analizing)
				return;
			view.SetAnalizingIndicationVisibility(analizing);
			isAnalizing = analizing;
		}

		readonly IModel model;
		readonly IView view;
		readonly LJTraceSource tracer;
		readonly ITabUsageTracker tabUsageTracker;
		readonly IStatusReportFactory statusReportFactory;
		readonly UI.Presenters.LogViewer.Presenter viewerPresenter;
		readonly Preprocessing.IPreprocessingUserRequests preprocessingUserRequests;
		readonly UI.Presenters.SearchPanel.IPresenter searchPanelPresenter;
		readonly UI.Presenters.BookmarksManager.IPresenter bookmarksManagerPresenter;
		readonly UI.Presenters.LoadedMessages.IPresenter loadedMessagesPresenter;
		readonly UI.Presenters.SearchResult.Presenter searchResultPresenter;
		readonly UI.Presenters.Timeline.IPresenter timelinePresenter;
		readonly IDisposable pluginsManager;
		readonly IUINavigationHandler navHandler;
		readonly IDragDropHandler dragDropHandler;
		readonly Options.Dialog.IPresenter optionsDialogPresenter;

		IInputFocusState inputFocusBeforeWaitState;
		bool isAnalizing;
		Action longRunningProcessCancellationRoutine;

		#endregion
	};
};