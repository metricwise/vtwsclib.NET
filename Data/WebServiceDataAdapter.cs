using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Vtiger.Data
{
    public class WebServiceDataAdapter : DataAdapter
    {
        private WebServiceClient webServiceClient = null;

        public WebServiceDataAdapter(WebServiceClient client)
        {
            webServiceClient = client;
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
                            webServiceClient.AsyncUpdate(token, new WebServiceDataHandler(OnUpdate));
                            break;
                    }
                }
            }
            return updated;
        }

        private void OnUpdate(JToken token)
        {
        }

        private JToken ToToken(DataRow value)
        {
            JTokenWriter writer = new JTokenWriter();
            JsonSerializer serializer = new JsonSerializer();
            serializer.Converters.Add(new DataTableConverter());
            serializer.Serialize(writer, value);
            JToken token = writer.Token["Table"][0];
            return token;
        }
    }
}
