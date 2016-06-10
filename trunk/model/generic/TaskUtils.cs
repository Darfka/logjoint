﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.CompilerServices;

namespace LogJoint
{
	public static class TaskUtils
	{
		public static Task StartInThreadPoolTaskScheduler(Func<Task> taskStarter)
		{
			// this facory will start worker in the default (thread pool based) scheduler
			// even if current scheduler is not default
			var taskFactory = new TaskFactory<Task>(TaskScheduler.Default);
			return taskFactory.StartNew(taskStarter).Result;
		}

		public static Task<T> StartInThreadPoolTaskScheduler<T>(Func<Task<T>> taskStarter)
		{
			// this facory will start worker in the default (thread pool based) scheduler
			// even if current scheduler is not default
			var taskFactory = new TaskFactory<Task<T>>(TaskScheduler.Default);
			return taskFactory.StartNew(taskStarter).Result;
		}

		public static async Task ToTask(this CancellationToken cancellation)
		{
			var taskSource = new TaskCompletionSource<int> ();
			using (var cancellationRegistration = cancellation.Register(() => taskSource.TrySetResult(1)))
			{
				if (cancellation.IsCancellationRequested)
					taskSource.TrySetResult(1);
				await taskSource.Task;
			}
		}

		public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken token)
		{
			var completedTask = await Task.WhenAny(task, token.ToTask());
			if (completedTask == task)
				return await task;
			else
				throw new TaskCanceledException();
		}

		public static async Task WithCancellation(this Task task, CancellationToken token)
		{
			var completedTask = await Task.WhenAny(task, token.ToTask());
			if (completedTask == task)
				await task;
			else
				throw new TaskCanceledException();
		}

		public static async Task WithTimeout(this Task task, TimeSpan timeout)
		{
			var completedTask = await Task.WhenAny(task, Task.Delay(timeout));
			if (task.IsCompleted)
				await task;
			else
				throw new TimeoutException();
		}

		/// <summary>
		/// Returns a task that awaits Task.Yield(). 
		/// It's useful if one wants to have the result of Task.Yield() as a Task.
		/// Note that Task.Yield() does not return Task.
		/// </summary>
		public static async Task Yield()
		{
			await Task.Yield();
		}


		/// <summary>
		/// Helper that makes the continuation of calling async method to run in threadpool-based 
		/// syncrinization context.
		/// </summary>
		/// <example>
		/// async Task MyMethod()
		/// {
		///		// This code runs in current sync context. For example in UI ctx.
		///		await RunInCurrentContext();
		///		
		///		await TaskUtils.SwitchToThreadpoolContext();
		///		
		///		// Code below will run in thread-pool thread.
		///		// All subsequent await's will also capture threadpool-based sync context.
		///		await RunInThreadpool();
		/// }
		/// </example>
		public static ConfiguredTaskAwaitable SwitchToThreadpoolContext()
		{
			return TaskUtils.Yield().ConfigureAwait(continueOnCapturedContext: false);
		}

		/// <summary>
		/// Returns exception that made Taks end. It's either Task.Exception or
		/// instance of TaskCanceledException if task was canelled.
		/// Note that for some reason throwing TaskCanceledException withing a task
		/// leaves task's Exception propetry null.
		/// </summary>
		public static Exception GetTaskException(this Task t)
		{
			if (t.Exception != null)
				return t.Exception;
			if (t.IsCanceled)
				return new TaskCanceledException();
			return null;
		}
	};

	public class AwaitableVariable<T>
	{
		readonly object sync = new object();
		readonly bool isAutoReset;
		TaskCompletionSource<T> value = new TaskCompletionSource<T>();

		public AwaitableVariable(bool isAutoReset = false)
		{
			this.isAutoReset = isAutoReset;
		}

		public void Set(T x)
		{
			lock (sync)
			{
				if (value.Task.IsCompleted)
					value = new TaskCompletionSource<T>();
				value.SetResult(x);
			}
		}

		public void Reset()
		{
			lock (sync)
			{
				if (value.Task.IsCompleted)
					value = new TaskCompletionSource<T>();
			}
		}

		public async Task<T> Wait()
		{
			var ret = await value.Task;
			if (isAutoReset)
				Reset();
			return ret;
		}
	}

	public class SynchronizationContextSwitch : IDisposable
	{
		SynchronizationContext oldContext;

		public SynchronizationContextSwitch(SynchronizationContext newContext)
		{
			oldContext = SynchronizationContext.Current;
			SynchronizationContext.SetSynchronizationContext(newContext);
		}

		public void Dispose()
		{
			SynchronizationContext.SetSynchronizationContext(oldContext);
		}
	};
}
