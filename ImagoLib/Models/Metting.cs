using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImagoLib.Models {
    public partial class Meeting : ObservableObject {
        [ObservableProperty] public int m_Id;
        [ObservableProperty] public string m_Title;
        [ObservableProperty] public string m_Location;
        [ObservableProperty] public string m_Description;
        [ObservableProperty] public string m_Feedback;
        [ObservableProperty] public DateTime m_CreatedAt;
        [ObservableProperty] public DateTime? m_UpdatedAt;

        public ObservableCollection<MeetingPhoto> Photos { get; set; } = new ObservableCollection<MeetingPhoto>();

        private static Meeting FromDataReader(IDataReader dr) {
            return new Meeting() {
                Id = dr.GetInt32(0),
                Title = dr.GetString(1),
                Location = dr.GetString(2),
                Description = dr.GetString(3),
                Feedback = dr.GetString(4),
                CreatedAt = dr.GetDateTime(5),
                UpdatedAt = dr.IsDBNull(6) ? (DateTime?)null : dr.GetDateTime(6),
            };
        }

        public static ObservableCollection<Meeting> GetMeetings() {
            var allMeetings = new List<Meeting>();

            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, Title, Location, Description, Feedback, CreatedAt, UpdatedAt FROM Meetings";
                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        allMeetings.Add(FromDataReader(dr));
                    }
                }
            }

            return new ObservableCollection<Meeting>(allMeetings);
        }

        public static Meeting GetMeetingById(int meetingId) {
            Meeting meeting = null;

            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, Title, Location, Description, Feedback, CreatedAt, UpdatedAt FROM Meetings WHERE MeetingId = @MeetingId";
                Db.SetParam(cmd,"@MeetingId", meetingId);

                using (var dr = cmd.ExecuteReader()) {
                    if (dr.Read()) {
                        meeting = FromDataReader(dr);
                    }
                }
            }

            if (meeting != null) {
                meeting.Photos = GetPhotosForMeeting(meeting.Id);
            }

            return meeting;
        }

        private static ObservableCollection<MeetingPhoto> GetPhotosForMeeting(int meetingId) {
            var photos = new List<MeetingPhoto>();

            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT Id, MeetingId, PhotoName, PhotoData FROM MeetingPhotos WHERE MeetingId = @MeetingId";
                Db.SetParam(cmd,"@MeetingId", meetingId);

                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        photos.Add(new MeetingPhoto {
                            Id = dr.GetInt32(0),
                            MeetingId = dr.GetInt32(1),
                            PhotoName = dr.GetString(2),
                            PhotoData = (byte[])dr["PhotoData"]
                        });
                    }
                }
            }

            return new ObservableCollection<MeetingPhoto>(photos);
        }

        public static int InsertMeeting(Meeting meeting) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "INSERT INTO Meetings (Title, Location, Description, Feedback, CreatedAt) " +"VALUES (@Title, @Location, @Description, @Feedback, @CreatedAt); " +
                                  "SELECT SCOPE_IDENTITY();"; 

                Db.SetParam(cmd, "@Title", meeting.Title);
                Db.SetParam(cmd, "@Location", meeting.Location);
                Db.SetParam(cmd, "@Description", meeting.Description);
                Db.SetParam(cmd, "@Feedback", meeting.Feedback);
                Db.SetParam(cmd, "@CreatedAt", meeting.CreatedAt);

                var result = cmd.ExecuteScalar();
                int meetingId = Convert.ToInt32(result);

                return meetingId;
            }
        }


        public static void UpdateMeeting(Meeting meeting, int id) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "UPDATE Meetings SET Title = @Title, Location = @Location, Description = @Description, Feedback = @Feedback, UpdatedAt = GETDATE() WHERE Id = @MeetingId";
                 Db.SetParam(cmd,"@MeetingId", id);
                 Db.SetParam(cmd,"@Title", meeting.Title);
                 Db.SetParam(cmd,"@Location", meeting.Location);
                 Db.SetParam(cmd,"@Description", meeting.Description);
                 Db.SetParam(cmd,"@Feedback", meeting.Feedback);

                 cmd.ExecuteNonQuery();
            }
        }

        public static void DeleteMeeting(int meetingId) {
            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "DELETE FROM MeetingPhotos WHERE MeetingId = @MeetingId";
                 Db.SetParam(cmd,"@MeetingId", meetingId);
                cmd.ExecuteNonQuery();

                cmd.CommandText = "DELETE FROM Meetings WHERE Id = @MeetingId";
                cmd.ExecuteNonQuery();
            }
        }
    }
}
