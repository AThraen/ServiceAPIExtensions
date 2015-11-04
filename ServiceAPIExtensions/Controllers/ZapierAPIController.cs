using EPiServer.ServiceApi.Configuration;
using EPiServer.ServiceApi.Models;
using EPiServer.ServiceLocation;
using ServiceAPIExtensions.Business;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http;

namespace ServiceAPIExtensions.Controllers
{
    /// <summary>
    /// API for managing content actions - e.g. list content waiting for approval, approve content, disapprove, ...
    /// </summary>
    [RoutePrefix("episerverapi/zapier")]
    public class ZapierAPIController : ApiController
    {
        //Mobile effort: 1) content/catalog presentation, 2) simple editing app, 3) marketing

        //Register webhook

        //Provide sample submission
        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPost, Route("resthook")]
        public IHttpActionResult RegisterWebhook([FromBody] ExpandoObject content)
        {
            dynamic d = content;
            string url = d.target_url;
            string evnt=(string) (content as IDictionary<string,object>)["event"];
            RestHook rh = new RestHook();
            rh.Url = url;
            rh.EventName = evnt;
            return Ok(new {id=rh.SaveRestHook().ToString()});
        }

        //Load Trigger fields for event.
        //Unregister web hook (delete)
        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpDelete, Route("resthook")]
        public IHttpActionResult UnRegisterWebhook([FromBody] ExpandoObject content)
        {
            dynamic d = content;
            RestHook.DeleteRestHook((string)d.id);
            return Ok();
        }

        [HttpGet, Route("Ready")]
        public string ListContentReadyToPublish()
        {
            //List content ready to publish, that the current user can publish
            
            return "";
        }
    }
}