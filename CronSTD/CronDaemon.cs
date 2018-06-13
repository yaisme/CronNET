﻿using System;
using System.Collections.Generic;
using System.Timers;

namespace CronSTD
{
    public interface ICronDaemon
    {
        void AddJob(string schedule, Action action, string timeZoneId, CronJobRunMode runMode, int jobTaskQueueUpperLimit);
        void Start();
        void Stop();
    }

    public class CronDaemon : ICronDaemon
    {
        private readonly Timer timer = new Timer(30000);
        private readonly List<ICronJob> cron_jobs = new List<ICronJob>();
        private DateTime _last = DateTime.Now;

        public CronDaemon()
        {
            timer.AutoReset = true;
            timer.Elapsed += timer_elapsed;
        }

        public void AddJob(string schedule, Action jobAction, string timeZoneId, CronJobRunMode runMode = CronJobRunMode.RunInParallel, int jobTaskQueueUpperLimit = 5)
        {
            var cj = new CronJob(schedule, jobAction, timeZoneId, runMode, jobTaskQueueUpperLimit);
            cron_jobs.Add(cj);
        }

        public void Start()
        {
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();

            foreach (CronJob job in cron_jobs)
                job.abort();
        }

        private void timer_elapsed(object sender, ElapsedEventArgs e)
        {
            if (DateTime.Now.Minute != _last.Minute)
            {
                _last = DateTime.Now;
                foreach (ICronJob job in cron_jobs)
                    job.execute(DateTime.Now);
            }
        }
    }
}
