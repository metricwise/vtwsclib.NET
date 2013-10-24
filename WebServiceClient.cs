/*
Copyright (c) 2012 MetricWise, Inc

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vtiger
{
    public delegate void WebServiceExceptionHandler(Exception exception);

    public class WebServiceClient
    {
        public event WebServiceExceptionHandler OnException;

        private string serverURL = "";
        private string sessionName = "";

        public void SetServerUrl(string url)
        {
            serverURL = url;
        }

        public JToken DoLogin(string userName, string accessKey)
        {
            string token = GetChallengeToken(userName);
            string accessData = token + accessKey;
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["operation"] = "login";
            dictionary["username"] = userName;
            dictionary["accessKey"] = MD5Hex(accessData);
            JToken data = PostRequest(dictionary);
            if (data != null)
            {
                sessionName = data["sessionName"].Value<string>();
            }
            return data;
        }

        public JToken DoLoginPassword(string userName, string password)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["operation"] = "login.password";
            dictionary["user_name"] = userName;
            dictionary["user_password"] = password;
            JToken data = PostRequest(dictionary);
            if (data != null)
            {
                sessionName = data["sessionName"].Value<string>();
            }
            return data;
        }

        public void DoLogout()
        {
            if (sessionName.Length > 0)
            {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();
                dictionary["operation"] = "logout";
                dictionary["sessionName"] = sessionName;
                JToken data = GetRequest(dictionary);
                if (data != null)
                {
                    sessionName = "";
                }
            }
        }

        public JToken DoDescribe(string elementType) {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["elementType"] = elementType;
            dictionary["operation"] = "describe";
            dictionary["sessionName"] = sessionName;
            return GetRequest(dictionary);
        }

        public JToken DoQuery(string query)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["operation"] = "query";
            dictionary["query"] = query;
            dictionary["sessionName"] = sessionName;
            return GetRequest(dictionary);
        }

        public JToken DoSync(string modifiedTime, string elementType)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["operation"] = "sync";
            dictionary["sessionName"] = sessionName;
            dictionary["modifiedTime"] = modifiedTime;
            dictionary["elementType"] = elementType;
            return GetRequest(dictionary);
        }

        public void AsyncUpdate(JToken element)
        {
            AsyncUpdate(element, null);
        }

        public void AsyncUpdate(JToken element, WebServiceDataHandler callback)
        {
            Dictionary<string, string> dictionary = GetUpdateDictionary(element);
            WebServiceAsyncRequest request = new WebServiceAsyncRequest();
            byte[] postData = ToPostData(dictionary);
            request.OnException += OnException;
            if (null != callback)
            {
                request.OnData += callback;
            }
            request.Begin(serverURL, postData);
        }

        public JToken DoUpdate(JToken element)
        {
            Dictionary<string, string> dictionary = GetUpdateDictionary(element);
            return PostRequest(dictionary);
        }

        private Dictionary<string, string> GetUpdateDictionary(JToken element)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["operation"] = "update";
            dictionary["sessionName"] = sessionName;
            dictionary["element"] = element.ToString();
            return dictionary;
        }

        private string GetChallengeToken(string username)
        {
            string token = "";
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            dictionary["operation"] = "getchallenge";
            dictionary["username"] = username;
            JToken data = GetRequest(dictionary);
            if (data != null)
            {
                token = data["token"].Value<String>();
            }
            return token;
        }

        private JToken GetRequest(Dictionary<string, string> dictionary)
        {
            JToken data = null;
            String url = serverURL + "?" + ToGetString(dictionary);
            try
            {
                Action action = delegate()
                {
                    WebRequest request = System.Net.WebRequest.Create(url);
                    request.Proxy = System.Net.GlobalProxySelection.GetEmptyWebProxy();
                    request.Method = "GET";
                    data = ParseResponse(request);
                };
                WebServiceRetry.ExponentialBackoff(action, TimeSpan.FromMilliseconds(512), 3);
            }
            catch (Exception e)
            {
                OnException(e);
            }
            return data;
        }

        private JToken PostRequest(Dictionary<string, string> dictionary)
        {
            JToken data = null;
            try
            {
                Action action = delegate()
                {
                    WebRequest request = System.Net.WebRequest.Create(serverURL);
                    request.Proxy = System.Net.GlobalProxySelection.GetEmptyWebProxy();
                    byte[] postData = ToPostData(dictionary);
                    request.ContentLength = postData.Length;
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.Method = "POST";
                    Stream stream = request.GetRequestStream();
                    stream.Write(postData, 0, postData.Length);
                    stream.Close();
                    data = ParseResponse(request);
                };
                WebServiceRetry.ExponentialBackoff(action, TimeSpan.FromMilliseconds(512), 3);
            }
            catch (Exception exception)
            {
                OnException(exception);
            }
            return data;
        }

        private byte[] ToPostData(Dictionary<string, string> dictionary)
        {
            string getString = ToGetString(dictionary);
            return Encoding.UTF8.GetBytes(getString);
        }

        private string ToGetString(Dictionary<string, string> dictionary)
        {
            StringBuilder query = new StringBuilder();
            foreach (string key in dictionary.Keys)
            {
                string value = dictionary[key];
                query.Append(string.Format("{0}={1}", System.Uri.EscapeDataString(key), System.Uri.EscapeDataString(value)));
                query.Append("&");
            }
            return query.ToString();
        }

        private JToken ParseResponse(WebRequest request)
        {
            WebResponse response = request.GetResponse();
            Stream stream = response.GetResponseStream();
            StreamReader reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            reader.Close();
            response.Close();
            return ParseJson(json);
        }

        private JToken ParseJson(string json) {
            JObject data = JObject.Parse(json);
            if ("true" != data["success"].ToString())
            {
                throw new WebServiceException(data["error"]);
            }
            return (JToken)data["result"];
        }

        private string MD5Hex(string value)
        {
            MD5 crypto = MD5.Create();
            byte[] hash = crypto.ComputeHash(Encoding.UTF8.GetBytes(value));
            StringBuilder hex = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                hex.Append(hash[i].ToString("x2"));
            }
            return hex.ToString();
        }
    }
}
