﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FakeTimes
{
    public class FakeTime
    {
        internal static FakeTime CurrentTime => _currentTime.Value
            ?? throw new InvalidOperationException("FakeTime is not initialized");

        private static AsyncLocal<FakeTime> _currentTime = new AsyncLocal<FakeTime>();

        private DateTime _initialDateTime;
        public DateTime InitialDateTime
        {
            get { return _initialDateTime; }
            set
            {
                if (_started)
                    throw new InvalidOperationException($"Cannot change {nameof(InitialDateTime)} after test started");

                _initialDateTime = value;
            }
        }

        public DateTime Now { get; private set; } = DateTime.Now;

        private bool _started = false;

        private DeterministicTaskScheduler _deterministicTaskScheduler = new DeterministicTaskScheduler();

        public async Task Isolate(Func<Task> methodUnderTest, CancellationToken cancellationToken = default)
        {
            if (_currentTime.Value != null)
                throw new InvalidOperationException("Cannot run isolated test inside another isolated test");

            _currentTime.Value = this;
            Now = InitialDateTime;

            const string harmonyId = "com.github.Tolyandre.fake-time";
            var harmony = new Harmony(harmonyId);

            harmony.PatchAll(typeof(FakeTime).Assembly);

            _started = true;
           
            try
            {
                var taskFactory = new TaskFactory(CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach, TaskContinuationOptions.None, _deterministicTaskScheduler);

                // TODO: track tasks to ensure they are completed after method exits
                var wrapper = taskFactory.StartNew(async() =>
                {
                    Tick(TimeSpan.Zero);
                    await methodUnderTest();
                });

                _deterministicTaskScheduler.RunTasksUntilIdle();

                await wrapper.Unwrap();
            }
            finally
            {
                harmony.UnpatchAll(harmonyId);
                _currentTime.Value = null;
            }
        }

        // This collection is not concurrent, because one-task per time TaskScheduler is used
        private SortedList<DateTime, TaskCompletionSource<bool>> _waitList = new SortedList<DateTime, TaskCompletionSource<bool>>();

        public Task FakeDelay(TimeSpan duration)
        {
            if (duration == TimeSpan.Zero)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>(null);
            
            _waitList.Add(Now + duration, tcs);

            return tcs.Task;
        }

        public void Tick(TimeSpan duration)
        {
            var endTick = Now + duration;

            while (_waitList.Count > 0 && Now <= endTick)
            {
                var next = _waitList.First();
                if (next.Key > endTick)
                {
                    Now = endTick;
                    break;
                }

                Now = next.Key;

                next.Value.SetResult(false);
                _waitList.RemoveAt(0);

                _deterministicTaskScheduler.RunTasksUntilIdle();
            }
        }

        /// <summary>
        /// Throws if there are dalay tasks that not expired yet.
        /// </summary>
        public void ThrowIfDalayTasksNotCompleted()
        {
            var times = string.Join(", ", _waitList.Select(x => x.Key));
            if (!string.IsNullOrEmpty(times))
            {
                throw new DalayTasksNotCompletedException($"Current time is {Now}. One or many Dalay tasks are still waiting for time: {times}");
            }
        }
    }
}
