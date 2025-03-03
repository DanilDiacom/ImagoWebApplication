using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagoLib.Models {
    public partial class MeetingPhoto : ObservableObject {
        [ObservableProperty] public int m_Id;
        [ObservableProperty] public int m_MeetingId;
        [ObservableProperty] public string m_PhotoName;
        [ObservableProperty] public byte[] m_PhotoData;

        private static MeetingPhoto FromDataReader(IDataReader dr) {
            return new MeetingPhoto() {
                Id = dr.GetInt32(0),
                MeetingId = dr.GetInt32(1),
                PhotoName = dr.GetString(2),
                PhotoData = (byte[])dr["PhotoData"]
            };
        }

        public static ObservableCollection<MeetingPhoto> GetPhotosForMeeting(int meetingId) {
            var photos = new List<MeetingPhoto>();

            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, MeetingId, PhotoName, PhotoData FROM MeetingPhotos WHERE MeetingId = @MeetingId";
                Db.SetParam(cmd, "@MeetingId", meetingId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        photos.Add(FromDataReader(dr));
                    }
                }
            }

            return new ObservableCollection<MeetingPhoto>(photos);
        }

        public static void InsertPhoto(MeetingPhoto photo, int meetingId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "INSERT INTO MeetingPhotos (MeetingId, PhotoName, PhotoData) VALUES (@MeetingId, @PhotoName, @PhotoData)";
                Db.SetParam(cmd, "@MeetingId", meetingId);
                Db.SetParam(cmd, "@PhotoName", photo.PhotoName);
                Db.SetParam(cmd, "@PhotoData", photo.PhotoData);

                cmd.ExecuteNonQuery();
            }
        }

        public static void DeletePhoto(int photoId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM MeetingPhotos WHERE Id = @PhotoId";
                Db.SetParam(cmd, "@PhotoId", photoId);

                cmd.ExecuteNonQuery();
            }
        }

        public static void UpdatePhoto(MeetingPhoto photo) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "UPDATE MeetingPhotos SET PhotoName = @PhotoName, PhotoData = @PhotoData WHERE MeetingId = @PhotoId";
                Db.SetParam(cmd, "@PhotoId", photo.MeetingId);
                Db.SetParam(cmd, "@PhotoName", photo.PhotoName);
                Db.SetParam(cmd, "@PhotoData", photo.PhotoData);

                cmd.ExecuteNonQuery();
            }
        }

        public static void DeletePhotosForMeeting(int meetingId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM MeetingPhotos WHERE MeetingId = @MeetingId";
                Db.SetParam(cmd, "@MeetingId", meetingId);

                cmd.ExecuteNonQuery();
            }
        }

    }
}
