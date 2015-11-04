using System;
using System.Linq;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;
using EPiServer.ServiceLocation;
using EPiServer.Core;
using System.Net;
using ServiceAPIExtensions.Business;
using ServiceAPIExtensions.Controllers;

namespace ContentAPI.Zapier
{
    [InitializableModule]
    [ModuleDependency(typeof(EPiServer.Web.InitializationModule))]
    public class ZapierInit : IInitializableModule
    {
        public void Initialize(InitializationEngine context)
        {
            //Add initialization logic, this method is called once after CMS has been initialized
            var events = ServiceLocator.Current.GetInstance<IContentEvents>();
            events.PublishedContent += events_PublishedContent;
            events.CreatedContent += events_CreatedContent;
            events.DeletedContent += events_DeletedContent;
            events.SavedContent += events_SavedContent;
            //User events?

        }

        void events_SavedContent(object sender, EPiServer.ContentEventArgs e)
        {
            RestHook.InvokeRestHooks("content_saved", ContentAPiController.ConstructExpandoObject(e.Content));
        }

        void events_DeletedContent(object sender, EPiServer.DeleteContentEventArgs e)
        {
            RestHook.InvokeRestHooks("content_deleted", ContentAPiController.ConstructExpandoObject(e.Content));
        }

        void events_CreatedContent(object sender, EPiServer.ContentEventArgs e)
        {
            RestHook.InvokeRestHooks("content_created", ContentAPiController.ConstructExpandoObject(e.Content));
        }

        void events_PublishedContent(object sender, EPiServer.ContentEventArgs e)
        {
            RestHook.InvokeRestHooks("content_published", ContentAPiController.ConstructExpandoObject(e.Content));
        }

        public void Preload(string[] parameters) { }

        public void Uninitialize(InitializationEngine context)
        {
            //Add uninitialization logic
        }

        
    }
}