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
        protected IRawContentRetriever _rc = ServiceLocator.Current.GetInstance<IRawContentRetriever>();
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

        protected ContentReference LookupRef(ContentReference Parent, string Name)
        {
            var content = _repo.GetChildren<IContent>(Parent).Where(ch => ch.Name == Name).FirstOrDefault();
            if (content == null) return ContentReference.EmptyReference;
            return content.ContentLink;
        }


        //TODO: Query
        //Region: New approach - dynamic and Expando Objects

        public static ExpandoObject ConstructExpandoObject(IContent c, string Select=null)
        {
            return ConstructExpandoObject(c,true, Select);
        }

        public static ExpandoObject ConstructExpandoObject(IContent c, bool IncludeBinary,string Select=null)
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

            if (c is MediaData)
            {
                dynamic Media = new ExpandoObject();
                var md = c as MediaData;
                Media.MimeType = md.MimeType;
                Media.RouteSegment = md.RouteSegment;
                if (IncludeBinary)
                {
                    using (var br = new BinaryReader(md.BinaryData.OpenRead()))
                    {
                        Media.Binary = Convert.ToBase64String(br.ReadBytes((int)br.BaseStream.Length));
                    }
                }
                dic.Add("Media", Media);
            }
            
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

        [/*AuthorizePermission("EPiServerServiceApi", "ReadAccess"),*/HttpGet, Route("{Reference}/{language?}",Name="GetContentRoute")]
        public virtual IHttpActionResult GetContent(string Reference, string language=null, string Select=null)
        {
            var r=LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();
            var cnt = _repo.Get<IContent>(r);
            if (cnt == null) return NotFound();
            
            //TODO: Check permissions for user to content
            return Ok(ConstructExpandoObject(cnt, Select));
        }

        //TODO Languages, versions

        //TODO: Get Property, Put Property, Schedule Publish
        [/*AuthorizePermission("EPiServerServiceApi", "ReadAccess"),*/HttpGet, Route("{Reference}/{Property}")]
        public virtual IHttpActionResult GetProperty(string Reference, string Property)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();
            var cnt = _repo.Get<IContent>(r);
            if (!cnt.Property.Contains(Property)) return NotFound();
            return Ok(new {Property=cnt.Property[Property].ToWebString()});
        }

        [/*AuthorizePermission("EPiServerServiceApi", "ReadAccess"),*/HttpGet, Route("{Reference}/BinaryData")]
        public virtual IHttpActionResult GetBinaryContent(string Reference)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();
            var cnt = _repo.Get<IContent>(r);
            var md = cnt as MediaData;
            if (md.BinaryData == null) return NotFound();
            using (var br = new BinaryReader(md.BinaryData.OpenRead()))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new ByteArrayContent(br.ReadBytes((int)br.BaseStream.Length));
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                return ResponseMessage(response);
            }           
        }


        [/*AuthorizePermission("EPiServerServiceApi", "WriteAccess"),*/HttpPost, Route("{Reference}/Publish")]
        public virtual IHttpActionResult PublishContent(string Reference)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();
            _repo.Save(_repo.Get<IContent>(r), EPiServer.DataAccess.SaveAction.Publish);
            return Ok();
        }

        [/*AuthorizePermission("EPiServerServiceApi", "ReadAccess"),*/HttpGet, Route("{Reference}/children")]
        public virtual IHttpActionResult ListChildren(string Reference, string Select = null, int Skip = 0, int Take = 100)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();
            var children=_repo.GetChildren<IContent>(r).Skip(Skip).Take(Take).ToList();
            if (children.Count > 0)
            {
                dynamic e = new ExpandoObject();
                e.Children = children.Select(c => ConstructExpandoObject(c,false,Select)).ToArray();
                return Ok((ExpandoObject) e);
            }
            else return Ok();
        }
        
        [AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpPost,HttpGet, Route("{Reference}/query/{contenttype?}")]
        public virtual IHttpActionResult QueryDescendents(string Reference, [FromBody] ExpandoObject Query, string contenttype = null, string Select = null, int Skip = 0, int Take = 100)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();
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
            return Ok(ToReturn.Select(c =>ConstructExpandoObject(c, Select)).ToArray());
        }

        [/*AuthorizePermission("EPiServerServiceApi", "WriteAccess"),*/ HttpPut, Route("path/{*Reference}")]
        public virtual IHttpActionResult PutContent(string Reference, [FromBody] ExpandoObject Updated, EPiServer.DataAccess.SaveAction action = EPiServer.DataAccess.SaveAction.Save)
        {
            var parts = Reference.Split('/');
            var r = LookupRef(parts.First());
            string previousPart = "";

            foreach (var k in parts.Skip(1))
            {
                if (previousPart.Equals("main"))
                {
                    var item = _repo.Get<IContent>(r).Property.Get("MainContentArea").Value as ContentArea;

                    ContentAreaItem contentArea = item.Items.Where(x => x.GetContent().Name.Equals(k)).First();

                    var olRef = r;
                    r = contentArea.ContentLink;
                }
                else if (previousPart.Equals("related"))
                {
                    var item = _repo.Get<IContent>(r).Property.Get("RelatedContentArea").Value as ContentArea;

                    ContentAreaItem contentArea = item.Items.Where(x => x.GetContent().Name.Equals(k)).First();

                    var olRef = r;
                    r = contentArea.ContentLink;
                }
                else if (k.Equals("main") || k.Equals("related"))
                {
                    previousPart = k;
                    continue;
                }
                else
                {
                    var oldRef = r;
                    r = LookupRef(r, k);
                }

                previousPart = k;
            }

            if (r == ContentReference.EmptyReference) return NotFound();
            var content = (_repo.Get<IContent>(r) as IReadOnly).CreateWritableClone() as IContent;
            var dic=Updated as IDictionary<string, object>;
            UpdateContentWithProperties(dic, content);
            EPiServer.DataAccess.SaveAction saveaction = action;
            if (dic.ContainsKey("SaveAction") && ((string)dic["SaveAction"]) == "Publish")
            {
                saveaction = EPiServer.DataAccess.SaveAction.Publish;
            }
            var rt = _repo.Save(content, saveaction);

            return Ok( new { reference=rt.ToString()});
        }

        [/*AuthorizePermission("EPiServerServiceApi", "WriteAccess"),*/ HttpDelete, Route("{Reference}")]
        public virtual IHttpActionResult DeleteContent(string Reference)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();
            if (_repo.GetAncestors(r).Any(ic => ic.ContentLink == ContentReference.WasteBasket))
            {
                //Already in waste basket, delete
                _repo.Delete(r, false);
            } else _repo.MoveToWastebasket(r);
            return Ok();
        }

        [/*AuthorizePermission("EPiServerServiceApi", "WriteAccess"),*/ HttpDelete, Route("path/{*Path}")]
        public virtual IHttpActionResult DeleteContentByPath(string Path)
        {
            var parts = Path.Split('/');
            var r = LookupRef(parts.First());
            string previousPart = "";

            foreach (var k in parts.Skip(1))
            {
                if (previousPart.Equals("main"))
                {
                    var item = _repo.Get<IContent>(r).Property.Get("MainContentArea").Value as ContentArea;

                    ContentAreaItem contentArea = item.Items.Where(x => x.GetContent().Name.Equals(k)).First();

                    var olRef = r;
                    r = contentArea.ContentLink;
                }
                else if (previousPart.Equals("related"))
                {
                    var item = _repo.Get<IContent>(r).Property.Get("RelatedContentArea").Value as ContentArea;

                    ContentAreaItem contentArea = item.Items.Where(x => x.GetContent().Name.Equals(k)).First();

                    var olRef = r;
                    r = contentArea.ContentLink;
                }
                else if (k.Equals("main") || k.Equals("related"))
                {
                    //This part only shows that the next part should be in the MainContentArea or RelatedContentArea, So skip this part.
                    previousPart = k;
                    continue;
                }
                else
                {
                    var oldRef = r;
                    r = LookupRef(r, k);
                }

                previousPart = k;
            }

            if (_repo.GetAncestors(r).Any(ic => ic.ContentLink == ContentReference.WasteBasket))
            {
                //Already in waste basket, delete
                _repo.Delete(r, false);
            }
            else _repo.MoveToWastebasket(r);

            return Ok();

        }


        /// <summary>
        /// Remember to add parameter if it should be published...
        /// </summary>
        /// <param name="ParentRef"></param>
        /// <param name="ContentType"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        [/*AuthorizePermission("EPiServerServiceApi", "WriteUs"),*/ HttpPost, Route("{*ParentRef}")]
        public virtual IHttpActionResult CreateContent(string ParentRef, [FromBody] ExpandoObject content, EPiServer.DataAccess.SaveAction action = EPiServer.DataAccess.SaveAction.Save)
        {
            var parts = ParentRef.Split('/');
            var r = LookupRef(parts.First());

            string previousPart = "";
            string ContentType = "";

            foreach (var k in parts.Skip(1))
            {           
                if (previousPart.Equals("main"))
                {
                    var item = _repo.Get<IContent>(r).Property.Get("MainContentArea").Value as ContentArea;

                    ContentAreaItem contentArea = item.Items.Where(x => x.GetContent().Name.Equals(k)).First();

                    var olRef = r;
                    r = contentArea.ContentLink;
                }
                else if (previousPart.Equals("related"))
                {
                    var item = _repo.Get<IContent>(r).Property.Get("RelatedContentArea").Value as ContentArea;

                    ContentAreaItem contentArea = item.Items.Where(x => x.GetContent().Name.Equals(k)).First();

                    var olRef = r;
                    r = contentArea.ContentLink;
                }else if (previousPart.Equals("Create"))
                {
                    ContentType = k;
                }
                else if (k.Equals("main") || k.Equals("related") || k.Equals("Create"))
                {
                    //This part only shows that the next part should be in the MainContentArea or RelatedContentArea, So skip this part.
                    previousPart = k;
                    continue;
                }
                else
                {
                    var oldRef = r;
                    r = LookupRef(r, k);
                }

                previousPart = k;
            }

            //Instantiate content of named type
            var p = r;
            if (p == ContentReference.EmptyReference) return NotFound();
            int j = 0;

            var ctype = _typerepo.Load(ContentType);
            if (ctype==null && int.TryParse(ContentType, out j))
            {
                ctype = _typerepo.Load(j);
            }
            if (ctype == null) return NotFound();


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
            return Created<object>(new Uri(Url.Link("GetContentRoute",new {Reference=rt.ToReferenceWithoutVersion().ToString()})), new {reference=rt.ToReferenceWithoutVersion().ToString()});
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
        public virtual IHttpActionResult EnsurePathExist(string Path, string ContentType)
        {
            //Ensures that the path exists, otherwise create it using ContentType
            //If first element doesn't exist, assuming globalblock
            var parts = Path.Split('/');
            var r = LookupRef(parts.First());
            if (r == ContentReference.EmptyReference) r = ContentReference.GlobalBlockFolder;
            IHttpActionResult d = Ok( new { reference = r.ToString() });
            foreach (var k in parts.Skip(1))
            {
                var oldRef = r;
                //TODO: IF k does not exist at this path
                r = LookupRef(r, ContentType, k);
                if (r == ContentReference.EmptyReference)
                {
                    dynamic dic = new ExpandoObject();
                    dic.Name = k;
                    //d = CreateContent(oldRef.ToString(), ContentType, dic, EPiServer.DataAccess.SaveAction.Publish);
                }
            }
            return d;
        }

        [/*AuthorizePermission("EPiServerServiceApi", "WriteAccess"),*/ HttpGet, Route("path/{*Path}")]
        public virtual IHttpActionResult ProcessPath(string Path)
        {
            var parts = Path.Split('/');
            var r = LookupRef(parts.First());

            //Should we really do this?
            //if (r == ContentReference.EmptyReference) r = ContentReference.GlobalBlockFolder;

            string previousPart = "";

            foreach (var k in parts.Skip(1))
            {
                //endpoint for binary data
                if (k.Equals("BinaryData")){
                    var cnt = _repo.Get<IContent>(r);

                    IContent imageContent;
                    if (cnt.Property.Get("Image") != null)
                    {
                        imageContent = _repo.Get<IContent>(LookupRef(cnt.Property.Get("Image").ToString()));
                    }else if(cnt.Property.Get("Media") != null)
                    {
                        imageContent = cnt;
                    }
                    else
                    {
                        imageContent = cnt;
                    }
                    var md = imageContent as MediaData;
                    if (md.BinaryData == null) return NotFound();
                    using (var br = new BinaryReader(md.BinaryData.OpenRead()))
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.OK);
                        response.Content = new ByteArrayContent(br.ReadBytes((int)br.BaseStream.Length));
                        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(md.MimeType);
                        return ResponseMessage(response);
                    }
                }

                //endpoint for children
                if (k.Equals("children"))
                {
                    var children = _repo.GetChildren<IContent>(r).ToList();
                    if (children.Count > 0)
                    {
                        dynamic e = new ExpandoObject();
                        e.Children = children.Select(c => ConstructExpandoObject(c, false)).ToArray();
                        return Ok((ExpandoObject)e);
                    }
                }

                if (previousPart.Equals("main"))
                {
                    var item = _repo.Get<IContent>(r).Property.Get("MainContentArea").Value as ContentArea;

                    ContentAreaItem contentArea = item.Items.Where(x => x.GetContent().Name.Equals(k)).First();

                    var olRef = r;
                    r = contentArea.ContentLink;
                } else if (previousPart.Equals("related")) {
                    var item = _repo.Get<IContent>(r).Property.Get("RelatedContentArea").Value as ContentArea;

                    ContentAreaItem contentArea = item.Items.Where(x => x.GetContent().Name.Equals(k)).First();

                    var olRef = r;
                    r = contentArea.ContentLink;


                } else if (k.Equals("main") || k.Equals("related")) {
                    //This part only shows that the next part should be in the MainContentArea or RelatedContentArea, So skip this part.
                    previousPart = k;
                    continue;
                }
                else
                {
                    var oldRef = r;
                    r = LookupRef(r, k);
                }

                previousPart = k;
            }

            var content = _repo.Get<IContent>(r);

            return Ok(ConstructExpandoObject(content));
        }

        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"),HttpPost, Route("{Ref}/Upload/{name}")]
        public virtual IHttpActionResult UploadBlob(string Ref, string name, [FromBody] byte[] data)
        {
            var r = LookupRef(Ref);
            if (r == null) return NotFound();
            var icnt=_repo.Get<IContent>(r);
            //TODO: Support Chunks - if blob already exist, extend on it.

            if (icnt is MediaData)
            {
                var md = (MediaData) (icnt as MediaData).CreateWritableClone();
                WriteBlobToStorage(name, data, md);
                _repo.Save(md, EPiServer.DataAccess.SaveAction.Publish); //Should we always publish?
                return Ok();
            }
            return StatusCode(HttpStatusCode.UnsupportedMediaType);
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
        public virtual IHttpActionResult MoveContent(string Ref, string ParentRef)
        {
            var a = LookupRef(Ref);
            var b = LookupRef(ParentRef);
            if (a == null || b == null) return NotFound();
            _repo.Move(a, b);
            return Ok();
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
        [AuthorizePermission("EPiServerServiceApi", "ReadAccess"),Route("version")]
        public virtual ApiVersion Version()
        {
            return new ApiVersion() { Component = "ContentAPI", Version = "1.0" };
        } 
    }


}