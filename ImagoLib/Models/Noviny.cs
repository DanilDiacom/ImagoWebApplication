using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImagoLib.Models {
    public partial class Noviny : ObservableObject {
        [ObservableProperty] public int m_Id;
        [ObservableProperty] public DateTime m_PostedDate;
        [ObservableProperty] public string m_Title;
        [ObservableProperty] public string m_Comment;
        [ObservableProperty] public string m_Description;
        [ObservableProperty] public byte[] m_IconPhoto;

        public ObservableCollection<NovinyPhoto> Photos { get; set; } = new ObservableCollection<NovinyPhoto>();
        public ObservableCollection<NovinyParameter> Parameters { get; set; } = new ObservableCollection<NovinyParameter>();

        private static Noviny FromDataReader(IDataReader dr) {
            return new Noviny() {
                Id = dr.GetInt32(0),
                PostedDate = dr.GetDateTime(1),
                Title = dr.GetString(2),
                Comment = dr.GetString(3),
                Description = dr.GetString(4),
                IconPhoto = (byte[])dr["IconPhoto"]
            };
        }

        public static ObservableCollection<Noviny> GetNoviny() {
            var allNoviny = new List<Noviny>();

            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, PostedDate, Title, Comment, Description, IconPhoto FROM Noviny";
                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        allNoviny.Add(FromDataReader(dr));
                    }
                }
            }

            return new ObservableCollection<Noviny>(allNoviny);
        }

        public static int InsertNoviny(Noviny noviny) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "INSERT INTO Noviny (PostedDate, Title, Comment, Description, IconPhoto) VALUES (@PostedDate, @Title, @Comment, @Description, @IconPhoto); SELECT SCOPE_IDENTITY();";

                Db.SetParam(cmd, "@PostedDate", noviny.PostedDate);
                Db.SetParam(cmd, "@Title", noviny.Title);
                Db.SetParam(cmd, "@Comment", noviny.Comment);
                Db.SetParam(cmd, "@Description", noviny.Description);
                Db.SetParam(cmd, "@IconPhoto", noviny.IconPhoto);

                var result = cmd.ExecuteScalar();
                return Convert.ToInt32(result);
            }
        }

        public static void UpdateNoviny(Noviny noviny, int id) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "UPDATE Noviny SET PostedDate = @PostedDate, Title = @Title, Comment = @Comment, Description = @Description, IconPhoto = @IconPhoto WHERE Id = @Id";

                Db.SetParam(cmd, "@Id", id);
                Db.SetParam(cmd, "@PostedDate", noviny.PostedDate);
                Db.SetParam(cmd, "@Title", noviny.Title);
                Db.SetParam(cmd, "@Comment", noviny.Comment);
                Db.SetParam(cmd, "@Description", noviny.Description);
                Db.SetParam(cmd, "@IconPhoto", noviny.IconPhoto);

                cmd.ExecuteNonQuery();
            }
        }

        public static void DeleteNoviny(int id) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM NovinyPhotos WHERE NovinyId = @Id; DELETE FROM NovinyParameters WHERE NovinyId = @Id; DELETE FROM Noviny WHERE Id = @Id;";
                Db.SetParam(cmd, "@Id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public partial class NovinyPhoto : ObservableObject {
        [ObservableProperty] public int m_Id;
        [ObservableProperty] public int m_NovinyId;
        [ObservableProperty] public string m_PhotoName;
        [ObservableProperty] public byte[] m_PhotoData;
    }



    public partial class NovinyParameter : ObservableObject {
        [ObservableProperty] public int m_Id;
        [ObservableProperty] public int m_NovinyId;
        [ObservableProperty] public string m_ParameterName;
        [ObservableProperty] public string m_ParameterValue;

        private static NovinyParameter FromDataReader(IDataReader dr) {
            return new NovinyParameter() {
                Id = dr.GetInt32(0),
                NovinyId = dr.GetInt32(1),
                ParameterName = dr.GetString(2),
                ParameterValue = dr.GetString(3)
            };
        }

        public static ObservableCollection<NovinyParameter> GetParametersForNoviny(int novinyId) {
            var parameters = new List<NovinyParameter>();
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, NovinyId, ParameterName, ParameterValue FROM NovinyParameters WHERE NovinyId = @NovinyId";
                Db.SetParam(cmd, "@NovinyId", novinyId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        parameters.Add(FromDataReader(dr));
                    }
                }
            }
            return new ObservableCollection<NovinyParameter>(parameters);
        }

        public static void InsertParameter(NovinyParameter parameter) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "INSERT INTO NovinyParameters (NovinyId, ParameterName, ParameterValue) VALUES (@NovinyId, @ParameterName, @ParameterValue)";
                Db.SetParam(cmd, "@NovinyId", parameter.NovinyId);
                Db.SetParam(cmd, "@ParameterName", parameter.ParameterName);
                Db.SetParam(cmd, "@ParameterValue", parameter.ParameterValue);
                cmd.ExecuteNonQuery();
            }
        }

        public static void UpdateParameter(NovinyParameter parameter) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "UPDATE NovinyParameters SET ParameterName = @ParameterName, ParameterValue = @ParameterValue WHERE Id = @Id";
                Db.SetParam(cmd, "@Id", parameter.Id);
                Db.SetParam(cmd, "@ParameterName", parameter.ParameterName);
                Db.SetParam(cmd, "@ParameterValue", parameter.ParameterValue);
                cmd.ExecuteNonQuery();
            }
        }

        public static void DeleteParameter(int parameterId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM NovinyParameters WHERE Id = @ParameterId";
                Db.SetParam(cmd, "@ParameterId", parameterId);
                cmd.ExecuteNonQuery();
            }
        }
        public static void DeleteParametersForNoviny(int novinyId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM NovinyParameters WHERE NovinyId = @NovinyId";
                Db.SetParam(cmd, "@NovinyId", novinyId);
                cmd.ExecuteNonQuery();
            }
        }

    }
}