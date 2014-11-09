using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;
using LogJoint;

namespace LogJoint.UI.Presenters.FiltersManager
{
	public class Presenter : IPresenter, IPresenterEvents
	{
		public Presenter(
			IModel model,
			IFiltersList filtersList,
			IView view,
			FiltersListBox.IPresenter filtersListPresenter,
			FilterDialog.IPresenter filtersDialogPresenter,
			LogViewer.Presenter logViewerPresenter,
			IViewUpdates viewUpdates,
			IHeartBeatTimer heartbeat)
		{
			this.model = model;
			this.filtersList = filtersList;
			this.view = view;
			this.filtersListPresenter = filtersListPresenter;
			this.filtersDialogPresenter = filtersDialogPresenter;
			this.isHighlightFilter = filtersList == model.HighlightFilters;
			this.logViewerPresenter = logViewerPresenter;
			this.viewUpdates = viewUpdates;

			view.SetFiltertingEnabledCheckBoxLabel(isHighlightFilter ? "Enabled highlighting" : "Enable filtering");

			UpdateControls();

			filtersListPresenter.SelectionChanged += delegate(object sender, EventArgs args)
			{
				UpdateControls();
			};
			filtersListPresenter.FilterChecked += delegate(object sender, EventArgs args)
			{
				NotifyAboutFilteringResultChange();
			};
			filtersList.OnPropertiesChanged += (sender, args) =>
			{
				updateTracker.Invalidate();
			};
			filtersList.OnCountersChanged += (sender, args) =>
			{
				updateTracker.Invalidate();
			};
			filtersList.OnFilteringEnabledChanged += (sender, args) =>
			{
				updateTracker.Invalidate();
			};
			filtersList.OnFiltersListChanged += (sender, args) =>
			{
				updateTracker.Invalidate();
			};
			heartbeat.OnTimer += (sender, args) =>
			{
				if (args.IsNormalUpdate && updateTracker.Validate())
					UpdateView();
			};

			view.SetPresenter(this);
		}

		FiltersListBox.IPresenter IPresenter.FiltersListPresenter { get { return filtersListPresenter; } }

		void IPresenterEvents.OnEnableFilteringChecked(bool value)
		{
			filtersList.FilteringEnabled = value;
			NotifyAboutFilteringResultChange();
		}

		void IPresenterEvents.OnAddFilterClicked()
		{
			string defaultTemplate = "";
			if (!logViewerPresenter.Selection.IsEmpty && logViewerPresenter.Selection.IsSingleLine)
				defaultTemplate = logViewerPresenter.GetSelectedText();
			Filter f = new Filter(
				FilterAction.Include,
				string.Format("New filter {0}", ++lastFilterIndex),
				true, defaultTemplate, false, false, false);
			try
			{
				if (!filtersDialogPresenter.ShowTheDialog(f))
				{
					return;
				}
				try
				{
					filtersList.Insert(0, f);
				}
				catch (TooManyFiltersException)
				{
					view.ShowTooManyFiltersAlert(isHighlightFilter ? "Too many highlighting rules" : "Too many filters");
					return;
				}
				f = null;
				NotifyAboutFilteringResultChange();
			}
			finally
			{
				if (f != null)
				{
					f.Dispose();
				}
			}
		}

		void IPresenterEvents.OnRemoveFilterClicked()
		{
			List<Filter> toDelete = new List<Filter>();
			foreach (Filter f in filtersListPresenter.SelectedFilters)
			{
				toDelete.Add(f);
			}

			if (toDelete.Count == 0)
			{
				return;
			}

			if (!view.AskUserConfirmationToDeleteFilters(toDelete.Count))
			{
				return;
			}

			filtersList.Delete(toDelete);
		}

		void IPresenterEvents.OnMoveFilterUpClicked()
		{
			MoveFilterInternal(true);
		}

		void IPresenterEvents.OnMoveFilterDownClicked()
		{
			MoveFilterInternal(false);
		}

		void IPresenterEvents.OnPrevClicked()
		{
			logViewerPresenter.GoToPrevHighlightedMessage();
		}

		void IPresenterEvents.OnNextClicked()
		{
			logViewerPresenter.GoToNextHighlightedMessage();
		}

		#region Implementation

		void MoveFilterInternal(bool up)
		{
			foreach (Filter f in filtersListPresenter.SelectedFilters)
			{
				if (filtersList.Move(f, up))
				{
					NotifyAboutFilteringResultChange();
				}
				break;
			}
		}

		void NotifyAboutFilteringResultChange()
		{
			viewUpdates.RequestUpdate();
		}

		void UpdateView()
		{
			filtersListPresenter.UpdateView();
			UpdateControls();
		}

		void UpdateControls()
		{
			ViewControl visibleCtrls = 
				ViewControl.FilteringEnabledCheckbox | 
				ViewControl.AddFilterButton | ViewControl.RemoveFilterButton | 
				ViewControl.MoveUpButton | ViewControl.MoveDownButton;
			if (isHighlightFilter)
				visibleCtrls |= (ViewControl.PrevButton | ViewControl.NextButton);
			view.SetControlsVisibility(visibleCtrls);

			int count = filtersListPresenter.SelectedFilters.Count();
			ViewControl enabledCtrls = 
				ViewControl.FilteringEnabledCheckbox | ViewControl.AddFilterButton;
			if (count > 0)
				enabledCtrls |= ViewControl.RemoveFilterButton;
			if (count == 1)
				enabledCtrls |= (ViewControl.MoveDownButton | ViewControl.MoveUpButton);
			if (isHighlightFilter && IsNavigationOverHighlightedMessagesEnabled())
				enabledCtrls |= (ViewControl.PrevButton | ViewControl.NextButton);
			view.EnableControls(enabledCtrls);

			view.SetFiltertingEnabledCheckBoxValue(filtersList.FilteringEnabled);
		}

		bool IsNavigationOverHighlightedMessagesEnabled()
		{
			return model.HighlightFilters.FilteringEnabled &&
				model.HighlightFilters.Count > 0;
		}

		readonly IModel model;
		readonly IFiltersList filtersList;
		readonly bool isHighlightFilter;
		readonly IView view;
		readonly FilterDialog.IPresenter filtersDialogPresenter;
		readonly FiltersListBox.IPresenter filtersListPresenter;
		readonly LogViewer.Presenter logViewerPresenter;
		readonly IViewUpdates viewUpdates;
		readonly LazyUpdateFlag updateTracker = new LazyUpdateFlag();
		int lastFilterIndex;

		#endregion
	};
};