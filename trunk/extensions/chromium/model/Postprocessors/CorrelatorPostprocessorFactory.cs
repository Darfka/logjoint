﻿using System.Threading.Tasks;
using System.Linq;
using LogJoint.Postprocessing;
using LogJoint.Analytics;
using CDL = LogJoint.Chromium.ChromeDebugLog;
using WRD = LogJoint.Chromium.WebrtcInternalsDump;
using CD = LogJoint.Chromium.ChromeDriver;
using LogJoint.Postprocessing.Correlator;
using LogJoint.Analytics.Correlation;
using M = LogJoint.Analytics.Messaging;
using System.Collections.Generic;
using LogJoint.Analytics.Messaging.Analisys;
using System.Text;
using System;

namespace LogJoint.Chromium.Correlator
{
	public interface IPostprocessorsFactory
	{
		ILogSourcePostprocessor CreatePostprocessor(IPostprocessorsRegistry postprocessorsRegistry);
	};

	public class PostprocessorsFactory : IPostprocessorsFactory
	{
		readonly Extensibility.IModel ljModel;
		readonly IInvokeSynchronization modelThreadSync;
		readonly IPostprocessorsManager postprocessorsManager;

		public PostprocessorsFactory(Extensibility.IModel ljModel)
		{
			this.ljModel = ljModel;
			this.modelThreadSync = ljModel.ModelThreadSynchronization;
			this.postprocessorsManager = ljModel.Postprocessing.PostprocessorsManager;
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreatePostprocessor(IPostprocessorsRegistry postprocessorsRegistry)
		{
			return new LogSourcePostprocessorImpl(
				PostprocessorIds.Correlator, "Logs correlation",
				(data, source) => CorrelatorPostprocessorOutput.Parse(data),
				inputFiles => Run(inputFiles, postprocessorsRegistry)
			);
		}

		async Task<IPostprocessorRunSummary> Run(LogSourcePostprocessorInput[] inputFiles, IPostprocessorsRegistry postprocessorsRegistry)
		{
			var usedRoleInstanceNames = new HashSet<string>();
			Func<LogSourcePostprocessorInput, string> getUniqueRoleInstanceName = inputFile =>
			{
				for (int tryCount = 0; ; ++tryCount)
				{
					var ret = string.Format(
						tryCount == 0 ? "{0}" : "{0} ({1})",
						inputFile.LogSource.GetShortDisplayNameWithAnnotation(),
						tryCount
					);
					if (usedRoleInstanceNames.Add(ret))
						return ret;
				}
			};

			var noMessagingEvents = Task.FromResult(new List<M.Event>());

			var chromeDebugLogs =
				Enumerable.Empty<NodeInfo>()
				.Concat(
					inputFiles
					.Where(f => f.LogSource.Provider.Factory == postprocessorsRegistry.ChromeDebugLog.LogProviderFactory)
					.Select(inputFile =>
					{
						var reader = (new CDL.Reader(inputFile.CancellationToken)).Read(inputFile.LogFileName, inputFile.GetLogFileNameHint(), inputFile.ProgressHandler);
						IPrefixMatcher prefixMatcher = new PrefixMatcher();
						var nodeId = new NodeId("chrome-debug", getUniqueRoleInstanceName(inputFile));
						var matchedMessages = CDL.Helpers.MatchPrefixes(reader, prefixMatcher).Multiplex();
						var webRtcStateInspector = new CDL.WebRtcStateInspector(prefixMatcher);
						var processIdDetector = new CDL.ProcessIdDetector();
						var nodeDetectionTokenTask = (new CDL.NodeDetectionTokenSource(processIdDetector, webRtcStateInspector)).GetToken(matchedMessages);
						return new NodeInfo(
							new[] { inputFile.LogSource }, 
							nodeId, 
							matchedMessages,
							null,
							noMessagingEvents,
							nodeDetectionTokenTask
						);
					})
				)
				.ToList();

			var webRtcInternalsDumps =
				Enumerable.Empty<NodeInfo>()
				.Concat(
					inputFiles
					.Where(f => f.LogSource.Provider.Factory == postprocessorsRegistry.WebRtcInternalsDump.LogProviderFactory)
					.Select(inputFile =>
					{
						var reader = (new WRD.Reader(inputFile.CancellationToken)).Read(inputFile.LogFileName, inputFile.GetLogFileNameHint(), inputFile.ProgressHandler);
						IPrefixMatcher prefixMatcher = new PrefixMatcher();
						var nodeId = new NodeId("webrtc-int", getUniqueRoleInstanceName(inputFile));
						var matchedMessages = WRD.Helpers.MatchPrefixes(reader, prefixMatcher).Multiplex();
						var webRtcStateInspector = new WRD.WebRtcStateInspector(prefixMatcher);
						var nodeDetectionTokenTask = (new WRD.NodeDetectionTokenSource(webRtcStateInspector)).GetToken(matchedMessages);
						return new NodeInfo(
							new[] { inputFile.LogSource },
							nodeId,
							matchedMessages,
							null,
							noMessagingEvents,
							nodeDetectionTokenTask
						);
					})
				)
				.ToList();

			var chromeDriverLogs =
				Enumerable.Empty<NodeInfo>()
				.Concat(
					inputFiles
					.Where(f => f.LogSource.Provider.Factory == postprocessorsRegistry.ChromeDriver.LogProviderFactory)
					.Select(inputFile =>
					{
						var reader = (new CD.Reader(inputFile.CancellationToken)).Read(inputFile.LogFileName, inputFile.GetLogFileNameHint(), inputFile.ProgressHandler);
						IPrefixMatcher prefixMatcher = new PrefixMatcher();
						var nodeId = new NodeId("webrtc-int", getUniqueRoleInstanceName(inputFile));
						var matchedMessages = CD.Helpers.MatchPrefixes(reader, prefixMatcher).Multiplex();
						var nodeDetectionTokenTask = (new CD.NodeDetectionTokenSource(new CD.ProcessIdDetector(prefixMatcher), prefixMatcher)).GetToken(matchedMessages);
						return new NodeInfo(
							new[] { inputFile.LogSource },
							nodeId,
							matchedMessages,
							null,
							noMessagingEvents,
							nodeDetectionTokenTask
						);
					})
				)
				.ToList();

			var tasks = new List<Task>();
			tasks.AddRange(chromeDebugLogs.Select(l => l.SameNodeDetectionTokenTask));
			tasks.AddRange(chromeDebugLogs.Select(l => l.MultiplexingEnumerable.Open()));
			tasks.AddRange(webRtcInternalsDumps.Select(l => l.SameNodeDetectionTokenTask));
			tasks.AddRange(webRtcInternalsDumps.Select(l => l.MultiplexingEnumerable.Open()));
			tasks.AddRange(chromeDriverLogs.Select(l => l.SameNodeDetectionTokenTask));
			tasks.AddRange(chromeDriverLogs.Select(l => l.MultiplexingEnumerable.Open()));
			await Task.WhenAll(tasks);

			var allLogs =
				Enumerable.Empty<NodeInfo>()
				.Concat(chromeDebugLogs)
				.Concat(webRtcInternalsDumps)
				.Concat(chromeDriverLogs)
				.ToArray();

			var fixedConstraints =
				allLogs
				.GroupBy(l => l.SameNodeDetectionTokenTask.Result, new SameNodeEqualityComparer())
				.SelectMany(group => LinqUtils.ZipWithNext(group).Select(pair => new NodesConstraint()
				{
					Node1 = pair.Key.NodeId,
					Node2 = pair.Value.NodeId,
					Value = pair.Value.SameNodeDetectionTokenTask.Result.DetectSameNode(pair.Key.SameNodeDetectionTokenTask.Result).TimeDiff
				}))
				.ToList();

			var allowInstacesMergingForRoles = new HashSet<string>();

			ICorrelator correlator = new LogJoint.Analytics.Correlation.Correlator(
				new M.Analisys.InternodeMessagesDetector(),
				SolverFactory.Create()
			);
			var correlatorSolution = await correlator.Correlate(
				allLogs.ToDictionary(i => i.NodeId, i => (IEnumerable<M.Event>)i.MessagesTask.Result),
				fixedConstraints,
				allowInstacesMergingForRoles
			);

			var nodeIdToLogSources =
				(from l in allLogs
				 from ls in l.LogSources
				 select new { L = l.NodeId, Ls = ls })
				 .ToLookup(i => i.L, i => i.Ls);

			var grouppedLogsReport = new StringBuilder();
			foreach (var group in allLogs.Where(g => g.LogSources.Length > 1))
			{
				if (grouppedLogsReport.Length == 0)
				{
					grouppedLogsReport.AppendLine();
					grouppedLogsReport.AppendLine("Groupped logs info:");
				}
				grouppedLogsReport.AppendLine(string.Format(
					"  - {0} were groupped to represent node {1}",
					string.Join(", ", group.LogSources.Select(ls => '\"' + ls.GetShortDisplayNameWithAnnotation() + '\"')),
					group.NodeId));
			}

			var timeOffsets =
				(from ns in correlatorSolution.NodeSolutions
				 from ls in nodeIdToLogSources[ns.Key]
				 select new { Sln = ns.Value, Ls = ls })
				.ToDictionary(i => i.Ls, i => i.Sln);

			await modelThreadSync.Invoke(() =>
			{
				foreach (var ls in ljModel.SourcesManager.Items)
				{
					NodeSolution sln;
					if (timeOffsets.TryGetValue(ls, out sln))
					{
						ITimeOffsetsBuilder builder = new TimeOffsets.Builder();
						builder.SetBaseOffset(sln.BaseDelta);
						if (sln.TimeDeltas != null)
							foreach (var d in sln.TimeDeltas)
								builder.AddOffset(d.At, d.Delta);
						ls.TimeOffsets = builder.ToTimeOffsets();
					}
				}
			});

			var correlatedLogsConnectionIds = postprocessorsManager.GetCorrelatableLogsConnectionIds(inputFiles.Select(i => i.LogSource));

			foreach (var inputFile in inputFiles)
			{
				NodeSolution sln;
				if (!timeOffsets.TryGetValue(inputFile.LogSource, out sln))
					continue;
				(new CorrelatorPostprocessorOutput(sln, correlatedLogsConnectionIds)).Save(inputFile.OutputFileName);
			}

			return new CorrelatorPostprocessorRunSummary(correlatorSolution.Success,
				correlatorSolution.CorrelationLog + grouppedLogsReport.ToString());
		}

		class NodeInfo
		{
			public readonly ILogSource[] LogSources;
			public readonly NodeId NodeId;
			public readonly IMultiplexingEnumerableOpen MultiplexingEnumerable;
			public readonly Task<ILogPartToken> LogPartTask;
			public readonly Task<ISameNodeDetectionToken> SameNodeDetectionTokenTask;
			public readonly Task<List<M.Event>> MessagesTask;
			public NodeInfo(ILogSource[] logSources, NodeId nodeId, IMultiplexingEnumerableOpen multiplexingEnumerable,
				Task<ILogPartToken> logPartTask, Task<List<M.Event>> messagesTask, Task<ISameNodeDetectionToken> sameNodeDetectionTokenTask)
			{
				LogSources = logSources;
				NodeId = nodeId;
				MultiplexingEnumerable = multiplexingEnumerable;
				LogPartTask = logPartTask;
				MessagesTask = messagesTask;
				SameNodeDetectionTokenTask = sameNodeDetectionTokenTask ??
					Task.FromResult((ISameNodeDetectionToken)new NullNodeDetectionToken());
			}
		};
	}
}
