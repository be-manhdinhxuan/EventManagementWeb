using MySql.Data.MySqlClient;
using System;
using System.Configuration;
using System.Data;
using System.Web.Security;
using System.Web.UI;

namespace EventManagementWeb.Admin
{
    public partial class EventManagement : Page
    {
        public DataTable EventList;
        public int CurrentPage = 1;
        public int TotalPages = 1;
        public const int PageSize = 10;

        public string SearchTerm = "";
        public string SelectedStatus = "ALL";

        public DataTable HeaderNotificationList;
        public int UnreadCount = 0;
        public string FullName = "";
        public string UserAvatar = "../Assets/images/avatar.jpg";

        protected void Page_Load(object sender, EventArgs e)
        {
            // Kiểm tra quyền Admin
            if (Session["UserId"] == null || Session["Role"]?.ToString() != "Admin")
            {
                Response.Redirect("~/Account/Login.aspx");
                return;
            }

            int adminId = Convert.ToInt32(Session["UserId"]);

            if (Request.HttpMethod == "POST" && Request.Form["btnAction"] == "logout")
            {
                HandleLogout();
                return;
            }

            // Thu thập filter từ POST
            if (Request.HttpMethod == "POST")
            {
                SearchTerm = Request.Form["txtSearch"]?.Trim() ?? "";
                SelectedStatus = Request.Form["ddlStatus"] ?? "ALL";

                // Reset filter
                if (Request.Form["btnAction"] == "reset")
                {
                    SearchTerm = "";
                    SelectedStatus = "ALL";
                }

                // Phân trang
                if (!string.IsNullOrEmpty(Request.Form["pageAction"]))
                {
                    int.TryParse(Request.Form["pageAction"], out CurrentPage);
                }

                // Xử lý xóa sự kiện
                string action = Request.Form["btnAction"];
                if (!string.IsNullOrEmpty(action) && action.StartsWith("delete_"))
                {
                    int eventId = Convert.ToInt32(action.Substring("delete_".Length));
                    DeleteEvent(eventId);
                    // Reload danh sách sau xóa
                    LoadEvents();
                }
            }

            if (!IsPostBack)
            {
                CurrentPage = 1;
            }

            LoadHeaderNotifications(adminId);
            LoadAdminInfo(adminId);
            LoadEvents();
        }

        private void LoadEvents()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            string filter = BuildFilterCondition();
            string sql = $@"
                SELECT SQL_CALC_FOUND_ROWS
                    Id, Title, StartTime, Location, ImageUrl, Status, CurrentRegistrations, MaxCapacity
                FROM Events
                WHERE IsDeleted = 0
                  {filter}
                ORDER BY CreatedAt DESC
                LIMIT @limit OFFSET @offset;

                SELECT FOUND_ROWS() AS TotalRecords;";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    if (!string.IsNullOrEmpty(SearchTerm))
                        cmd.Parameters.AddWithValue("@search", "%" + SearchTerm + "%");

                    cmd.Parameters.AddWithValue("@limit", PageSize);
                    cmd.Parameters.AddWithValue("@offset", (CurrentPage - 1) * PageSize);

                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        DataSet ds = new DataSet();
                        adapter.Fill(ds);

                        EventList = ds.Tables[0];

                        int totalRecords = ds.Tables[1].Rows.Count > 0
                            ? Convert.ToInt32(ds.Tables[1].Rows[0]["TotalRecords"])
                            : 0;

                        TotalPages = totalRecords > 0
                            ? (int)Math.Ceiling((double)totalRecords / PageSize)
                            : 1;
                    }
                }
            }
        }

        private string BuildFilterCondition()
        {
            var conditions = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(SearchTerm))
                conditions.Add("Title LIKE @search");

            if (SelectedStatus != "ALL")
            {
                if (SelectedStatus == "PUBLISHED")
                    conditions.Add("Status = 'Published'");
                else if (SelectedStatus == "DRAFT")
                    conditions.Add("Status = 'Draft'");
                else if (SelectedStatus == "PAST")
                    conditions.Add("EndTime < NOW()");
            }

            return conditions.Count > 0 ? "AND " + string.Join(" AND ", conditions) : "";
        }

        private void LoadHeaderNotifications(int adminId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
                SELECT Id, Type, Title, Message, IsRead, CreatedAt, RelatedEventId
                FROM Notifications
                WHERE UserId = @userId OR Type = 'System'
                ORDER BY CreatedAt DESC
                LIMIT 5";

            string countSql = "SELECT COUNT(*) FROM Notifications WHERE (UserId = @userId OR Type = 'System') AND IsRead = 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();

                using (MySqlCommand cmd = new MySqlCommand(countSql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", adminId);
                    UnreadCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", adminId);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        HeaderNotificationList = new DataTable();
                        adapter.Fill(HeaderNotificationList);
                    }
                }
            }
        }

        private void LoadAdminInfo(int adminId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "SELECT FullName, Avatar FROM Users WHERE Id = @id";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", adminId);
                    conn.Open();
                    using (MySqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            FullName = r["FullName"].ToString();
                            string avatar = r["Avatar"]?.ToString();
                            UserAvatar = string.IsNullOrEmpty(avatar)
                                ? "../Assets/images/avatar.jpg"
                                : "../Uploads/avatars/" + avatar;
                        }
                    }
                }
            }
        }

        public string GetEventImage(object url)
        {
            string img = url?.ToString();
            return string.IsNullOrEmpty(img) ? "https://via.placeholder.com/200x150?text=No+Image" : "../Uploads/" + img;
        }

        public string GetEventStatusClass(string status, DateTime endTime)
        {
            if (endTime < DateTime.Now) return "badge--past";
            return status == "Published" ? "badge--success" : "badge--draft";
        }

        public string GetEventStatusText(string status, DateTime endTime)
        {
            if (endTime < DateTime.Now) return "Đã qua";
            return status == "Published" ? "Đã xuất bản" : "Bản thảo";
        }

        public string GetRelativeTime(DateTime createdAt)
        {
            TimeSpan span = DateTime.Now - createdAt;
            if (span.TotalMinutes < 1) return "Vừa xong";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} ngày trước";
            return createdAt.ToString("dd-MM-yyyy");
        }

        private void DeleteEvent(int eventId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"UPDATE Events 
                   SET IsDeleted = 1 
                   WHERE Id = @eventId AND IsDeleted = 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@eventId", eventId);
                    conn.Open();
                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        // Optional: Tạo thông báo cho Admin
                        // CreateSystemNotification($"Sự kiện ID {eventId} đã được xóa bởi Admin.");
                        // Hoặc dùng toast nếu có
                    }
                }
            }
        }
        private void HandleLogout()
        {
            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            Response.Redirect("~/Account/Login.aspx");
        }

    }
}