using System;
using System.Linq;
using Quartz;
using Quartz.Collection;
using Quartz.Impl;
using Quartz.Spi;
using Topshelf.Logging;
using Topshelf.ServiceConfigurators;

namespace Topshelf.Quartz
{
    public static class ScheduleJobServiceConfiguratorExtensions
    {
        private static readonly Func<IScheduler> DefaultSchedulerFactory = () =>
                                                                               {
                                                                                   var schedulerFactory = new StdSchedulerFactory();
                                                                                   return schedulerFactory.GetScheduler();
                                                                               };

        private static Func<IScheduler> _customSchedulerFactory;
        private static IScheduler Scheduler;
        private static LogWriter Log = HostLogger.Get(typeof(ScheduleJobServiceConfiguratorExtensions));
        internal static IJobFactory JobFactory;

        public static Func<IScheduler> SchedulerFactory
        {
            get { return _customSchedulerFactory ?? DefaultSchedulerFactory; }
            set { _customSchedulerFactory = value; }
        }

        public static bool EnableLog { get; set; } = true;

        private static IScheduler GetScheduler()
        {
            var scheduler = SchedulerFactory();

            if (JobFactory != null)
                scheduler.JobFactory = JobFactory;

            return scheduler;
        }

        public static ServiceConfigurator<T> UsingQuartzJobFactory<T, TJobFactory>(this ServiceConfigurator<T> configurator, Func<TJobFactory> jobFactory)
            where T : class
            where TJobFactory : IJobFactory
        {
            JobFactory = jobFactory();
            return configurator;
        }

        public static ServiceConfigurator<T> UsingQuartzJobFactory<T, TJobFactory>(this ServiceConfigurator<T> configurator) where T : class where TJobFactory : IJobFactory, new()
        {
            return UsingQuartzJobFactory(configurator, () => new TJobFactory());
        }

        public static ServiceConfigurator<T> ScheduleQuartzJob<T>(
            this ServiceConfigurator<T> configurator,
            Action<QuartzConfigurator> jobConfigurator,
            bool replaceJob = false,
            bool keepSchedulerState = false) where T : class
        {
            ConfigureJob<T>(configurator, jobConfigurator, replaceJob, keepSchedulerState);
            return configurator;
        }

        private static void ConfigureJob<T>(
            ServiceConfigurator<T> configurator,
            Action<QuartzConfigurator> jobConfigurator,
            bool replaceJob = false,
            bool keepSchedulerState = false) where T : class
        {
            var jobConfig = new QuartzConfigurator();
            jobConfigurator(jobConfig);

            if (jobConfig.JobEnabled == null || jobConfig.JobEnabled() || (jobConfig.Job == null || jobConfig.Triggers == null))
            {
                var jobDetail = jobConfig.Job();
                var jobTriggers = jobConfig.Triggers.Select(triggerFactory => triggerFactory()).Where(trigger => trigger != null);
                var jobListeners = jobConfig.JobListeners;
                var triggerListeners = jobConfig.TriggerListeners;
                var scheduleListeners = jobConfig.ScheduleListeners;
                configurator.BeforeStartingService(() =>
                                                       {
                                                           Debug("[Topshelf.Quartz] Configuring Scheduler...");
                                                           if (Scheduler == null)
                                                               Scheduler = GetScheduler();

                                                           if (Scheduler != null && jobDetail != null && jobTriggers.Any())
                                                           {
                                                               var triggersForJob = new HashSet<ITrigger>(jobTriggers);
                                                               Scheduler.ScheduleJob(jobDetail, triggersForJob, replaceJob);
                                                               Info(string.Format("[Topshelf.Quartz] Scheduled Job: {0}", jobDetail.Key));

                                                               foreach (var trigger in triggersForJob)
                                                                   Info(string.Format("[Topshelf.Quartz] Job Schedule: {0} - Next Fire Time (local): {1}", trigger, trigger.GetNextFireTimeUtc().HasValue ? trigger.GetNextFireTimeUtc().Value.ToLocalTime().ToString() : "none"));

                                                               if (jobListeners.Any())
                                                               {
                                                                   foreach (var listener in jobListeners)
                                                                   {
                                                                       var config = listener();
                                                                       Scheduler.ListenerManager.AddJobListener(
                                                                           config.Listener, config.Matchers);
                                                                       Info(string.Format(
                                                                               "[Topshelf.Quartz] Added Job Listener: {0}",
                                                                               config.Listener.Name));
                                                                   }
                                                               }

                                                               if (triggerListeners.Any())
                                                               {
                                                                   foreach (var listener in triggerListeners)
                                                                   {
                                                                       var config = listener();
                                                                       Scheduler.ListenerManager.AddTriggerListener(config.Listener, config.Matchers);
                                                                       Info(string.Format(
                                                                               "[Topshelf.Quartz] Added Trigger Listener: {0}",
                                                                               config.Listener.Name));
                                                                   }
                                                               }
                                                               if (scheduleListeners.Any())
                                                               {
                                                                   foreach (var listener in scheduleListeners)
                                                                   {
                                                                       var schedListener = listener();
                                                                       Scheduler.ListenerManager.AddSchedulerListener(schedListener);
                                                                       string.Format(
                                                                           "[Topshelf.Quartz] Added Schedule Listener: {0}",
                                                                           schedListener.GetType());
                                                                   }
                                                               }

                                                               if (!keepSchedulerState)
                                                               {
                                                                   Scheduler.Start();
                                                                   Info("[Topshelf.Quartz] Scheduler started...");
                                                               }
                                                           }
                                                       });

                configurator.BeforeStoppingService(() =>
                                        {
                                            Debug("[Topshelf.Quartz] Scheduler shutting down...");
                                            if ((Scheduler != null) && (!keepSchedulerState))
                                                if (!Scheduler.IsShutdown)
                                                    Scheduler.Shutdown();
                                            Info("[Topshelf.Quartz] Scheduler shut down...");
                                        });
            }
        }

        private static void Debug(string message)
        {
            if (EnableLog)
                Log.Debug(message);
        }

        private static void Info(string message)
        {
            if (EnableLog)
                Log.Info(message);
        }
    }
}