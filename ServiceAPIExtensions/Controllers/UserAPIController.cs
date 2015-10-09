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
         public virtual HttpResponseMessage ListAllUsers()
         {
             var users=Membership.GetAllUsers().Cast<MembershipUser>().ToArray();
             //TODO: Support pagination
             var lst = new List<ExpandoObject>();
             foreach (var u in users)
             {
                 dynamic d = BuildUserObject(u);
                 lst.Add(d);
             }

             return Request.CreateResponse(HttpStatusCode.OK, lst.ToArray());
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
         public virtual HttpResponseMessage CreateUpdateUser(string UserName, [FromBody] dynamic Payload)
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
             
             return Request.CreateResponse(HttpStatusCode.OK);
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"),HttpPut, Route("roles/{rolename}")]
         public virtual HttpResponseMessage CreateRole(string rolename)
         {
             if (!Roles.RoleExists(rolename))
             {
                 Roles.CreateRole(rolename);
             }
             return Request.CreateResponse(HttpStatusCode.OK);
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"),HttpDelete, Route("roles/{rolename}")]
         public virtual HttpResponseMessage DeleteRole(string rolename)
         {
             if (Roles.RoleExists(rolename))
             {
                 Roles.DeleteRole(rolename);
             }
             else return Request.CreateResponse(HttpStatusCode.NotFound);
             return Request.CreateResponse(HttpStatusCode.OK);
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPost, Route("roles/{rolename}")]
         public virtual HttpResponseMessage AddUsersToRole(string rolename, [FromBody] dynamic Payload)
         {
             Roles.AddUsersToRole((string[])Payload.users, rolename);
             return Request.CreateResponse(HttpStatusCode.OK);
         }

         [AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpGet, Route("{UserName}")]
         public virtual HttpResponseMessage GetUser(string UserName)
         {
             var u = FindUser(UserName);
             return Request.CreateResponse(HttpStatusCode.OK, (ExpandoObject)BuildUserObject(u));
         }

         [AuthorizePermission("EPiServerServiceApi", "ReadAccess"), HttpGet, Route("{UserName}/roles")]
         public virtual HttpResponseMessage GetRolesForUser(string UserName)
         {

             var u = FindUser(UserName);
             var lst=Roles.GetRolesForUser(u.UserName);
             return Request.CreateResponse(HttpStatusCode.OK, lst);
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpPut, Route("{UserName}/roles")]
         public virtual HttpResponseMessage PutUserInRole(string UserName, [FromBody]dynamic Payload)
         {
             var u = FindUser(UserName);
             Roles.AddUserToRole(u.UserName, (string)Payload.Role);
             var lst = Roles.GetRolesForUser(u.UserName);
             return Request.CreateResponse(HttpStatusCode.OK, lst);
         }

         [AuthorizePermission("EPiServerServiceApi", "WriteAccess"), HttpDelete, Route("{UserName}/roles")]
         public virtual HttpResponseMessage RemoveUserFromRole(string UserName,[FromBody] dynamic Payload)
         {
             var u = FindUser(UserName);
             Roles.RemoveUserFromRole(u.UserName, (string) Payload.Role);
             var lst = Roles.GetRolesForUser(u.UserName);
             return Request.CreateResponse(HttpStatusCode.OK, lst);
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