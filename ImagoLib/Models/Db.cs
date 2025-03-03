using System.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ImagoLib.Models {
    public static class Db {
        public static IDbConnection Get() {
            var conString = new SqlConnectionStringBuilder();
#if DEBUG
            conString.DataSource = "diacom.technology";
            conString.UserID = "imagodt";
            conString.Password = "C9uUR28bRV";
            conString.InitialCatalog = "imagodt";
            conString.TrustServerCertificate = true;
#else
            conString.DataSource = "diacom.technology";
            conString.UserID = "healthycom";
            conString.Password = "5a~oAG@MiD@w";
            conString.InitialCatalog = "healthycom";
            conString.TrustServerCertificate = true;
#endif
            var db = new SqlConnection(conString.ConnectionString);
            db.Open();
            return db;
        }

        public static string SQL(string sql) {
            return sql;
        }

        public static void SetParam(IDbCommand cmd, string name, object value) {
            var sqlCmd = (SqlCommand)cmd;
            if (sqlCmd.Parameters.Contains(name)) {
                sqlCmd.Parameters[name].Value = value;
            }
            else {
                sqlCmd.Parameters.AddWithValue(name, value);
            }
        }
    }
}
