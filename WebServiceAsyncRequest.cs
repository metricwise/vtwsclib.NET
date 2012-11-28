﻿/*
Copyright (c) 2012 MetricWise, Inc

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Vtiger
{
    public delegate void WebServiceDataHandler(JToken result);

    class WebServiceAsyncRequest
    {
        public event WebServiceDataHandler OnData;
        public event WebServiceExceptionHandler OnException;

        private byte[] postData;
        private WebRequest request;
        private string requestURI;

        public void Begin(string requestURI)
        {
            Begin(requestURI, null);
        }

        public void Begin(string requestURI, byte[] postData)
        {
            this.requestURI = requestURI;
            this.postData = postData;
            try
            {
                Thread thread = new Thread(new ThreadStart(Start));
                thread.Start();
            }
            catch (Exception e)
            {
                OnException(e);
            }
        }

        private void Start()
        {
            try
            {
                request = System.Net.WebRequest.Create(requestURI);
                if (null != postData)
                {
                    request.ContentLength = postData.Length;
                    request.ContentType = "application/x-www-form-urlencoded";
                    request.Method = "POST";
                    request.BeginGetRequestStream(new AsyncCallback(GetRequestCallback), request);
                }
                else
                {
                    request.Method = "GET";
                    request.BeginGetResponse(new AsyncCallback(GetResponseCallback), request);
                }
            }
            catch (Exception e)
            {
                OnException(e);
            }
        }

        private void GetRequestCallback(IAsyncResult result)
        {
            try
            {
                WebRequest request = (WebRequest)result.AsyncState;
                Stream stream = request.EndGetRequestStream(result);
                stream.Write(postData, 0, postData.Length);
                stream.Close();
                request.BeginGetResponse(new AsyncCallback(GetResponseCallback), request);
            }
            catch (Exception e)
            {
                OnException(e);
            }
        }

        private void GetResponseCallback(IAsyncResult result)
        {
            try
            {
                WebRequest request = (WebRequest)result.AsyncState;
                WebResponse response = request.EndGetResponse(result);
                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream);
                string json = reader.ReadToEnd();
                reader.Close();
                response.Close();
                JToken data = ParseJson(json);
                OnData(data);
            }
            catch (Exception e)
            {
                OnException(e);
            }
        }

        private JToken ParseJson(string json)
        {
            JObject data = JObject.Parse(json);
            if ("true" != data["success"].ToString())
            {
                throw new WebServiceException(data["error"]);
            }
            return (JToken)data["result"];
        }
    }
}
