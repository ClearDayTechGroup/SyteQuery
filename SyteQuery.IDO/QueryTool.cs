using Mongoose.IDO;
using Mongoose.IDO.DataAccess;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Text;


namespace QueryTool
{
    [IDOExtensionClass(nameof(QueryTool))]
    public class QueryTool : IDOExtensionClass
    {
        [IDOMethod(MethodFlags.None, "Infobar")]
        public int ExecuteQuery(string InputCommand, ref string Output, ref string Infobar)
        {
            var msgs = new StringBuilder();
            var returnValue = 0;
            var dt = new DataTable();
            try
            {
                using (ApplicationDB appDb = IDORuntime.Context.CreateAppDB())
                {
                    using (IDbCommand cmd = appDb.CreateCommand())
                    {
                        cmd.CommandText = InputCommand;
                        cmd.CommandType = CommandType.Text;

                        using (IDataReader drOut = appDb.ExecuteReader(cmd))
                        {
                            dt = ConvertDataReaderToDataTable(drOut);
                        }
                    }
                }

                string json = JsonConvert.SerializeObject(dt);
                Output = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

                Infobar = msgs.ToString();
                return returnValue;
            }
            catch (Exception ex)
            {
                Infobar = ex.Message;
                return 16;
            }
        }

        public DataTable ConvertDataReaderToDataTable(IDataReader reader)
        {
            DataTable dt = new DataTable();
            int intFieldCount = reader.FieldCount;
            for (int i = 0; i < reader.FieldCount; i++)
            {
                dt.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
            }
            while (reader.Read())
            {
                object[] values = new object[reader.FieldCount];
                reader.GetValues(values);
                dt.LoadDataRow(values, true);
            }
            return dt;
        }
    }
}
