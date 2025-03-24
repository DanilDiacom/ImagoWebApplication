using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using Microsoft.Data.SqlClient;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImagoLib.Models {
    public partial class Pages : ObservableObject {
        [ObservableProperty] public int m_Id;
        [ObservableProperty] public string m_Title;
        [ObservableProperty] public string m_Url;
        [ObservableProperty] public int? m_ParentId;

        public ObservableCollection<Pages> SubPages { get; set; } = new ObservableCollection<Pages>();

        private static Pages FromDataReader(IDataReader dr) {
            return new Pages() {
                Id = dr.GetInt32(0),
                Title = dr.GetString(1),
                Url = dr.GetString(2),
                ParentId = dr.IsDBNull(3) ? null : dr.GetInt32(3),
            };
        }

        public static ObservableCollection<Pages> GetPagesHierarchy() {
            var allPages = new List<Pages>();

            using (var db = Db.Get()) {
                var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT id, title, url, parentId FROM Pages";
                using (var dr = cmd.ExecuteReader()) {
                    while (dr.Read()) {
                        allPages.Add(FromDataReader(dr));
                    }
                }
            }

            // Построение иерархии
            var pageDictionary = allPages.ToDictionary(p => p.Id);
            foreach (var page in allPages) {
                if (page.ParentId.HasValue && pageDictionary.ContainsKey(page.ParentId.Value)) {
                    pageDictionary[page.ParentId.Value].SubPages.Add(page);
                }
            }

            return new ObservableCollection<Pages>(pageDictionary.Values.Where(p => !p.ParentId.HasValue));
        }
    }
}