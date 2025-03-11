using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;

namespace ImagoLib.Models {
    public partial class NovinyFoto : ObservableObject {
        [ObservableProperty] public int m_Id;
        [ObservableProperty] public int m_NovinyId;
        [ObservableProperty] public string m_PhotoName;
        [ObservableProperty] public byte[] m_PhotoData;

        private static NovinyFoto FromDataReader(IDataReader dr) {
            return new NovinyFoto() {
                Id = dr.GetInt32(0),
                NovinyId = dr.GetInt32(1),
                PhotoName = dr.GetString(2),
                PhotoData = (byte[])dr["PhotoData"]
            };
        }

        public static ObservableCollection<NovinyFoto> GetPhotosForRequest(int NovinyId) {
            var photos = new List<NovinyFoto>();

            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, NovinyId, PhotoName, PhotoData FROM NovinyPhotos WHERE NovinyId = @NovinyId";
                Db.SetParam(cmd, "@NovinyId", NovinyId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        photos.Add(FromDataReader(dr));
                    }
                }
            }

            return new ObservableCollection<NovinyFoto>(photos);
        }

       
        public static void InsertPhoto(NovinyFoto photo, int NovinyId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "INSERT INTO NovinyPhotos (NovinyId, PhotoName, PhotoData) VALUES (@NovinyId, @PhotoName, @PhotoData)";
                Db.SetParam(cmd, "@NovinyId", NovinyId);
                Db.SetParam(cmd, "@PhotoName", photo.PhotoName);
                Db.SetParam(cmd, "@PhotoData", photo.PhotoData);

                cmd.ExecuteNonQuery();
            }
        }

        public static void DeletePhoto(int photoId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM NovinyPhotos WHERE Id = @PhotoId";
                Db.SetParam(cmd, "@PhotoId", photoId);

                cmd.ExecuteNonQuery();
            }
        }

        public static void UpdatePhoto(NovinyFoto photo) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "UPDATE NovinyPhotos SET PhotoName = @PhotoName, PhotoData = @PhotoData WHERE Id = @PhotoId";
                Db.SetParam(cmd, "@PhotoId", photo.Id);
                Db.SetParam(cmd, "@PhotoName", photo.PhotoName);
                Db.SetParam(cmd, "@PhotoData", photo.PhotoData);

                cmd.ExecuteNonQuery();
            }
        }

        public static void DeletePhotosForRequest(int NovinyId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM NovinyPhotos WHERE NovinyId = @NovinyId";
                Db.SetParam(cmd, "@NovinyId", NovinyId);

                cmd.ExecuteNonQuery();
            }
        }
    }
}
