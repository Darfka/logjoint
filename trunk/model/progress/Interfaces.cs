﻿using System;
using System.ComponentModel;

namespace LogJoint.Progress
{
	public interface IProgressEventsSink : IDisposable
	{
		void SetValue(double value);
	}

	public interface IProgressAggregator
	{
		IProgressEventsSink CreateProgressSink();

		event EventHandler<EventArgs> ProgressStarted;
		event EventHandler<ProgressChangedEventArgs> ProgressChanged;
		event EventHandler<EventArgs> ProgressEnded;
	};
}