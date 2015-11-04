using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Web;

namespace ServiceAPIExtensions.Business
{
    public class ConversionHelpers
    {
        public static dynamic ConvertObjectToExpando(object o)
        {
            var dic = new ExpandoObject() as IDictionary<string, object>;
            var t = o.GetType();
            var props=t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.GetProperty);
            foreach (var p in props)
            {
                dic.Add(p.Name, p.GetValue(o));
            }
            return (dynamic)dic;
        }
    }
}