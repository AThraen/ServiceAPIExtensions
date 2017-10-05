using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Text;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net.Http.Headers;

namespace ServiceAPIExtensionsTest
{
    [TestClass]
    public class ServiceAPIExtensionsTest
    {
        static Uri BaseURL = new Uri("https://localhost:44314/episerverapi");
        static string TestPagePath = "/content/path/start/test-pages";
        static string bearer;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

#if DEBUG
            ServicePointManager.ServerCertificateValidationCallback = delegate (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                { return true; };
#endif

            if (!Auth())
                Assert.Fail("Failed to authenticate!");
            
            JObject result = Post("/content/path/start", JObject.Parse(
                "{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Test Pages\"}"), out HttpStatusCode status);
            if (status != HttpStatusCode.Created)
                Assert.Fail("Failed to create TestPage structure!");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            JObject result = Delete(TestPagePath, out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.OK, status, "Failed to delete Test Pages, Please delete manually");
        }

        static bool Auth()
        {
            JObject result = Post("/token", "grant_type=password&username=xillio&password=Xilli0!");

            if (!string.IsNullOrEmpty(result["access_token"].ToString()))
            {
                bearer = result["access_token"].ToString();
                return true;
            }
            return false;
        }

        [TestMethod, TestCategory("GET")]
        public void GetStartPage()
        {
            JObject result = Get("/content/start");
            Assert.IsTrue(ValidatePage(result));
            Assert.AreEqual("Start", result["Name"].ToString());
            Assert.AreEqual("Start", result["PageName"].ToString());
            Assert.AreEqual("5", result["ContentLink"].ToString());
            Assert.AreEqual("start", result["PageURLSegment"].ToString());
            Assert.AreEqual(4, ((JArray)result["MainContentArea"]).Count);
        }

        #region Path Operation
        [TestMethod, TestCategory("GET"), TestCategory("PATH")]
        public void GetStartPageByPath()
        {
            JObject result = Get("/content/path/start");
            Assert.IsTrue(ValidatePage(result));
            Assert.AreEqual("Start", result["Name"].ToString());
            Assert.AreEqual("Start", result["PageName"].ToString());
            Assert.AreEqual("5", result["ContentLink"].ToString());
            Assert.AreEqual("start", result["PageURLSegment"].ToString());
            Assert.AreEqual(4, ((JArray)result["MainContentArea"]).Count);
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH")]
        public void GetNonExistentPageByPath()
        {
            JObject result = Get("/content/path/start/not-existing-page", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
            Assert.IsFalse(result.HasValues);
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH")]
        public void GetLongPath()
        {
            JObject result = Get("/content/path/start/alloy-plan/download-alloy-plan/start-downloading");
            Assert.IsTrue(ValidatePage(result));
            Assert.AreEqual("Installing", result["Name"].ToString());
            Assert.AreEqual("Installing", result["PageName"].ToString());
            Assert.AreEqual("8", result["ContentLink"].ToString());
            Assert.AreEqual("start-downloading", result["PageURLSegment"].ToString());
            Assert.AreEqual(2, ((JArray)result["MainContentArea"]).Count);
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH")]
        public void GetLongV2Path()
        {
            JObject result = Get("/content/path/start/how-to-buy/book-a-demo/thank-you");
            Assert.IsTrue(ValidatePage(result));
            Assert.AreEqual("Thank you", result["Name"].ToString());
            Assert.AreEqual("Thank you", result["PageName"].ToString());
            Assert.AreEqual("43", result["ContentLink"].ToString());
            Assert.AreEqual("thank-you", result["PageURLSegment"].ToString());
            Assert.AreEqual(1, ((JArray)result["MainContentArea"]).Count);
        }
        #endregion

        #region Children Operation
        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("CHILDREN")]
        public void GetChildrenStartPage()
        {
            JObject result = Get("/content/path/start/children", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.OK, status);
            Assert.IsTrue(result.HasValues);
            Assert.IsTrue(result.TryGetValue("Children", out _));
            Assert.IsNotNull(result["Children"]);

            Assert.IsTrue(ValidateChildren(result));
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("CHILDREN")]
        public void GetChildrenOneButLast()
        {
            JObject result = Get("/content/path/start/alloy-plan/download-alloy-plan/children", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.OK, status);
            Assert.IsTrue(result.HasValues);
            Assert.IsTrue(result.TryGetValue("Children", out _));
            Assert.IsNotNull(result["Children"]);

            Assert.IsTrue(ValidateChildren(result));
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("CHILDREN")]
        public void GetChildrenLast()
        {
            JObject result = Get("/content/path/start/alloy-plan/download-alloy-plan/start-downloading/children", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.OK, status);
            Assert.IsFalse(result.HasValues);
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("CHILDREN")]
        public void GetChildrenNonExistent()
        {
            JObject result = Get("/content/path/start/non-existent-page/children", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
            Assert.IsFalse(result.HasValues);
        }

        [TestMethod, TestCategory("GET"), TestCategory("CHILDREN")]
        public void GetChildrenStartPageNoPath()
        {
            JObject result = Get("/content/start/children", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.OK, status);
            Assert.IsTrue(result.HasValues);
            Assert.IsTrue(result.TryGetValue("Children", out _));
            Assert.IsNotNull(result["Children"]);

            Assert.IsTrue(ValidateChildren(result));
        }

        [TestMethod, TestCategory("GET"), TestCategory("CHILDREN")]
        public void GetChildrenExistingID()
        {
            // Same as "/content/path/start/alloy-track/children"
            JObject result = Get("/content/9/children", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.OK, status);
            Assert.IsTrue(result.HasValues);
            Assert.IsTrue(result.TryGetValue("Children", out _));
            Assert.IsNotNull(result["Children"]);

            Assert.IsTrue(ValidateChildren(result));
        }

        [TestMethod, TestCategory("GET"), TestCategory("CHILDREN")]
        public void GetChildrenNonExistingID()
        {
            JObject result = Get("/content/99999/children", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
            Assert.IsFalse(result.HasValues);
        }
        
        #endregion

        #region Main Operation
        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("MAIN")]
        public void GetMainFromStart()
        {
            JObject result = Get("/content/path/start/main/alloy-meet-jumbotron", out HttpStatusCode status);
            Assert.AreEqual("Alloy Meet jumbotron", result["Name"].ToString());
            Assert.AreEqual("50", result["ParentLink"].ToString());
            Assert.AreEqual("51", result["ContentLink"].ToString());
            Assert.AreEqual("Wherever you meet!", result["Heading"].ToString());
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("MAIN")]
        public void GetEmptyMain()
        {
            JObject result = Get("/content/path/start/main/", out HttpStatusCode status);
            // Is This expected behaviour?
            Assert.IsTrue(ValidatePage(result));
            Assert.AreEqual("Start", result["Name"].ToString());
            Assert.AreEqual("Start", result["PageName"].ToString());
            Assert.AreEqual("5", result["ContentLink"].ToString());
            Assert.AreEqual("start", result["PageURLSegment"].ToString());
            Assert.AreEqual(4, ((JArray)result["MainContentArea"]).Count);
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("MAIN")]
        public void GetNonexistingMain()
        {
            JObject result = Get("/content/path/start/main/non-existing-main", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("MAIN")]
        public void GetMainFromNonExistingPath()
        {
            JObject result = Get("/content/path/start/non-existing-path/main/some-content", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }
        #endregion

        #region Rel Operation
        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("RELATED")]
        public void GetRelated()
        {
            JObject result = Get("/content/path/start/alloy-plan/related/event-list", out HttpStatusCode status);
            Assert.AreEqual("Event list", result["Name"].ToString());
            Assert.AreEqual("62", result["ContentLink"].ToString());
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("RELATED")]
        public void GetEmptyRelated()
        {
            JObject result = Get("/content/path/start/related/", out HttpStatusCode status);
            // Is This expected behaviour?
            Assert.IsTrue(ValidatePage(result));
            Assert.AreEqual("Start", result["Name"].ToString());
            Assert.AreEqual("Start", result["PageName"].ToString());
            Assert.AreEqual("5", result["ContentLink"].ToString());
            Assert.AreEqual("start", result["PageURLSegment"].ToString());
            Assert.AreEqual(4, ((JArray)result["MainContentArea"]).Count);
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("RELATED")]
        public void GetNonexistingRel()
        {
            JObject result = Get("/content/path/start/related/non-existing-rel", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }

        [TestMethod, TestCategory("GET"), TestCategory("PATH"), TestCategory("RELATED")]
        public void GetRelFromNonExistingPath()
        {
            JObject result = Get("/content/path/start/non-existing-path/related/some-content", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }
        #endregion

        #region Create Operation
        [TestMethod, TestCategory("CREATE"), TestCategory("PATH")]
        public void CreatePage()
        {
            JObject result = Post(TestPagePath, JObject.Parse(
                "{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Simple Created Test Page\"}"), out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.Created, status);

            result = Get(TestPagePath + "/simple-created-test-page");
            Assert.IsTrue(ValidatePage(result));
            Assert.AreEqual("Simple Created Test Page", result["Name"].ToString());
            Assert.AreEqual("StandardPage", result["PageTypeName"].ToString());
        }

        [TestMethod, TestCategory("CREATE")]
        public void RecreatePage()
        {
            JObject result = Post(TestPagePath, JObject.Parse(
                "{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Existing Page\"}"), out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.Created, status);

            result = Post(TestPagePath, JObject.Parse(
                "{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Existing Page\"}"), out status);
            Assert.AreEqual(HttpStatusCode.BadRequest, status);
        }

        [TestMethod, TestCategory("CREATE")]
        public void CreateInvalidPage()
        {
            // test that tries to break the properties of a page while creating.
            JObject result = Post(TestPagePath, JObject.Parse(
                "{\"SaveAction\":\"Publish\", \"ContentType\":\"FAKEPAGE\", \"Name\":\"Simple Test Page\"}"), out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.BadRequest, status);
        }

        [TestMethod, TestCategory("CREATE"), TestCategory("PATH")]
        public void CreatePageInvalidPath()
        {
            // test that tries to create a file at a nonexisting path 
            JObject result = Post(TestPagePath + "/nonexisting-path", JObject.Parse(
                "{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Simple Test Page\"}"), out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }
        #endregion

        #region Delete Operation
        [TestMethod, TestCategory("DELETE")]
        public void DeletePage()
        {
            Post(TestPagePath, JObject.Parse("{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Simple Delete Test Page\"}"), out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.Created, status);

            Delete(TestPagePath + "/simple-delete-test-page", out status);
            Assert.AreEqual(HttpStatusCode.OK, status);

            Get(TestPagePath + "/simple-delete-test-page", out status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }

        [TestMethod, TestCategory("DELETE")]
        public void DeleteNonExistingPage()
        {
            Get(TestPagePath + "/non-existing-page", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);

            Delete(TestPagePath + "/non-existing-page", out status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }

        [TestMethod, TestCategory("DELETE")]
        public void DeleteRootOfTreeStructure()
        {
            Post(TestPagePath, JObject.Parse("{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Simple Test Page\"}"));
            Post(TestPagePath + "/simple-test-page", JObject.Parse("{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Test Page One\"}"));
            Post(TestPagePath + "/simple-test-page", JObject.Parse("{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Test Page Two\"}"));

            Get(TestPagePath + "/simple-test-page/test-page-one", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.OK, status);

            Delete(TestPagePath + "/simple-test-page", out status);
            Assert.AreEqual(HttpStatusCode.OK, status);

            Get(TestPagePath + "/simple-test-page/test-page-one", out status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }
        #endregion

        #region Update Operation
        [TestMethod, TestCategory("UPDATE")]
        public void UpdatePage()
        {
            Post(TestPagePath, JObject.Parse("{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Simple Test Page One\"}"));
            Get(TestPagePath + "/simple-test-page-one", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.OK, status);
            
            JObject result = Put(TestPagePath + "/simple-test-page-one", JObject.Parse("{\n\"SaveAction\":\"Publish\",\"MetaTitle\": \"Updated Title\"}"), out status);
            Assert.AreEqual(HttpStatusCode.OK, status);

            result = Get(TestPagePath + "/simple-test-page-one", out status);
            Assert.AreEqual(HttpStatusCode.OK, status);
            Assert.AreEqual("Updated Title", result["MetaTitle"].ToString());
        }

        [TestMethod, TestCategory("UPDATE")]
        public void UpdateNonExistingPage()
        {
            JObject result = Get("/content/path/start/non-existing-path", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);

            result = Put("/content/path/start/non-existing-path", JObject.Parse("{\n\"SaveAction\":\"Publish\",\"MetaTitle\": \"Updated Title\"}"), out status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }

        [TestMethod, TestCategory("UPDATE")]
        public void UpdateInvalidInfo()
        {
            Post(TestPagePath, JObject.Parse("{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Simple Test Page Two\"}"));
            Get(TestPagePath + "/simple-test-page-two", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.OK, status);

            JObject result = Put(TestPagePath + "/simple-test-page-two", JObject.Parse("{\n\"SaveAction\":\"Publish\",\"InvalidProperty\": \"Content\"}"), out status);
            Assert.AreEqual(HttpStatusCode.BadRequest, status);
        }
        #endregion

        #region BinaryContent
        [TestMethod, TestCategory("GET"), TestCategory("BINARY")]
        public void GetBinaryContent()
        {
            string result = GetBinary("/content/58/BinaryData", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.OK, status);
            Assert.AreEqual("PNG", result.Substring(1, 3));
        }
        
        [TestMethod, TestCategory("GET"), TestCategory("BINARY")]
        public void GetNonExistingBinaryContent()
        {
            GetBinary("/content/path/start/non-existing-path", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }

        [TestMethod, TestCategory("UPDATE"), TestCategory("PATH"), TestCategory("BINARY")]
        public void UpdateBinaryContentOfNonExistingPage()
        {
            JObject result = Get("/content/path/start/non-existing-path", out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);

            result = Put("/content/path/start/non-existing-path", JObject.Parse("{\n\"SaveAction\":\"Publish\",\"binarydata\": \"R0lGODlhAQABAIAAAP///////yH5BAEKAAEALAAAAAABAAEAAAICTAEAOw==\"}"), out status);
            Assert.AreEqual(HttpStatusCode.NotFound, status);
        }

        [TestMethod, TestCategory("UPDATE"), TestCategory("PATH"), TestCategory("BINARY")]
        public void UpdateBinaryContentOfNonMediaType()
        {
            Post(TestPagePath, JObject.Parse("{\"SaveAction\":\"Publish\", \"ContentType\":\"StandardPage\", \"Name\":\"Simple Test Page Binary\"}"), out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.Created, status);
            
            JObject result = Put(TestPagePath + "/simple-test-page-binary", JObject.Parse("{\n\"SaveAction\":\"Publish\",\"binarydata\": \"R0lGODlhAQABAIAAAP///////yH5BAEKAAEALAAAAAABAAEAAAICTAEAOw==\"}"), out status);
            Assert.AreEqual(HttpStatusCode.BadRequest, status);
        }

        [TestMethod, TestCategory("UPDATE"), TestCategory("PATH"), TestCategory("BINARY")]
        public void UpdateBinaryContent()
        {
            //TODO: change contentType to mediaType
            Post(TestPagePath, JObject.Parse("{\"SaveAction\":\"Publish\", \"ContentType\":\"ImageFile\", \"Name\":\"test-image.png\", \"BinaryData\": \"R0lGODlhAQABAIAAAP///////yH5BAEKAAEALAAAAAABAAEAAAICTAEAOw==\"}"), out HttpStatusCode status);
            Assert.AreEqual(HttpStatusCode.Created, status);
            
            JObject result = Put(TestPagePath + "/test-image.png", JObject.Parse("{\n\"SaveAction\":\"Publish\",\"BinaryData\": \""+ System.Convert.ToBase64String(Encoding.UTF8.GetBytes("test")) +"\"}"), out status);
            Assert.AreEqual(HttpStatusCode.OK, status);


            string content = GetBinary(TestPagePath + "/test-image.png/BinaryData", out status);
            //string content = GetBinary("/content/"+result["reference"].ToString().Substring(0, 4)+"/BinaryData", out status);
            Assert.AreEqual(HttpStatusCode.OK, status);
            Assert.AreEqual("test", content);
        }
        #endregion

        #region Validation Methods
        private bool ValidatePage(JObject page)
        {
            string[] fields = new string[]
            {
                "Name", "ParentLink", "ContentGuid", "ContentLink", "PageTypeName", "PageChanged", "PageName", "PageURLSegment",
                "PageCreated"
            };

            foreach (string field in fields)
                if (!page.TryGetValue(field, out _)) return false;
            return true;
        }

        private bool ValidateChildren(JObject data)
        {

            return true;
        }
        #endregion

        #region Helper Methods
        static JObject Post(string path, string body)
        {
            return Post(path, body, out _);
        }
        static JObject Post(string path, string body, out HttpStatusCode status)
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseURL + path);
            httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
            httpRequest.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");

            HttpClient client = new HttpClient();
            var response = client.SendAsync(httpRequest).Result;
            client.Dispose();

            status = response.StatusCode;
            if (response.StatusCode != HttpStatusCode.OK)
                return new JObject();

            string content = response.Content.ReadAsStringAsync().Result;
            return content == "" ? new JObject() : JObject.Parse(content);
        }

        static JObject Post(string path, JObject body)
        {
            return Post(path, body, out _);
        }
        static JObject Post(string path, JObject body, out HttpStatusCode status)
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseURL + path);
            httpRequest.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("bearer", bearer);
            httpRequest.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            HttpClient client = new HttpClient();
            var response = client.SendAsync(httpRequest).Result;
            client.Dispose();

            status = response.StatusCode;
            if (response.StatusCode != HttpStatusCode.OK)
                return new JObject();

            string content = response.Content.ReadAsStringAsync().Result;
            return content == "" ? new JObject() : JObject.Parse(content);
        }

        static JObject Get(string path)
        {
            return Get(path, out _);
        }
        static JObject Get(string path, out HttpStatusCode status)
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, BaseURL + path);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("bearer", bearer);
            httpRequest.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            HttpClient client = new HttpClient();
            var response = client.SendAsync(httpRequest).Result;
            client.Dispose();

            status = response.StatusCode;
            if (response.StatusCode != HttpStatusCode.OK)
                return new JObject();

            string content = response.Content.ReadAsStringAsync().Result;
            return content == "" ? new JObject() : JObject.Parse(content);
        }

        static T Get<T>(string path, Func<JObject, T> func)
        {
            return func(Get(path, out _));
        }
        static T Get<T>(string path, Func<JObject, HttpStatusCode, T> func)
        {
            JObject result = Get(path, out HttpStatusCode status);
            return func(result, status);
        }

        static string GetBinary(string path, out HttpStatusCode status)
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Get, BaseURL + path);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("bearer", bearer);
            httpRequest.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            HttpClient client = new HttpClient();
            var response = client.SendAsync(httpRequest).Result;
            client.Dispose();

            status = response.StatusCode;
            if (response.StatusCode != HttpStatusCode.OK)
                return "";

            string result = "";
            using (StreamReader sr = new StreamReader(response.Content.ReadAsStreamAsync().Result))
            {
                result = sr.ReadToEnd();
            }

            return result;
        }

        static JObject Delete(string path)
        {
            return Delete(path, out _);
        }
        static JObject Delete(string path, out HttpStatusCode status)
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Delete, BaseURL + path);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("bearer", bearer);
            httpRequest.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            HttpClient client = new HttpClient();
            var response = client.SendAsync(httpRequest).Result;
            client.Dispose();

            status = response.StatusCode;
            if (response.StatusCode != HttpStatusCode.OK)
                return new JObject();

            string content = response.Content.ReadAsStringAsync().Result;
            return content == "" ? new JObject() : JObject.Parse(content);
        }

        static JObject Put(string path, JObject body)
        {
            return Put(path, body, out _);
        }
        static JObject Put(string path, JObject body, out HttpStatusCode status)
        {
            HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Put, BaseURL + path);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("bearer", bearer);
            httpRequest.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            httpRequest.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");

            HttpClient client = new HttpClient();
            var response = client.SendAsync(httpRequest).Result;
            client.Dispose();

            status = response.StatusCode;
            if (response.StatusCode != HttpStatusCode.OK)
                return new JObject();

            string content = response.Content.ReadAsStringAsync().Result;
            return content == "" ? new JObject() : JObject.Parse(content);
        }
        #endregion
    }
}
