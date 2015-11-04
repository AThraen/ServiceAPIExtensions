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
using EPiServer.ServiceApi.Models;
using System.Web.Security;
using EPiServer.ServiceLocation;
using EPiServer.Shell.Profile;

namespace ServiceAPIExtensions.Controllers
{
    [RequireHttps, RoutePrefix("episerverapi/user")]
    public class UserAPIController : ApiController
    {

         [AuthorizePermission("EPiServerServiceApi", "ReadAccess"),HttpGet, Route("list")]
         public virtual IHttpActionResult ListAllUsers()
         {
             var users=Membership.GetAllUsers().Cast<MembershipUser>().ToArray();
             //TODO: Support pagination
             var lst = new List<ExpandoObject>();
             foreach (var u in users)
             {
                 dynamic d = BuildUserObject(u);
                 lst.Add(d);
             }

             return Ok(lst.ToArray());
         }


         private static void ProfileTest()
         {
             var profilerep = ServiceLocator.Current.GetInstance<IProfileRepository>();
             var p=profilerep.GetProfile("Allan");
             var a=EPiServer.Personalization.EPiServerProfile.Get("Admin");
         }
        
         //TODO Profile
         private static dynamic BuildUserObject(MembershipUser u)
         {
             dynamic d = new ExpandoObject();
             var dic = d as IDictionary<string, object>;
             //dic.Add("Number", 42);
             d.UserName = u.UserName;
             d.ProviderUserKey = u.ProviderUserKey;
             d.ProviderName = u.ProviderName;
             d.Email = u.Email;
             d.Comment = u.Comment;
             d.CreationDate = u.CreationDate;
             d.IsLockedOut = u.IsLockedOut;
             d.IsOnline = u.IsOnline;
             d.LastActivityDate = u.LastActivityDate;
             return d;
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"),HttpPut, Route("{UserName}")]
         public virtual IHttpActionResult CreateUpdateUser(string UserName, [FromBody] dynamic Payload)
         {
             var u = FindUser(UserName);
             if (u == null)
             {
                 //User does not exist, create
                 u=Membership.CreateUser(UserName, Membership.GeneratePassword(10, 2));
             }
             u.Email = (string) Payload.Email;
             u.LastLoginDate = (DateTime)Payload.LastLoginDate;
             Membership.UpdateUser(u);
             
             return Ok();
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"),HttpPut, Route("roles/{rolename}")]
         public virtual IHttpActionResult CreateRole(string rolename)
         {
             if (!Roles.RoleExists(rolename))
             {
                 Roles.CreateRole(rolename);
             }
             return Ok();
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"),HttpDelete, Route("roles/{rolename}")]
         public virtual IHttpActionResult DeleteRole(string rolename)
         {
             if (Roles.RoleExists(rolename))
             {
                 Roles.DeleteRole(rolename);
             }
             else return NotFound();
             return Ok();
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPost, Route("roles/{rolename}")]
         public virtual IHttpActionResult AddUsersToRole(string rolename, [FromBody] dynamic Payload)
         {
             Roles.AddUsersToRole((string[])Payload.users, rolename);
             return Ok();
         }

         [AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpGet, Route("{UserName}")]
         public virtual IHttpActionResult GetUser(string UserName)
         {
             var u = FindUser(UserName);
             return Request.CreateResponse(HttpStatusCode.OK, (ExpandoObject)BuildUserObject(u));
         }

         [AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpGet, Route("{UserName}/roles")]
         public virtual IHttpActionResult GetRolesForUser(string UserName)
         {

             var u = FindUser(UserName);
             var lst=Roles.GetRolesForUser(u.UserName);
             return Ok();
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPut, Route("{UserName}/roles")]
         public virtual IHttpActionResult PutUserInRole(string UserName, [FromBody]dynamic Payload)
         {
             var u = FindUser(UserName);
             Roles.AddUserToRole(u.UserName, (string)Payload.Role);
             var lst = Roles.GetRolesForUser(u.UserName);
             return Ok();
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpDelete, Route("{UserName}/roles")]
         public virtual IHttpActionResult RemoveUserFromRole(string UserName, [FromBody] dynamic Payload)
         {
             var u = FindUser(UserName);
             Roles.RemoveUserFromRole(u.UserName, (string) Payload.Role);
             var lst = Roles.GetRolesForUser(u.UserName);
             return Ok( lst);
         }


         private static MembershipUser FindUser(string UserName)
         {
             var col = Membership.FindUsersByName(UserName);
             if ((col.Count == 0) && (UserName.Contains('@'))) col = Membership.FindUsersByEmail(UserName);
             var u = col.Cast<MembershipUser>().FirstOrDefault();
             return u;
         }


         [HttpGet]
         [Route("version")]
         public virtual ApiVersion Version()
         {
             return new ApiVersion();
         } 
    }
}