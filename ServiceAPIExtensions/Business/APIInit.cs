using System;
using System.Linq;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using System.Web.Http;

namespace ServiceAPIExtensions.Business
{
    [InitializableModule]
    [ModuleDependency(typeof(EPiServer.ServiceApi.IntegrationInitialization))]
    public class APIInit : IInitializableModule
    {
        public void Initialize(InitializationEngine context)
        {
            GlobalConfiguration.Configure(delegate(HttpConfiguration config){
            
                config.EnsureInitialized();
                config.Formatters.Add(new BinaryMediaTypeFormatter());
            });
        }

        public void Preload(string[] parameters) { }

        public void Uninitialize(InitializationEngine context)
        {
            //Add uninitialization logic
        }
    }
}