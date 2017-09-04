using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ServiceAPIExtensionsTest
{
    [TestClass]
    public class ServiceAPIExtensionsTest
    {
        HttpClient client;
        
        [TestInitialize]
        public void Init()
        {
            client = new HttpClient();
            client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com");
        }

        [TestMethod, TestCategory("GET")]
        public void GetStart()
        {
            Console.WriteLine(Get("/posts/1"));
        }

        private JObject Get(string path)
        {
            HttpResponseMessage message = client.GetAsync(path).Result;
            string content = message.Content.ReadAsStringAsync().Result;
            JObject result = JObject.Parse(content);
            return result;
        }

        private T Get<T>(string path, Func<JObject, T> func)
        {
            JObject result = JObject.Parse(client.GetAsync(path).Result.Content.ToString());
            return func(result);
        }
    }
}
