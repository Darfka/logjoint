﻿using LogJoint.Analytics.Correlation;
using System.Collections.Generic;

namespace LogJoint.Postprocessing.Correlator
{
	public interface IPostprocessorsFactory
	{
		void Init(IPostprocessorsManager postprocessorsManager);
		ILogSourcePostprocessor CreatePostprocessor();
	};

	public interface ICorrelatorPostprocessorOutput 
	{
		HashSet<string> CorrelatedLogsConnectionIds { get; }
		NodeSolution Solution { get; }
	};
}
