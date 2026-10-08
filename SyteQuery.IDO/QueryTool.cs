using Mongoose.IDO;
using Mongoose.IDO.DataAccess;
using System;
using System.Data;
using System.Text;


namespace QueryTool
{
    [IDOExtensionClass(nameof(QueryTool))]
    public class QueryTool : IDOExtensionClass
    {
        /// <summary>
        /// Runs <paramref name="InputCommand"/> and returns every result set it produces as base64-encoded JSON
        /// (see <see cref="ResultSetJson"/>). On failure the SQL error goes in <paramref name="Infobar"/> and the
        /// return value is 16; if a later statement failed, <paramref name="Output"/> still holds the result sets
        /// read before it.
        /// </summary>
        [IDOMethod(MethodFlags.None, "Infobar")]
        public int ExecuteQuery(string InputCommand, ref string Output, ref string Infobar)
        {
            try
            {
                string json;
                string error;
                using (ApplicationDB appDb = IDORuntime.Context.CreateAppDB())
                {
                    using (IDbCommand cmd = appDb.CreateCommand())
                    {
                        cmd.CommandText = InputCommand;
                        cmd.CommandType = CommandType.Text;

                        using (IDataReader drOut = appDb.ExecuteReader(cmd))
                        {
                            json = ResultSetJson.Serialize(drOut, out error);
                        }
                    }
                }

                // Output carries whatever was read, even when a later statement failed.
                Output = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
                if (error != null)
                {
                    Infobar = error;
                    return 16;
                }

                Infobar = string.Empty;
                return 0;
            }
            catch (Exception ex)
            {
                Infobar = ex.Message;
                return 16;
            }
        }
    }
}
