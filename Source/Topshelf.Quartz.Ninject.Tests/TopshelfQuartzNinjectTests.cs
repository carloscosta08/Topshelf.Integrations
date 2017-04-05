using System;
using NUnit.Framework;
using Quartz;
using Topshelf.Common.Tests;
using Topshelf.Ninject;

namespace Topshelf.Quartz.Ninject.Tests
{
    [TestFixture]
    public class TopshelfQuartzNinjectTests
    {
        [SetUp]
        public void Setup()
        {
            SampleJob.HasRun = false;
        }

        [Test]
        public void TestCanScheduleJobAlongsideService()
        {
            Host host = HostFactory.New(configurator =>
                                            {
                                                configurator.UseTestHost();
                                                configurator.UseNinject(new SampleNinjectModule());
                                                configurator.UseQuartzNinject();
                                                configurator.Service<SampleNinjectService>(s =>
                                                                                        {
                                                                                            s.ConstructUsingNinject();
                                                                                            s.WhenStarted((service, control) => service.Start());
                                                                                            s.WhenStopped((service, control) => service.Stop());
                                                                                            s.ScheduleQuartzJob(q => q.WithJob(() => JobBuilder.Create<SampleNinjectJob>().Build()).AddTrigger(() => TriggerBuilder.Create().WithSimpleSchedule(builder => builder.WithRepeatCount(0)).Build()));
                                                                                        });
                                            });
            host.Run();

            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(2.0));

            Assert.IsTrue(SampleJob.HasRun);
        }

        [Test]
        [Ignore("Ignored")]
        public void TestCanScheduleJobAsService()
        {
            Host host = HostFactory.New(configurator =>
            {
                configurator.UseTestHost();
                configurator.UseNinject(new SampleNinjectModule());
                configurator.UseQuartzNinject();

                configurator.ScheduleQuartzJobAsService(
                    q =>
                    q.WithJob(() => JobBuilder.Create<SampleNinjectJob>().Build()).AddTrigger(
                        () => TriggerBuilder.Create().WithSimpleSchedule(builder => builder.WithRepeatCount(0)).Build()));
            });

            host.Run();

            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(2.0));

            Assert.IsTrue(SampleJob.HasRun);
        }
    }
}