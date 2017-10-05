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
using EPiServer.Web.Internal;
using System.Reflection;
using Newtonsoft.Json;
using System.Text;

namespace ServiceAPIExtensions.Controllers
{
    [/*AuthorizePermission("EPiServerServiceApi", "WriteAccess"),*/ RequireHttps, RoutePrefix("episerverapi/content")]
    public class ContentAPiController : ApiController
    {
        protected IContentRepository _repo = ServiceLocator.Current.GetInstance<IContentRepository>();
        protected IContentTypeRepository _typerepo = ServiceLocator.Current.GetInstance<IContentTypeRepository>();
        protected IRawContentRetriever _rc = ServiceLocator.Current.GetInstance<IRawContentRetriever>();
        protected BlobFactory _blobfactory = ServiceLocator.Current.GetInstance<BlobFactory>();
        
        /// <summary>
        /// Finds the content with the given name
        /// </summary>
        /// <param name="Ref">The name of the content</param>
        /// <returns>The requested content on success or ContentReference.EmptyReference otherwise</returns>
        protected ContentReference LookupRef(string Ref)
        {
            if (Ref.ToLower() == "root") return ContentReference.RootPage;
            if (Ref.ToLower() == "start") return ContentReference.StartPage;
            if (Ref.ToLower() == "globalblock") return ContentReference.GlobalBlockFolder;
            if (Ref.ToLower() == "siteblock") return ContentReference.SiteBlockFolder;
            
            if (ContentReference.TryParse(Ref, out ContentReference c)) return c;

            Guid g=Guid.Empty;
            if (Guid.TryParse(Ref, out g)) EPiServer.Web.PermanentLinkUtility.FindContentReference(g);
            return ContentReference.EmptyReference;
        }

        /// <summary>
        /// Finds the content with a given name of its parent. Favours URLEncoded name over actual name.
        /// </summary>
        /// <param name="Parent">The reference to the parent</param>
        /// <param name="Name">The name of the content</param>
        /// <returns>The requested content on success or ContentReference.EmptyReference otherwise</returns>
        protected ContentReference LookupRef(ContentReference Parent, string Name)
        {
            var content = (new UrlSegment(_repo)).GetContentBySegment(Parent, Name);
            if (content != null && !content.Equals(ContentReference.EmptyReference))
            {
                return content;
            }
            
            var temp = _repo.GetChildren<IContent>(Parent).Where(ch => SegmentedName(ch.Name) == Name).FirstOrDefault();
            if (temp != null)
            {
                return temp.ContentLink;
            }

            return ContentReference.EmptyReference;
        }

        /// <summary>
        /// Finds the content with a given name and its type.  Favours URLEncoded name over actual name.
        /// </summary>
        /// <param name="Parent">The reference to the parent</param>
        /// <param name="ContentType">The content type</param>
        /// <param name="Name">The name of the content</param>
        /// <returns>The requested content on success or ContentReference.EmptyReference otherwise</returns>
        protected ContentReference LookupRef(ContentReference Parent, string ContentType, string Name)
        {
            var content = (new UrlSegment(_repo)).GetContentBySegment(Parent, Name);
            if (content != null) return content;
            else
            {
                var temp = _repo.GetChildren<IContent>(Parent).Where(ch => ch.GetType().Name == ContentType && ch.Name == Name).FirstOrDefault();
                if (temp != null) return temp.ContentLink;
                else return ContentReference.EmptyReference;
            }
        }
        
        public static ExpandoObject ConstructExpandoObject(IContent c, string Select=null)
        {
            return ConstructExpandoObject(c,true, Select);
        }

        public static ExpandoObject ConstructExpandoObject(IContent c, bool IncludeBinary,string Select=null)
        {
            dynamic e = new ExpandoObject();
            var dic=e as IDictionary<string,object>;

            if (c == null) return null;

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
                if (IncludeBinary && md.BinaryData != null)
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

        /*[AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpGet, Route("{Reference}/{language?}",Name="GetContentRoute")]
        public virtual IHttpActionResult GetContent(string Reference, string language=null, string Select=null)
        {
            var r=LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();

            try
            {
                var cnt = _repo.Get<IContent>(r);
                if (cnt == null) return NotFound();
                return Ok(ConstructExpandoObject(cnt, Select));
            } catch (ContentNotFoundException e)
            {
                return NotFound();
            }
        }*/

        /*[AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpGet, Route("{Reference}/{Property}")]
        public virtual IHttpActionResult GetProperty(string Reference, string Property)
        {
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();

            try
            {
                var cnt = _repo.Get<IContent>(r);
                if (cnt == null || !cnt.Property.Contains(Property)) return NotFound();
                return Ok(new { Property = cnt.Property[Property].ToWebString() });
            }
            catch (ContentNotFoundException e)
            {
                return NotFound();
            }
        }*/

        [AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpGet, Route("{Reference}/BinaryData")]
        public virtual IHttpActionResult GetBinaryContent(string Reference)
        {
            // Find the reference to the object.
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();

            try
            {
                // Get the binary contents.
                var cnt = _repo.Get<IContent>(r);
                var binary = cnt as IBinaryStorable;
                if (binary.BinaryData == null) return NotFound();

                // Return the binary contents as a stream.
                using (var br = new BinaryReader(binary.BinaryData.OpenRead()))
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Content = new ByteArrayContent(br.ReadBytes((int)br.BaseStream.Length));
                    if (cnt as IContentMedia != null)
                        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue((cnt as IContentMedia).MimeType);
                    return ResponseMessage(response);
                }
            }
            catch (ContentNotFoundException e)
            {
                return NotFound();
            }
            catch (NullReferenceException ex)
            {
                return NotFound();
            }
        }

        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPost, Route("{Reference}/Publish")]
        public virtual IHttpActionResult PublishContent(string Reference)
        {
            // Find the reference to the object.
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();

            // Save the content with the Publish action.
            _repo.Save(_repo.Get<IContent>(r), EPiServer.DataAccess.SaveAction.Publish);
            return Ok();
        }

        [AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpGet, Route("{Reference}/children")]
        public virtual IHttpActionResult ListChildren(string Reference, string Select = null, int Skip = 0, int Take = 100)
        {
            // Find the reference to the object.
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();

            // Check for invalid ID.
            try
            {
                _repo.Get<IContent>(r);
            } catch { return NotFound(); }

            // Collect all the children and create the response message.
            var children=_repo.GetChildren<IContent>(r).Skip(Skip).Take(Take).ToList();
            if (children.Count > 0)
            {
                dynamic e = new ExpandoObject();
                e.Children = children.Select(c => ConstructExpandoObject(c,false,Select)).ToArray();
                return Ok((ExpandoObject) e);
            }
            else return Ok(new ExpandoObject());
        }

        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpDelete, Route("{Reference}")]
        public virtual IHttpActionResult DeleteContent(string Reference)
        {
            // Find the reference to the object.
            var r = LookupRef(Reference);
            if (r == ContentReference.EmptyReference) return NotFound();

            // If its already in the wastebasket delete it, otherwise put it in the wastebasket.
            if (_repo.GetAncestors(r).Any(ic => ic.ContentLink == ContentReference.WasteBasket)) _repo.Delete(r, false);
            else _repo.MoveToWastebasket(r);
            return Ok();
        }

        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPut, Route("path/{*Path}")]
        public virtual IHttpActionResult UpdateContentByPath(string Path, [FromBody] ExpandoObject Updated, EPiServer.DataAccess.SaveAction action = EPiServer.DataAccess.SaveAction.Save)
        {
            // Find the reference to the object with a path.
            FindContentReference(Path, out ContentReference r);
            if (r == ContentReference.EmptyReference) return NotFound();

            var content = (_repo.Get<IContent>(r) as IReadOnly).CreateWritableClone() as IContent;
            var dic = Updated as IDictionary<string, object>;
            EPiServer.DataAccess.SaveAction saveaction = action;
            if (dic.ContainsKey("SaveAction") && ((string)dic["SaveAction"]) == "Publish")
            {
                saveaction = EPiServer.DataAccess.SaveAction.Publish;
                dic.Remove("SaveAction");
            }
            
            // Store the new information in the object.
            UpdateContentWithProperties(dic, content, out string error);
            if (!string.IsNullOrEmpty(error)) return BadRequest($"Invalid property '{error}'");

            // Save the reference and publish if requested.
            var rt = _repo.Save(content, saveaction);
            return Ok(new { reference = rt.ToString() });
        }
        
        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpDelete, Route("path/{*Path}")]
        public virtual IHttpActionResult DeleteContentByPath(string Path)
        {
            // Find the reference to the object with a path.
            FindContentReference(Path, out ContentReference r);
            if (r == ContentReference.EmptyReference) return NotFound();
            
            // If its already in the wastebasket delete it, otherwise put it in the wastebasket.
            if (_repo.GetAncestors(r).Any(ic => ic.ContentLink == ContentReference.WasteBasket)) _repo.Delete(r, false);
            else _repo.MoveToWastebasket(r);
            return Ok();
        }

        [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPost, Route("path/{*Path}")]
        public virtual IHttpActionResult CreateContentByPath(string Path, [FromBody] ExpandoObject content, EPiServer.DataAccess.SaveAction action = EPiServer.DataAccess.SaveAction.Save)
        {
            // Find the reference to the object with a path.
            FindContentReference(Path, out ContentReference r);
            if (r == ContentReference.EmptyReference) return NotFound();

            // Instantiate content of named type.
            var properties = content as IDictionary<string, object>;
            if (!properties.TryGetValue("ContentType", out object ContentType))
                return BadRequest("'ContentType' is a required field.");

            // Check ContentType.
            var ctype = _typerepo.Load((string)ContentType);
            if (ctype == null && int.TryParse((string)ContentType, out int j)) ctype = _typerepo.Load(j);
            if (ctype == null) return BadRequest($"'{ContentType}' is an invalid ContentType");

            // Remove 'ContentType' from properties before iterating properties.
            properties.Remove("ContentType");

            // Check if the object already exists.
            if (properties.TryGetValue("Name", out object name))
            {
                var temp = _repo.GetChildren<IContent>(r).Where(ch => ch.Name == (string)name).FirstOrDefault();
                if (temp != null) return BadRequest($"Content with name '{name}' already exists");
            }
            
            // Create content.
            IContent con = _repo.GetDefault<IContent>(r, ctype.ID);

            EPiServer.DataAccess.SaveAction saveaction = action;
            if (properties.ContainsKey("SaveAction") && (string)properties["SaveAction"] == "Publish")
            {
                saveaction = EPiServer.DataAccess.SaveAction.Publish;
                properties.Remove("SaveAction");
            }

            // Set the reference name.
            string _name = "";
            if (properties.ContainsKey("Name"))
            {
                _name = properties["Name"].ToString();
                properties.Remove("Name");
            }
            
            if (!string.IsNullOrEmpty(_name)) con.Name = _name;

            // Set all the other values.
            UpdateContentWithProperties(properties, con, out string error);
            if (!string.IsNullOrEmpty(error)) return BadRequest($"Invalid property '{error}'");

            // Save the reference with the requested save action.
            if (!string.IsNullOrEmpty(_name)) con.Name = _name;
            var rt=_repo.Save(con, saveaction);
            return Created<object>(Path, new { reference = rt.ToReferenceWithoutVersion() });
            //return Created<object>(new Uri(Url.Link("GetContentRoute",new {Reference=rt.ToReferenceWithoutVersion().ToString()})), new {reference=rt.ToReferenceWithoutVersion().ToString()});
        }

        [/*AuthorizePermission("EPiServerServiceApi", "ReadAccess"),*/ HttpGet, Route("path/{*Path}")]
        public virtual IHttpActionResult GetContentByPath(string Path)
        {
            // Extract the method from the path
            string method = Path.ToLower().Substring(Path.LastIndexOf("/")+1);
            if (method == "binarydata" || method == "children")
                Path = Path.Substring(0, Path.LastIndexOf("/"));

            // Find the reference to the object with a path.
            FindContentReference(Path, out ContentReference r);
            if (r == ContentReference.EmptyReference) return NotFound();

            if (method == "binarydata")
            {
                // Get the binary data from the reference.
                var cnt = _repo.Get<IContent>(r);

                if ((cnt is IBinaryStorable) && (cnt as IBinaryStorable).BinaryData != null)
                {
                    var binary = cnt as IBinaryStorable;
                    if (binary.BinaryData == null) return NotFound();
                    using (var br = new BinaryReader(binary.BinaryData.OpenRead()))
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.OK);
                        response.Content = new ByteArrayContent(br.ReadBytes((int)br.BaseStream.Length));
                        if (cnt as IContentMedia != null)
                            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue((cnt as IContentMedia).MimeType);
                        return ResponseMessage(response);
                    }
                }
                else return BadRequest("Resource does not have binary data");
            } else if (method == "children")
            {
                // Get all the children from the reference. 
                var children = _repo.GetChildren<IContent>(r).ToList();
                if (children.Count > 0)
                {
                    dynamic e = new ExpandoObject();
                    e.Children = children.Select(c => ConstructExpandoObject(c, false)).ToArray();
                    return Ok((ExpandoObject)e);
                }
                else return Ok(new ExpandoObject());
            } 

            // Return the information of the reference itself.
            var content = _repo.Get<IContent>(r);
            return Ok(ConstructExpandoObject(content));
        }

        [/*AuthorizePermission("EPiServerServiceApi", "ReadAccess"),*/ HttpGet, Route("type/{Type}")]
        public virtual IHttpActionResult GetContentType(string Type)
        {
            var episerverType = _typerepo.Load(Type);

            if(episerverType==null)
            {
                return NotFound();
            }

            var page = _repo.GetDefault<IContent>(ContentReference.RootPage, episerverType.ID);
            //we don't use episerverType.PropertyDefinitions since those don't include everything (PageCreated for example)

            return new JsonResult<object>(new
                {
                    TypeName = Type,
                    Properties = page.Property.Select(p => new { Name = p.Name, Type = p.Type.ToString() })
                },
                new JsonSerializerSettings(), Encoding.UTF8, this);
        }


        /*[AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPost, Route("{Ref}/Upload/{name}")]
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
        } */

        /*[AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpPost, Route("{Ref}/Move/{ParentRef}")]
        public virtual IHttpActionResult MoveContent(string Ref, string ParentRef)
        {
            var a = LookupRef(Ref);
            var b = LookupRef(ParentRef);
            if (a == null || b == null) return NotFound();
            _repo.Move(a, b);
            return Ok();
        } */

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

        private void UpdateContentWithProperties(IDictionary<string, object> properties, IContent con, out string error)
        {
            error = "";
            foreach (var k in properties.Keys)
            {
                UpdateFieldOnContent(properties, con, k, out error);
                if (!string.IsNullOrEmpty(error)) return;
            }
        }

        private void UpdateFieldOnContent(IDictionary<string, object> properties, IContent con, string k, out string error)
        {
            error = "";
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
                else if (con.Property[k].GetType() == typeof(EPiServer.Core.PropertyDate))
                {
                    if (properties[k] is DateTime)
                    {
                        con.Property[k].Value = properties[k];
                    }
                    else
                    {
                        con.Property[k].ParseToSelf((string)properties[k]);
                    }
                }
                else
                {
                    con.Property[k].Value = properties[k];
                }
            }
            else if (k.ToLower() == "binarydata" && con is MediaData)
            {
                dynamic binitm = properties[k];
                byte[] bytes = Convert.FromBase64String(binitm);
                WriteBlobToStorage(con.Name ?? (string)properties["Name"], bytes, con as MediaData);
            } else
            {
                error = k;
                return;
            }
        }

        /// <summary>
        /// Transforms a name into an URLEncoded name.
        /// </summary>
        /// <param name="name">The origional name</param>
        /// <returns>An URLEncoded name</returns>
        private string SegmentedName(string name)
        {
            return name.Replace(' ', '-').ToLower();
        }

        /// <summary>
        /// Finds the content reference of a path.
        /// </summary>
        /// <param name="Path">The path that needs to be recursed</param>
        /// <param name="r">The reference to the last item</param>
        private void FindContentReference(string Path, out ContentReference r)
        {
            var parts = Path.Split(new char[1] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            r = LookupRef(parts.First());
            string previousPart = "";
            foreach (var k in parts.Skip(1))
            {
                if (previousPart.ToLower().Equals("main"))
                {
                    try
                    {
                        var item = _repo.Get<IContent>(r).Property.Get("MainContentArea").Value as ContentArea;
                        ContentAreaItem contentArea = item.Items.Where(x => SegmentedName(x.GetContent().Name).Equals(k)).First();
                        if (contentArea == null)
                            contentArea = item.Items.Where(x => x.GetContent().Name.Equals(k)).First();

                        var olRef = r;
                        r = contentArea.ContentLink;
                    }
                    catch
                    {
                        r = ContentReference.EmptyReference;
                        return;
                    }
                }
                else if (previousPart.ToLower().Equals("related"))
                {
                    try
                    {
                        var item = _repo.Get<IContent>(r).Property.Get("RelatedContentArea").Value as ContentArea;
                        ContentAreaItem contentArea = item.Items.Where(x => SegmentedName(x.GetContent().Name).Equals(k)).First();
                        if (contentArea == null)
                            contentArea = item.Items.Where(x => x.GetContent().Name.Equals(k)).First();

                        var olRef = r;
                        r = contentArea.ContentLink;
                    }
                    catch
                    {
                        r = ContentReference.EmptyReference;
                        return;
                    }
                }
                else if (k.ToLower().Equals("main") || k.ToLower().Equals("related"))
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
        }

    }
}