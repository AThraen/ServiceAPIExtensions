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
using EPiServer.Web.Routing;
using EPiServer.Data.Entity;

namespace ServiceAPIExtensions.Controllers
{
    [/*AuthorizePermission("EPiServerServiceApi", "WriteAccess"),*/ RequireHttps, RoutePrefix("episerverapi/content")]
    public class ContentAPiController : ApiController
    {
        protected IContentRepository _repo = ServiceLocator.Current.GetInstance<IContentRepository>();
        protected IContentTypeRepository _typerepo = ServiceLocator.Current.GetInstance<IContentTypeRepository>();
        protected RawContentRetriever _rc= ServiceLocator.Current.GetInstance<RawContentRetriever>();
        protected BlobFactory _blobfactory = ServiceLocator.Current.GetInstance<BlobFactory>();


        protected ContentReference LookupRef(string Ref)
        {
            if (Ref.ToLower() == "root") return ContentReference.RootPage;
            if (Ref.ToLower() == "start") return ContentReference.StartPage;
            if (Ref.ToLower() == "globalblock") return ContentReference.GlobalBlockFolder;
            if (Ref.ToLower() == "siteblock") return ContentReference.SiteBlockFolder;
            ContentReference c=ContentReference.EmptyReference;
            if (ContentReference.TryParse(Ref, out c)) return c;
            Guid g=Guid.Empty;
            if (Guid.TryParse(Ref, out g)) EPiServer.Web.PermanentLinkUtility.FindContentReference(g);
            return ContentReference.EmptyReference;
        }

        protected ContentReference LookupRef(ContentReference Parent, string ContentType, string Name)
        {
            var content=_repo.GetChildren<IContent>(Parent).Where(ch => ch.GetType().Name == ContentType && ch.Name == Name).FirstOrDefault();
            if (content == null) return ContentReference.EmptyReference;
            return content.ContentLink;
        }


        //TODO: Query
        

        //Region: New approach - dynamic and Expando Objects

        private ExpandoObject ConstructExpandoObject(IContent c, string Select=null)
        {
            dynamic e = new ExpandoObject();
            var dic=e as IDictionary<string,object>;
            e.Name = c.Name;
            e.ParentLink = c.ParentLink;
            e.ContentGuid = c.ContentGuid;
            e.ContentLink = c.ContentLink;
            e.ContentTypeID = c.ContentTypeID;
            //TODO: Resolve Content Type
            var parts = (Select == null) ? null : Select.Split(',');
            foreach (var pi in c.Property)
            {
                if (parts != null && (!parts.Contains(pi.Name))) continue;

                if (pi.Value != null)
                {
                    if (pi.Type == PropertyDataType.Block)
                    {
                        //TODO: Doesn't work. Check SiteLogoType on start page
                        if(pi.Value is IContent)  dic.Add(pi.Name, ConstructExpandoObject((IContent)pi.Value));
                    }
                    else if (pi is EPiServer.SpecializedProperties.PropertyContentArea)
                    {
                        //TODO: Loop through and make array
                        var pca = pi as EPiServer.SpecializedProperties.PropertyContentArea;
                        ContentArea ca = pca.Value as ContentArea;
                        List<ExpandoObject> lst=new List<ExpandoObject>();
                        foreach(var itm in ca.Items){
                            dynamic itmobj = ConstructExpandoObject(itm.GetContent());
                            lst.Add(itmobj);
                        }
                        dic.Add(pi.Name, lst.ToArray());

                    } 
                    else if (pi.Value is string[])
                    {
                        dic.Add(pi.Name, (pi.Value as string[]));
                    }
                    else if (pi.Value is Int32  || pi.Value is Boolean || pi.Value is DateTime || pi.Value is Double)
                    {
                        dic.Add(pi.Name, pi.Value);
                    }
                    else { 
                        //TODO: Handle different return values
                        dic.Add(pi.Name, (pi.Value != null) ? pi.ToWebString() : null);
                    }
                }
                    
            }
            return e;
        }

        [/*AuthorizePermission("EPiServerServiceApi", "ReadAccess"),*/HttpGet, Route("{Reference}/{language?}")]
        public virtual HttpResponseMessage GetContent(string Reference, string language=null, string Select=null)
        {
            var r=LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return Request.CreateResponse(HttpStatusCode.NotFound);
            var cnt = _repo.Get<IContent>(r);
            if (cnt == null) return Request.CreateResponse(HttpStatusCode.NotFound);
            
            //TODO: Check permissions for user to content
            return Request.CreateResponse(HttpStatusCode.OK, ConstructExpandoObject(cnt, Select));
        }

        //TODO Languages, versions

        //TODO: Get Property, Put Property, Schedule Publish
        [/*AuthorizePermission("EPiServerServiceApi", "ReadAccess"),*/HttpGet, Route("{Reference}/{Property}")]
        public virtual HttpResponseMessage GetProperty(string Reference, string Property)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return Request.CreateResponse(HttpStatusCode.NotFound);
            var cnt = _repo.Get<IContent>(r);
            if (!cnt.Property.Contains(Property)) Request.CreateResponse(HttpStatusCode.NotFound);
            return Request.CreateResponse(HttpStatusCode.OK,new {Property=cnt.Property[Property].ToWebString()});
        }


        [/*AuthorizePermission("EPiServerServiceApi", "WriteAccess"),*/HttpPost, Route("{Reference}/Publish")]
        public virtual HttpResponseMessage PublishContent(string Reference)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return Request.CreateResponse(HttpStatusCode.NotFound);
            _repo.Save(_repo.Get<IContent>(r), EPiServer.DataAccess.SaveAction.Publish);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [/*AuthorizePermission("EPiServerServiceApi", "ReadAccess"),*/HttpGet, Route("{Reference}/children")]
        public virtual HttpResponseMessage ListChildren(string Reference, string Select=null, int Skip=0, int Take=100)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return Request.CreateResponse(HttpStatusCode.NotFound);
            var children=_repo.GetChildren<IContent>(r).Skip(Skip).Take(Take).ToList();
            if (children.Count > 0)
            {
                dynamic e = new ExpandoObject();
                e.Children = children.Select(c => ConstructExpandoObject(c,Select)).ToArray();
                return Request.CreateResponse(HttpStatusCode.OK, (ExpandoObject) e);
            }
            else return Request.CreateResponse(HttpStatusCode.OK);
        }
        
        [AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpPost,HttpGet, Route("{Reference}/query/{contenttype?}")]
        public virtual HttpResponseMessage QueryDescendents(string Reference, [FromBody] ExpandoObject Query,string contenttype=null, string Select = null, int Skip = 0, int Take = 100)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return Request.CreateResponse(HttpStatusCode.NotFound);
            var descendents = _repo.GetDescendents(r);
            List<IContent> ToReturn = new List<IContent>(Take+Skip);
            int Skipped = 0;
            foreach (var d in descendents)
            {
                var c = _repo.Get<IContent>(d);
                //if((contenttype!=null) && (c.ContentTypeID))
                //TODO: Apply Queries

                if (Skip > Skipped) { Skipped++; continue; }
                ToReturn.Add(c);
                if (ToReturn.Count == Take) break;
            }
            return Request.CreateResponse(HttpStatusCode.OK, ToReturn.Select(c =>ConstructExpandoObject(c, Select)).ToArray());
        }

        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPut, Route("{Reference}")]
        public virtual HttpResponseMessage PutContent(string Reference, [FromBody] ExpandoObject Updated, EPiServer.DataAccess.SaveAction action = EPiServer.DataAccess.SaveAction.Save)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return Request.CreateResponse(HttpStatusCode.NotFound);
            var content = (_repo.Get<IContent>(r) as IReadOnly).CreateWritableClone() as IContent;
            var dic=Updated as IDictionary<string, object>;
            UpdateContentWithProperties(dic, content);
            EPiServer.DataAccess.SaveAction saveaction = action;
            if (dic.ContainsKey("SaveAction") && ((string)dic["SaveAction"]) == "Publish")
            {
                saveaction = EPiServer.DataAccess.SaveAction.Publish;
            }
            var rt = _repo.Save(content, saveaction);

            return Request.CreateResponse(HttpStatusCode.OK, new { reference=rt.ToString()});
        }

        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpDelete, Route("{Reference}")]
        public virtual HttpResponseMessage DeleteContent(string Reference)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return Request.CreateResponse(HttpStatusCode.NotFound);
            if (_repo.GetAncestors(r).Any(ic => ic.ContentLink == ContentReference.WasteBasket))
            {
                //Already in waste basket, delete
                _repo.Delete(r, false);
            } else _repo.MoveToWastebasket(r);
            return Request.CreateResponse(HttpStatusCode.OK);
        }



        /// <summary>
        /// Remember to add parameter if it should be published...
        /// </summary>
        /// <param name="ParentRef"></param>
        /// <param name="ContentType"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPost, Route("{ParentRef}/Create/{ContentType}/{SaveAction?}")]
        public virtual HttpResponseMessage CreateContent(string ParentRef, string ContentType, [FromBody] ExpandoObject content, EPiServer.DataAccess.SaveAction action=EPiServer.DataAccess.SaveAction.Save)
        {
            //Instantiate content of named type
            var p = LookupRef(ParentRef);
            if (p == ContentReference.EmptyReference) return Request.CreateResponse(HttpStatusCode.NotFound);
            var ctype = _typerepo.Load(ContentType);
            if (ctype == null) return Request.CreateResponse(HttpStatusCode.NotFound);


            var properties = content as IDictionary<string, object>;

            IContent con = _repo.GetDefault<IContent>(p, ctype.ID);
            UpdateContentWithProperties(properties, con);
            //TODO: Handle local blocks. Handle properties that are not strings (parse values).

            if (properties.ContainsKey("Name")) con.Name = properties["Name"].ToString();
            EPiServer.DataAccess.SaveAction saveaction = action;
            if (properties.ContainsKey("SaveAction") && properties["SaveAction"]=="Publish")
            {
                saveaction = EPiServer.DataAccess.SaveAction.Publish;
            }
            var rt=_repo.Save(con, saveaction);
            return Request.CreateResponse(HttpStatusCode.OK, new {reference=rt.ToReferenceWithoutVersion().ToString()});
        }

        private void UpdateContentWithProperties(IDictionary<string, object> properties, IContent con)
        {
            foreach (var k in properties.Keys)
            {
                UpdateFieldOnContent(properties, con, k);
            }
        }

        private void UpdateFieldOnContent(IDictionary<string, object> properties, IContent con, string k)
        {
            //Problem: con might only contain very few properties (not inherited)
            if (con.Property.Contains(k))
            {

                if (con.Property[k] is EPiServer.SpecializedProperties.PropertyContentArea)
                {
                    //Handle if property is Content Area.
                    if (con.Property[k].Value == null) con.Property[k].Value = new ContentArea();
                    ContentArea ca = con.Property[k].Value as ContentArea;
                    var lst = properties[k];
                    if (lst is List<object>)
                    {
                        foreach (var s in (lst as List<object>))
                        {
                            var itmref = LookupRef(s.ToString());
                            ca.Items.Add(new ContentAreaItem() { ContentLink = itmref });
                        }
                    }
                }
                else if (properties[k] is string[])
                {
                    con.Property[k].Value = properties[k] as string[];
                }
                else
                {
                    con.Property[k].Value = properties[k];
                }
            }
            else if (k.ToLower() == "binarydata" && con is MediaData)
            {
                dynamic binitm = properties[k];
                string name = binitm.Name;
                byte[] bytes = Convert.FromBase64String(binitm.Data);
                WriteBlobToStorage(name, bytes, con as MediaData);
            }
        }




        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPost, Route("EnsurePathExist/{ContentType}/{*Path}")]
        public virtual HttpResponseMessage EnsurePathExist(string Path, string ContentType)
        {
            //Ensures that the path exists, otherwise create it using ContentType
            //If first element doesn't exist, assuming globalblock
            var parts = Path.Split('/');
            var r = LookupRef(parts.First());
            if (r == null) r = ContentReference.GlobalBlockFolder;
            HttpResponseMessage d = Request.CreateResponse(HttpStatusCode.OK, new { reference = r.ToString() });
            foreach (var k in parts.Skip(1))
            {
                //TODO: IF k does not exist at this path
                r = LookupRef(r, ContentType, k);
                dynamic dic = new ExpandoObject();
                dic.Name = k;
                d=CreateContent(r.ToString(), ContentType, dic, EPiServer.DataAccess.SaveAction.Publish);
            }
            return d;
        }

        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"),HttpPost, Route("{Ref}/Upload/{name}")]
        public virtual HttpResponseMessage UploadBlob(string Ref, string name, [FromBody] byte[] data)
        {
            var r = LookupRef(Ref);
            if (r == null) return Request.CreateResponse(HttpStatusCode.NotFound);
            var icnt=_repo.Get<IContent>(r);
            //TODO: Support Chunks - if blob already exist, extend on it.

            if (icnt is MediaData)
            {
                var md = (MediaData) (icnt as MediaData).CreateWritableClone();
                WriteBlobToStorage(name, data, md);
                _repo.Save(md, EPiServer.DataAccess.SaveAction.Publish); //Should we always publish?
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            return Request.CreateResponse(HttpStatusCode.UnsupportedMediaType);
        }


        private void WriteBlobToStorage(string name, byte[] data, MediaData md)
        {
            var blob = _blobfactory.CreateBlob(md.BinaryDataContainer, Path.GetExtension(name));
            using (var s = blob.OpenWrite())
            {
                BinaryWriter w = new BinaryWriter(s);
                w.Write(data);
                w.Flush();
            }
            md.BinaryData = blob;
        }

        
        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"),HttpGet, Route("{Ref}/Move/{ParentRef}")]
        public virtual HttpResponseMessage MoveContent(string Ref, string ParentRef)
        {
            var a = LookupRef(Ref);
            var b = LookupRef(ParentRef);
            if (a == null || b == null) return Request.CreateResponse(HttpStatusCode.NotFound);
            _repo.Move(a, b);
            return Request.CreateResponse(HttpStatusCode.OK);
        }
        

        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"),HttpGet,Route("validatewrite")]
        public virtual bool ValidateWriteAccess()
        {
            return true;
        }

        [AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpGet, Route("validateread")]
        public virtual bool ValidateReadAccess()
        {
            return true;
        }



        //Add Blob

        [HttpGet]
        [Route("version")]
        public virtual ApiVersion Version()
        {
            return new ApiVersion() { Component = "ContentAPI", Version = "1.0" };
        } 
    }


}