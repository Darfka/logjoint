﻿using System;

namespace LogJoint.UI
{
	public static class ComponentsInitializer
	{
		public static void WireupDependenciesAndInitMainWindow(MainWindowAdapter mainWindow)
		{
			var tracer = new LJTraceSource("app", "app");
			tracer.Info("starting app");

			using (tracer.NewFrame)
			{
				ILogProviderFactoryRegistry logProviderFactoryRegistry = new LogProviderFactoryRegistry();
				IFormatDefinitionsRepository formatDefinitionsRepository = new DirectoryFormatsRepository(null);
				IUserDefinedFormatsManager userDefinedFormatsManager = new UserDefinedFormatsManager(formatDefinitionsRepository, logProviderFactoryRegistry);
				new AppInitializer(tracer, userDefinedFormatsManager, logProviderFactoryRegistry);
				tracer.Info("app initializer created");

				IInvokeSynchronization invokingSynchronization = new InvokeSynchronization(new NSSynchronizeInvoke());

				TempFilesManager tempFilesManager = LogJoint.TempFilesManager.GetInstance();
				var modelHost = new UI.ModelHost(tracer, mainWindow);

				UI.HeartBeatTimer heartBeatTimer = new UI.HeartBeatTimer();
				UI.Presenters.IViewUpdates viewUpdates = heartBeatTimer;

				IFiltersFactory filtersFactory = new FiltersFactory();
				IBookmarksFactory bookmarksFactory = new BookmarksFactory();
				var bookmarks = bookmarksFactory.CreateBookmarks();
				var persistentUserDataFileSystem = Persistence.Implementation.DesktopFileSystemAccess.CreatePersistentUserDataFileSystem();

				Persistence.Implementation.IStorageManagerImplementation userDataStorage = new Persistence.Implementation.StorageManagerImplementation();
				Persistence.IStorageManager storageManager = new Persistence.PersistentUserDataManager(userDataStorage);
				Settings.IGlobalSettingsAccessor globalSettingsAccessor = new Settings.GlobalSettingsAccessor(storageManager);
				userDataStorage.Init(
					new Persistence.Implementation.RealTimingAndThreading(),
					persistentUserDataFileSystem,
					new Persistence.PersistentUserDataManager.ConfigAccess(globalSettingsAccessor)
				);
				Persistence.IFirstStartDetector firstStartDetector = persistentUserDataFileSystem;
				Persistence.Implementation.IStorageManagerImplementation contentCacheStorage = new Persistence.Implementation.StorageManagerImplementation();
				contentCacheStorage.Init(
					new Persistence.Implementation.RealTimingAndThreading(),
					Persistence.Implementation.DesktopFileSystemAccess.CreateCacheFileSystemAccess(),
					new Persistence.ContentCacheManager.ConfigAccess(globalSettingsAccessor)
				);
				Persistence.IContentCache contentCache = new Persistence.ContentCacheManager(contentCacheStorage);
				Persistence.IWebContentCache webContentCache = new Persistence.WebContentCache(
					contentCache,
					new WebContentCacheConfig()
				);
				IShutdown shutdown = new AppShutdown();
				MultiInstance.IInstancesCounter instancesCounter = new MultiInstance.InstancesCounter(shutdown);
				Progress.IProgressAggregatorFactory progressAggregatorsFactory = new Progress.ProgressAggregator.Factory(heartBeatTimer, invokingSynchronization);
				Progress.IProgressAggregator progressAggregator = progressAggregatorsFactory.CreateProgressAggregator();

				IAdjustingColorsGenerator colorGenerator = new AdjustingColorsGenerator(
					new PastelColorsGenerator(),
					globalSettingsAccessor.Appearance.ColoringBrightness
				);

				IModelThreads modelThreads = new ModelThreads(colorGenerator);

				ILogSourcesManager logSourcesManager = new LogSourcesManager(
					modelHost,
					heartBeatTimer,
					invokingSynchronization,
					modelThreads,
					tempFilesManager,
					storageManager,
					bookmarks,
					globalSettingsAccessor
				);

				Telemetry.ITelemetryCollector telemetryCollector = new Telemetry.TelemetryCollector(
					storageManager,
					new Telemetry.ConfiguredAzureTelemetryUploader(),
					invokingSynchronization,
					instancesCounter,
					shutdown,
					logSourcesManager
				);
				tracer.Info("telemetry created");

				new Telemetry.UnhandledExceptionsReporter(telemetryCollector);

				MRU.IRecentlyUsedEntities recentlyUsedLogs = new MRU.RecentlyUsedEntities(
					storageManager,
					logProviderFactoryRegistry,
					telemetryCollector
				);
				IFormatAutodetect formatAutodetect = new FormatAutodetect(recentlyUsedLogs, logProviderFactoryRegistry);


				Workspaces.IWorkspacesManager workspacesManager = new Workspaces.WorkspacesManager(
					logSourcesManager,
					logProviderFactoryRegistry,
					storageManager,
					new Workspaces.Backend.NoWorkspacesBackend(),
					tempFilesManager,
					recentlyUsedLogs
				);

				AppLaunch.IAppLaunch pluggableProtocolManager = new PluggableProtocolManager();

				Preprocessing.IPreprocessingManagerExtensionsRegistry preprocessingManagerExtensionsRegistry = 
					new Preprocessing.PreprocessingManagerExtentionsRegistry();

				Preprocessing.ICredentialsCache preprocessingCredentialsCache = new PreprocessingCredentialsCache(
					mainWindow.Window,
					storageManager.GlobalSettingsEntry
				);

				Preprocessing.IPreprocessingStepsFactory preprocessingStepsFactory = new Preprocessing.PreprocessingStepsFactory(
					workspacesManager,
					pluggableProtocolManager,
					invokingSynchronization,
					preprocessingManagerExtensionsRegistry,
					progressAggregator,
					webContentCache,
					preprocessingCredentialsCache
				);

				Preprocessing.ILogSourcesPreprocessingManager logSourcesPreprocessings = new Preprocessing.LogSourcesPreprocessingManager(
					invokingSynchronization,
					formatAutodetect,
					preprocessingStepsFactory,
					preprocessingManagerExtensionsRegistry,
					telemetryCollector
				);

				IModel model = new Model(modelHost, invokingSynchronization, tempFilesManager, heartBeatTimer,
					filtersFactory, bookmarks, userDefinedFormatsManager, logProviderFactoryRegistry, storageManager,
					globalSettingsAccessor, recentlyUsedLogs, logSourcesPreprocessings, logSourcesManager, colorGenerator, modelThreads, 
					preprocessingManagerExtensionsRegistry, progressAggregator);
				tracer.Info("model created");

				AutoUpdate.IAutoUpdater autoUpdater = new AutoUpdate.AutoUpdater(
					instancesCounter,
					new AutoUpdate.ConfiguredAzureUpdateDownloader(),
					tempFilesManager,
					model,
					invokingSynchronization,
					firstStartDetector
				);
	
				var presentersFacade = new UI.Presenters.Facade();
				UI.Presenters.IPresentersFacade navHandler = presentersFacade;

				UI.Presenters.IClipboardAccess clipboardAccess = new UI.ClipboardAccess();

				UI.Presenters.LoadedMessages.IView loadedMessagesView = mainWindow.LoadedMessagesControlAdapter;
				UI.Presenters.LoadedMessages.IPresenter loadedMessagesPresenter = new UI.Presenters.LoadedMessages.Presenter(
					model,
					loadedMessagesView,
					navHandler,
					heartBeatTimer,
					clipboardAccess);

				UI.Presenters.LogViewer.IPresenter viewerPresenter = loadedMessagesPresenter.LogViewerPresenter;

				UI.Presenters.StatusReports.IPresenter statusReportPresenter = new UI.Presenters.StatusReports.Presenter(
					mainWindow.StatusPopupControlAdapter,
					heartBeatTimer
				);

				UI.Presenters.SourcesList.IPresenter sourcesListPresenter = new UI.Presenters.SourcesList.Presenter(
					model,
					mainWindow.SourcesManagementControlAdapter.SourcesListControlAdapter,
					logSourcesPreprocessings,
					null,// todo new UI.Presenters.SourcePropertiesWindow.Presenter(new UI.SourceDetailsWindowView(), navHandler),
					viewerPresenter,
					navHandler);

				UI.Presenters.SearchResult.IPresenter searchResultPresenter = new UI.Presenters.SearchResult.Presenter(
					model,
					mainWindow.SearchResultsControlAdapter,
					navHandler,
					loadedMessagesPresenter,
					heartBeatTimer,
					filtersFactory,
					clipboardAccess);
				
				UI.Presenters.SearchPanel.IPresenter searchPanelPresenter = new UI.Presenters.SearchPanel.Presenter(
					model,
					mainWindow.SearchPanelControlAdapter,
					mainWindow,
					viewerPresenter,
					searchResultPresenter,
					statusReportPresenter
				);
				tracer.Info("search panel presenter created");

				UI.Presenters.HistoryDialog.IView historyDialogView = new UI.HistoryDialogAdapter();
				UI.Presenters.HistoryDialog.IPresenter historyDialogPresenter = new UI.Presenters.HistoryDialog.Presenter(
					historyDialogView,
					model,
					logSourcesPreprocessings,
					preprocessingStepsFactory,
					recentlyUsedLogs,
					new UI.Presenters.QuickSearchTextBox.Presenter(historyDialogView.QuickSearchTextBox)
				);

				UI.Presenters.WebBrowserDownloader.IPresenter webBrowserDownloaderWindowPresenter = new UI.Presenters.WebBrowserDownloader.Presenter(
					new LogJoint.UI.WebBrowserDownloaderWindowController(),
					invokingSynchronization,
					webContentCache
				);

				UI.Presenters.MainForm.ICommandLineHandler commandLineHandler = null; // todo

				UI.Presenters.NewLogSourceDialog.IPagePresentersRegistry newLogSourceDialogPagesPresentersRegistry = 
					new UI.Presenters.NewLogSourceDialog.PagePresentersRegistry();

				UI.Presenters.NewLogSourceDialog.IPresenter newLogSourceDialogPresenter = new UI.Presenters.NewLogSourceDialog.Presenter(
					logProviderFactoryRegistry,
					newLogSourceDialogPagesPresentersRegistry,
					recentlyUsedLogs,
					new UI.NewLogSourceDialogView(),
					userDefinedFormatsManager,
					() => new UI.Presenters.NewLogSourceDialog.Pages.FormatDetection.Presenter(
						new LogJoint.UI.FormatDetectionPageController(),
						logSourcesPreprocessings,
						preprocessingStepsFactory
					),
					null // formatsWizardPresenter
				);

				newLogSourceDialogPagesPresentersRegistry.RegisterPagePresenterFactory(
					StdProviderFactoryUIs.FileBasedProviderUIKey,
					f => new UI.Presenters.NewLogSourceDialog.Pages.FileBasedFormat.Presenter(
						new LogJoint.UI.FileBasedFormatPageController(), 
						(IFileBasedLogProviderFactory)f,
						model
					)
				);

				UI.Presenters.SourcesManager.IPresenter sourcesManagerPresenter = new UI.Presenters.SourcesManager.Presenter(
					model,
					mainWindow.SourcesManagementControlAdapter,
					logSourcesPreprocessings,
					preprocessingStepsFactory,
					workspacesManager,
					sourcesListPresenter,
					newLogSourceDialogPresenter,
					heartBeatTimer,
					null,//sharingDialogPresenter,
					historyDialogPresenter,
					presentersFacade
				);

				UI.Presenters.BookmarksList.IPresenter bookmarksListPresenter = new UI.Presenters.BookmarksList.Presenter(
					model, 
					mainWindow.BookmarksManagementControlAdapter.ListView,
					heartBeatTimer,
					loadedMessagesPresenter,
					clipboardAccess);

				UI.Presenters.BookmarksManager.IPresenter bookmarksManagerPresenter = new UI.Presenters.BookmarksManager.Presenter(
					model,
					mainWindow.BookmarksManagementControlAdapter,
					viewerPresenter,
					searchResultPresenter,
					bookmarksListPresenter,
					statusReportPresenter,
					navHandler,
					viewUpdates);

				UI.Presenters.MainForm.IDragDropHandler dragDropHandler = new UI.DragDropHandler(
					logSourcesPreprocessings,
					preprocessingStepsFactory,
					model
				);

				new UI.LogsPreprocessorUI(
					logSourcesPreprocessings,
					statusReportPresenter
				);

				UI.Presenters.About.IPresenter aboutDialogPresenter = new UI.Presenters.About.Presenter(
                	new UI.AboutDialogAdapter(),
                	new UI.AboutDialogConfig(),
                	clipboardAccess,
					autoUpdater
				);

				UI.Presenters.Timeline.IPresenter timelinePresenter = new UI.Presenters.Timeline.Presenter(
					logSourcesManager,
					bookmarks,
					mainWindow.TimelinePanelControlAdapter.TimelineControlAdapter,
					viewerPresenter,
					statusReportPresenter,
					null, // tabUsageTracker
					heartBeatTimer
				);

				new UI.Presenters.TimelinePanel.Presenter(
					model,
					mainWindow.TimelinePanelControlAdapter,
					timelinePresenter,
					heartBeatTimer
				);

				UI.Presenters.MainForm.IPresenter mainFormPresenter = new UI.Presenters.MainForm.Presenter(
					model,
					mainWindow,
					viewerPresenter,
					searchResultPresenter,
					searchPanelPresenter,
					sourcesListPresenter,
					sourcesManagerPresenter,
					timelinePresenter,
					null,//messagePropertiesDialogPresenter,
					loadedMessagesPresenter,
					commandLineHandler,
					bookmarksManagerPresenter,
					heartBeatTimer,
					null,//tabUsageTracker,
					statusReportPresenter,
					dragDropHandler,
					navHandler,
					null,//optionsDialogPresenter,
					autoUpdater,
					progressAggregator,
					historyDialogPresenter,
					aboutDialogPresenter
				);
				tracer.Info("main form presenter created");

				((AppShutdown)shutdown).Attach(mainFormPresenter);

				presentersFacade.Init(
					null, //messagePropertiesDialogPresenter,
					null, //threadsListPresenter,
					sourcesListPresenter,
					bookmarksManagerPresenter,
					mainFormPresenter);

				modelHost.Init(viewerPresenter, viewUpdates);

				var extensibilityEntryPoint = new Extensibility.Application(
					new Extensibility.Model(
						invokingSynchronization,
						telemetryCollector,
						webContentCache,
						storageManager,
						bookmarks,
						logSourcesManager,
						modelThreads,
						tempFilesManager,
						preprocessingManagerExtensionsRegistry,
						logSourcesPreprocessings,
						progressAggregator,
						logProviderFactoryRegistry,
						userDefinedFormatsManager,
						recentlyUsedLogs,
						progressAggregatorsFactory
					),
					new Extensibility.Presentation(
						loadedMessagesPresenter,
						clipboardAccess,
						presentersFacade,
						sourcesManagerPresenter,
						webBrowserDownloaderWindowPresenter,
						newLogSourceDialogPresenter
					),
					new Extensibility.View(
					)
				);

				new Extensibility.PluginsManager(
					extensibilityEntryPoint,
					mainFormPresenter,
					telemetryCollector,
					shutdown
				);
			}
		}
	}
}

