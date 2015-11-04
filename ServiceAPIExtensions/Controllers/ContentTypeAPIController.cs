using EPiServer;
using EPiServer.Core;
using EPiServer.Core.Transfer;
using EPiServer.DataAbstraction;
using EPiServer.ServiceApi.Models;
using EPiServer.ServiceLocation;
using EPiServer.SpecializedProperties;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using EPiServer.ServiceApi.Configuration;
using EPiServer.Framework.Blobs;
using System.IO;

namespace ServiceAPIExtensions.Controllers
{
    [/*AuthorizePermission("EPiServerServiceApi", "ReadAccess"),*/RequireHttps, RoutePrefix("episerverapi/contenttype")]
    public class ContentTypeAPIController : ApiController
    {
        protected IContentRepository _repo = ServiceLocator.Current.GetInstance<IContentRepository>();
        protected IContentTypeRepository _typerepo = ServiceLocator.Current.GetInstance<IContentTypeRepository>();
        protected RawContentRetriever _rc = ServiceLocator.Current.GetInstance<RawContentRetriever>();
        protected BlobFactory _blobfactory = ServiceLocator.Current.GetInstance<BlobFactory>();
        protected ContentMediaResolver _mediaDataResolver = ServiceLocator.Current.GetInstance<ContentMediaResolver>();


        public virtual ContentType LocateContentType(string type)
        {
            //Try guid, try ID, try name
            Guid g=Guid.Empty;
            if (Guid.TryParse(type, out g)) return _typerepo.Load(g);
            int i = 0;
            if (Int32.TryParse(type, out i)) return _typerepo.Load(i);
            return _typerepo.Load(type);
        }

        private ExpandoObject ConstructExpandoObject(object o)
        {
            dynamic e = new ExpandoObject();
            var dic = e as IDictionary<string, object>;

            var t = o.GetType();
            foreach (var p in t.GetProperties())
            {
                if (p.PropertyType == typeof(string) || 
                    p.PropertyType == typeof(Int32) ||
                    p.PropertyType == typeof(Double) ||
                    p.PropertyType == typeof(bool) ||
                    p.PropertyType == typeof(DateTime) ||
                    p.PropertyType == typeof(Guid)
                    )
                {
                    dic.Add(p.Name, p.GetValue(o));
                }
                else if (p.PropertyType==typeof(PropertyDefinitionCollection))
                {
                    dic.Add(p.Name, (p.GetValue(o) as PropertyDefinitionCollection).Cast<object>().Select(pd => ConstructExpandoObject(pd)).ToList());
                } 
            }
            
            return e;
        }


        [HttpGet, Route("{ContentType}")]
        public virtual IHttpActionResult ContentTypeInfo(string ContentType)
        {
            var ct = LocateContentType(ContentType);
            if (ct == null) return NotFound();
            return Ok( ConstructExpandoObject(ct));
        }

        [HttpGet, Route("{ContentType}/zapierproperties")]
        public virtual IHttpActionResult ContentTypeZapierProperties(string ContentType)
        {
            var ct = LocateContentType(ContentType);
            if (ct == null) return NotFound();

            return Ok(ct.PropertyDefinitions.Select(pd =>
                new {key=pd.Name, type="unicode", required=false, label=pd.EditCaption, help_text=pd.HelpText}));
        }


        [HttpGet, Route("list")]
        public virtual IHttpActionResult ListContentTypes()
        {
            return Ok( _typerepo.List().Select(ct => ConstructExpandoObject(ct)).ToList());
        }

        [HttpGet, Route("typefor/{MediaExtension}")]
        public virtual IHttpActionResult ContentTypeForMedia(string MediaExtension)
        {
            if(!MediaExtension.StartsWith("."))   MediaExtension="."+MediaExtension;
            var mediatype = _mediaDataResolver.GetFirstMatching(MediaExtension.ToLower()); //Extension contains .
            var contentType=_typerepo.Load(mediatype);
            return ContentTypeInfo(contentType.Name);
        }

        //Add Blob

        [HttpGet]
        [Route("version")]
        public virtual ApiVersion Version()
        {
            return new ApiVersion() { Component = "ContentTypeAPI", Version = "1.0" };
        }
    }


}