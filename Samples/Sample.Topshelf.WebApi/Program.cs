using System;
using System.Web.Http;
using Ninject.Modules;
using Topshelf;
using Topshelf.Ninject;
using Topshelf.WebApi;
using Topshelf.WebApi.Ninject;

namespace Sample.Topshelf.WebApi
{
    internal class Program
    {
        private static void Main()
        {
            HostFactory.Run(c =>
            {
                c.UseNinject(new SampleModule()); //Initiates Ninject and consumes Modules

                c.Service<SampleService>(s =>
                {
                    //Specifies that Topshelf should delegate to Ninject for construction
                    s.ConstructUsingNinject();

                    s.WhenStarted((service, control) => service.Start());
                    s.WhenStopped((service, control) => service.Stop());

                    //Topshelf.WebApi - Begins configuration of an endpoint
                    s.WebApiEndpoint(api =>
                        //Topshelf.WebApi - Uses localhost as the domain, defaults to port 8080.
                        //You may also use .OnHost() and specify an alternate port.
                        api.OnLocalhost()
                            //Topshelf.WebApi - Pass a delegate to configure your routes
                            .ConfigureRoutes(Configure)
                            //Topshelf.WebApi.Ninject (Optional) - You may delegate controller
                            //instantiation to Ninject.
                            //Alternatively you can set the WebAPI Dependency Resolver with
                            //.UseDependencyResolver()
                            .UseNinjectDependencyResolver()
                            //Instantaties and starts the WebAPI Thread.
                            .Build());
                });
            });
        }

        private static void Configure(HttpRouteCollection routes)
        {
            routes.MapHttpRoute(
                    "DefaultApiWithId",
                    "Api/{controller}/{id}",
                    new { id = RouteParameter.Optional },
                    new { id = @"\d+" });
        }
    }

    public class SampleController : ApiController
    {
        private readonly ISampleDependency _dependency;

        public SampleController(ISampleDependency dependency)
        {
            _dependency = dependency;
        }

        public string Get(int id)
        {
            return string.Format("The id squared is: {0}", _dependency.Square(id));
        }
    }

    public class SampleModule : NinjectModule
    {
        public override void Load()
        {
            Bind<ISampleDependency>().To<SampleDependency>();
        }
    }

    public class SampleService
    {
        private readonly ISampleDependency _sample;

        public SampleService(ISampleDependency sample)
        {
            _sample = sample;
        }

        public bool Start()
        {
            Console.WriteLine("Sample Service Started.");
            Console.WriteLine("Sample Dependency: {0}", _sample);
            return _sample != null;
        }

        public bool Stop()
        {
            return _sample != null;
        }
    }

    public interface ISampleDependency
    {
        int Square(int id);
    }

    public class SampleDependency : ISampleDependency
    {
        public int Square(int id)
        {
            return id * id;
        }
    }
}