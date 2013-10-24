/*
Copyright (c) 2013 MetricWise, Inc

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using LastModifiedDictionary = System.Collections.Generic.Dictionary<string, System.DateTime>;

namespace Vtiger.Data
{
    public delegate void BeforeImportHandler(DataTable table, DataRow row);

    public class WebServiceDataAdapter : DataAdapter
    {
        public LastModifiedDictionary lastModifiedDict = new LastModifiedDictionary();
        public event BeforeImportHandler BeforeImport;
        private WebServiceClient webServiceClient = null;

        public WebServiceDataAdapter(WebServiceClient client)
        {
            webServiceClient = client;
        }

        public override int Fill(DataSet dataSet)
        {
            int filled = 0;
            foreach (DataTable table in dataSet.Tables)
            {
                DateTime lastModified;
                if (lastModifiedDict.ContainsKey(table.TableName))
                {
                    lastModified = lastModifiedDict[table.TableName];
                }
                else
                {
                    lastModified = DateTime.Now - TimeSpan.FromDays(90); // TODO: 90 days is arbitrary
                }
                int modifiedTime = ToUnixTime(lastModified);
                JToken token = webServiceClient.DoSync(modifiedTime.ToString(), table.TableName);
                if (token != null)
                {
                    DataTable dataTable = ToDataTable(token["updated"]);
                    DataTable mergeTable = dataSet.Tables[table.TableName].Clone();
                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (null != BeforeImport)
                        {
                            BeforeImport(mergeTable, row);
                        }
                        mergeTable.ImportRow(row);
                    }
                    dataSet.Merge(mergeTable);
                }
            }
            return filled;
        }

        public override int Update(DataSet dataSet)
        {
            int updated = 0;
            foreach (DataTable table in dataSet.Tables)
            {
                foreach (DataRow row in table.Rows)
                {
                    JToken token = ToToken(row);
                    switch (row.RowState)
                    {
                        case DataRowState.Modified:
                            webServiceClient.DoUpdate(token);
                            break;
                    }
                    row.AcceptChanges();
                }
            }
            return updated;
        }

        public DataTable ToDataTable(JToken token)
        {
            if (!(token is JArray))
            {
                JArray container = new JArray();
                container.Add(token);
                token = container;
            }
            JTokenReader reader = new JTokenReader(token);
            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new DataTableConverter());
            DataTable dataTable = serializer.Deserialize<DataTable>(reader);
            if (!dataTable.Columns.Contains("crmid"))
            {
                dataTable.Columns.Add("crmid", typeof(int));
                foreach (DataRow row in dataTable.Rows)
                {
                    row["crmid"] = ToCrmId(row["id"].ToString());
                }
                dataTable.PrimaryKey = new DataColumn[] { dataTable.Columns["crmid"] };
            }
            return dataTable;
        }

        private int ToCrmId(string wsId)
        {
            int crmId = int.Parse(Regex.Replace(wsId, ".*x", ""));
            return crmId;
        }

        private JToken ToToken(DataRow row)
        {
            JTokenWriter writer = new JTokenWriter();
            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new DataTableConverter());
            serializer.Serialize(writer, row);
            JToken token = writer.Token["Table"][0];
            return token;
        }

        private int ToUnixTime(DateTime dateTime)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime();
            TimeSpan span = dateTime - epoch;
            int unixtime = (int)Math.Round(span.TotalSeconds);
            return unixtime;
        }
    }
}
