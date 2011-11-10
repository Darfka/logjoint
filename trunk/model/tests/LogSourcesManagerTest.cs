﻿// The following code was generated by Microsoft Visual Studio 2005.
// The test owner should check each test for validity.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;
using System.ComponentModel;
using System.Collections.Generic;
using LogJoint;
using Rhino.Mocks;
using System.Threading;

namespace LogJointTests
{
	[TestClass()]
	public class LogSourcesManagerTest
	{

		class SynchronizeInvoke : ISynchronizeInvoke
		{
			class AsyncResult: IAsyncResult
			{
				static readonly ManualResetEvent alwaysSignalledEvt = new ManualResetEvent(true);

				public AsyncResult(object ret, Exception ex) { retVal = ret; exception = ex; }

				public object AsyncState { get { return null; } }

				public WaitHandle AsyncWaitHandle { get { return alwaysSignalledEvt; } }

				public bool CompletedSynchronously { get { return true; } }

				public bool  IsCompleted { get { return true; } }

				internal object retVal;
				internal Exception exception;
			};

			public IAsyncResult BeginInvoke(Delegate method, object[] args)
			{
				try
				{
					return new AsyncResult(method.DynamicInvoke(args), null);
				}
				catch (System.Reflection.TargetInvocationException ex)
				{
					return new AsyncResult(null, ex);
				}
			}

			public object EndInvoke(IAsyncResult result)
			{
				AsyncResult ar = (AsyncResult)result;
				if (ar.exception != null)
					throw ar.exception;
				return ar.retVal;
			}

			public object Invoke(Delegate method, object[] args)
			{
				return method.DynamicInvoke(args);
			}

			public bool InvokeRequired
			{
				get { return false; }
			}
		};

		[TestMethod()]
		public void NavigateToTest()
		{
			//MockRepository rep = new MockRepository();

			//UpdateTracker updates = new UpdateTracker();
			//Threads threads = new Threads();

			//ILogSourcesManagerHost host = (ILogSourcesManagerHost)rep.CreateMock(typeof(ILogSourcesManagerHost));
			//IInvokeSynchronization invoke = rep.CreateMock<IInvokeSynchronization>();
			//Expect.Call(invoke.InvokeRequired).Return(false).Repeat.Any();

			//Expect.Call(host.Tracer).Return(null);
			//Expect.Call(host.Updates).Return(updates);
			//Expect.Call(host.Threads).Return(threads);
			//Expect.Call(host.Invoker).Return(invoke).Repeat.Any();

			//rep.ReplayAll();

			//LogSourcesManager target = new LogSourcesManager(host);

			////target.

			//rep.VerifyAll();
		}

	}


}
