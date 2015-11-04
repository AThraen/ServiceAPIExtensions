# ServiceAPIExtensions
An extension pack for the EPiServer ServiceAPI

Can be installed through Nuget on any EPiServer CMS 9.x site with the ServiceAPI installed.
Currently adds some ContentAPI and some ContentTypeAPI extensions, enabling you to do most CRUD operations on IContent as 
well as fetch meta data on Content Types.
Useful for integrations and migrations.

Currently supported syntax: 

GET /episerverapi/content/{reference}
https://episerverapi.azurewebsites.net/episerverapi/content/start
Get's Content at Reference. Reference can be guid, ID, 'root','start','globalblock','siteblock'.
Returns {"property":"value", ...}
Supports query parameter: Select={List of property names}

PUT /episerverapi/content/{reference}
Updates the content item with the body object content.

POST /episerverapi/content/{reference}/Publish
Publishes the content

GET /episerverapi/content/{reference}/{Property name}
Returns the string representation of that property

GET /episerverapi/content/{reference}/children
https://episerverapi.azurewebsites.net/episerverapi/content/start/children
Get's children of content. Returns {"Children":[{"property":"value", ...}, {}]}
Supports Query parameters: Skip and Take (for pagination), and Select={list of property names}.

POST /episerverapi/content/{parent-reference}/Create/{content-type}/{optional:Save Action}
https://episerverapi.azurewebsites.net/episerverapi/content/start/create/StandardPage
Creates a new content item below the provided parent, of the given type.
The Content Type should be the name of the content type.
The parent-reference should be a reference to an existing content item.
The post body should be resolvable to a Dictionary<string,object>. Whereever a property name matches, it'll assign the value.
If a property "SaveAction" is set to "Publish" it will publish the content right away.
Returns {'reference':'new reference'}.

POST /episerverapi/content/{reference}/upload/{name}
Uploads a blob to a given Media data content item (reference). The Name is the filename being uploaded. 
The POST body should contain the binary data.
Returns void.

GET /episerverapi/content/EnsurePathExist/{content-type}/{*path}
Ensures that the provided path exist - otherwise it'll create it using the assigned container content-type. 
Returns a {reference=last created node}.

GET /episerverapi/content/{reference}/move/{new parent ref}
Moves the content.


Content Type API

GET /episerverapi/contenttype/list
Lists all Content Types

GET /episerverapi/contenttype/{contenttype}
Returns details on that content type

GET /episerverapi/contenttype/typefor/{extension}
Returns the content type that can handle that specific media type.
