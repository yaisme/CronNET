using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CronNET
{
    public interface ICronJob
    {
        void execute(DateTime date_time);
        void abort();
    }

    public enum CronJobRunMode
    {
        RunOneInstance,
        RunInQueue,
        RunInParallel
    }

    public class CronJob : ICronJob
    {
        private readonly ICronSchedule _cron_schedule = new CronSchedule();
        private readonly Action _job_action;
        private Task _task;
        private TimeZoneInfo timeZoneInfo;

        protected CancellationTokenSource cancelToken { get; set; }
        protected int jobTaskQueueUpperLimit { get; set; }
        protected IList<Task> activeJobTaskQueue { get; set; }

        public CronJobRunMode runMode { get; private set; }

        public CronJob(string schedule, Action jobAction, string timeZoneId, CronJobRunMode runMode = CronJobRunMode.RunInParallel, int jobTaskQueueUpperLimit = 5)
        {
            this.runMode = runMode;
            this.jobTaskQueueUpperLimit = jobTaskQueueUpperLimit;
            activeJobTaskQueue = new List<Task>();
            cancelToken = new CancellationTokenSource();

            _cron_schedule = new CronSchedule(schedule);
            _job_action = jobAction;

            if (!string.IsNullOrEmpty(timeZoneId))
            {
                timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }

        }

        private readonly object _lock = new object();
        public void execute(DateTime date_time)
        {
            var runTime = date_time;
            if (timeZoneInfo != null)
            {
                runTime = TimeZoneInfo.ConvertTime(date_time, timeZoneInfo);
            }
            var needJobQueue = runMode != CronJobRunMode.RunOneInstance;

            lock (_lock)
            {
                if (needJobQueue)
                {
                    cleanActiveJobTaskQueue();

                    if (activeJobTaskQueue.Count >= this.jobTaskQueueUpperLimit)
                    {
                        return;
                    }
                }

                if (!_cron_schedule.isTime(runTime))
                    return;

                switch (runMode)
                {
                    case CronJobRunMode.RunOneInstance:
                        abort();
                        _task = Task.Factory.StartNew(_job_action, cancelToken.Token);
                        return;
					case CronJobRunMode.RunInParallel:
						activeJobTaskQueue.Add(_task);
						_task = Task.Factory.StartNew(_job_action, cancelToken.Token);
						activeJobTaskQueue.Add(_task);
                        return;
                    case CronJobRunMode.RunInQueue:
                        var lastTaskInQueue = activeJobTaskQueue.LastOrDefault();
                        if (lastTaskInQueue != null)
                        {
                            _task = lastTaskInQueue.ContinueWith((preTask) => { _job_action(); }, cancelToken.Token);
                        }
                        else
                        {
                            _task = Task.Factory.StartNew(_job_action, cancelToken.Token);
						}
						activeJobTaskQueue.Add(_task);
                        return;
                }
            }
        }

        public void abort()
        {
            cancelToken.Cancel();
            cancelToken = new CancellationTokenSource();
            if (runMode != CronJobRunMode.RunOneInstance)
            {
                activeJobTaskQueue = new List<Task>();
            }
        }

        protected void cleanActiveJobTaskQueue()
        {
            activeJobTaskQueue.Where((jobTask) => jobTask.IsFaulted || jobTask.IsCanceled || jobTask.IsCompleted)
                              .ToList()
                              .ForEach((jobTask) => activeJobTaskQueue.Remove(jobTask));
        }

    }
}
