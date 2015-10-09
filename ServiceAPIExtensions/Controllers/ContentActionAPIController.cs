using EPiServer.ServiceApi.Configuration;
using EPiServer.ServiceApi.Models;
using EPiServer.ServiceLocation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace ServiceAPIExtensions.Controllers
{
    /// <summary>
    /// API for managing content actions - e.g. list content waiting for approval, approve content, disapprove, ...
    /// </summary>
    [AuthorizePermission("EPiServerServiceApi", "ReadAccess"), RequireHttps, RoutePrefix("episerverapi/content/actions")]
    public class ContentActionAPIController : ApiController
    {
        //Mobile effort: 1) content/catalog presentation, 2) simple editing app, 3) marketing

        [HttpGet,Route("Ready")]
        public string ListContentReadyToPublish()
        {
            //List content ready to publish, that the current user can publish
            return "";
        }
    }
}