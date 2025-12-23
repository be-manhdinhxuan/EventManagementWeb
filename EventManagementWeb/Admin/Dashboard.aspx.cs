using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web;
using System.Web.Security;
using System.Web.UI;

namespace EventManagementWeb.Admin
{
    public partial class Dashboard : System.Web.UI.Page
    {
        private const string ToastSessionKey = "ToastMessage";
        public int TotalEvents = 0;
        public int UpcomingEvents = 0;
        public int TotalEmployees = 0;
        public int TotalRegistrations = 0;

        public DataTable RecentEvents;
        public DataTable RecentRegistrations;

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
            RecentEvents = new DataTable(); // Thêm dòng này
            RecentRegistrations = new DataTable();

            // Xử lý Đăng xuất thủ công qua Request.Form
            if (Request.HttpMethod == "POST" && Request.Form["btnAction"] == "logout")
            {
                HandleLogout();
                return;
            }

            if (!IsPostBack)
            {
                CheckAndShowToast();
                LoadStats();
                LoadRecentEvents();
                LoadRecentRegistrations();
            }

            LoadHeaderNotifications(adminId);
            LoadAdminInfo(adminId);
        }

        private void LoadStats()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
                SELECT 
                    (SELECT COUNT(*) FROM Events WHERE IsDeleted = 0) AS TotalEvents,
                    (SELECT COUNT(*) FROM Events WHERE IsDeleted = 0 AND StartTime > NOW()) AS UpcomingEvents,
                    (SELECT COUNT(*) FROM Users WHERE Role = 'Employee') AS TotalEmployees,
                    (SELECT SUM(CurrentRegistrations) FROM Events WHERE IsDeleted = 0) AS TotalRegistrations";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    using (MySqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            TotalEvents = Convert.ToInt32(r["TotalEvents"]);
                            UpcomingEvents = Convert.ToInt32(r["UpcomingEvents"]);
                            TotalEmployees = Convert.ToInt32(r["TotalEmployees"]);
                            TotalRegistrations = Convert.ToInt32(r["TotalRegistrations"]);
                        }
                    }
                }
            }
        }

        private void LoadRecentEvents()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
                SELECT Id, Title, StartTime, Status, CurrentRegistrations
                FROM Events
                WHERE IsDeleted = 0
                ORDER BY CreatedAt DESC
                LIMIT 5";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        RecentEvents = new DataTable();
                        adapter.Fill(RecentEvents);
                    }
                }
            }
        }

        private void LoadRecentRegistrations()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
        SELECT 
            u.FullName, 
            u.Avatar AS UserAvatar,   -- Thêm dòng này
            e.Title AS EventTitle, 
            er.CreatedAt AS RegistrationTime
        FROM EventRegistrations er
        JOIN Users u ON er.UserId = u.Id
        JOIN Events e ON er.EventId = e.Id
        WHERE er.IsDeleted = 0 
          AND er.Status = 'Approved'
        ORDER BY er.CreatedAt DESC
        LIMIT 5";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        RecentRegistrations = new DataTable();
                        adapter.Fill(RecentRegistrations);
                    }
                }
            }
        }

        private void LoadHeaderNotifications(int adminId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
        SELECT Id, Type, Title, Message, IsRead, CreatedAt, RelatedEventId  -- THÊM RelatedEventId VÀO ĐÂY
        FROM Notifications
        WHERE UserId = @userId OR Type = 'System'  -- Admin thấy cả cá nhân + hệ thống
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

        public string GetRelativeTime(DateTime createdAt)
        {
            TimeSpan span = DateTime.Now - createdAt;
            if (span.TotalMinutes < 1) return "Vừa xong";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} ngày trước";
            if (span.TotalDays < 30) return $"{(int)(span.TotalDays / 7)} tuần trước";
            if (span.TotalDays < 365) return createdAt.ToString("dd/MM");
            return createdAt.ToString("dd/MM/yyyy");
        }

        public string GetEventStatusClass(string status)
        {
            return status == "Published" ? "events__status--published" : "events__status--draft";
        }

        public string GetEventStatusText(string status)
        {
            return status == "Published" ? "Đã xuất bản" : "Bản thảo";
        }

        private void CheckAndShowToast()
        {
            if (Session[ToastSessionKey] != null)
            {
                var toastData = Session[ToastSessionKey] as System.Collections.Generic.Dictionary<string, string>;

                if (toastData != null)
                {
                    // Mã hóa chuỗi để đảm bảo không có lỗi cú pháp JavaScript
                    string title = HttpUtility.JavaScriptStringEncode(toastData["Title"]);
                    string message = HttpUtility.JavaScriptStringEncode(toastData["Message"]);
                    string type = toastData["Type"]; // Type thường không cần mã hóa

                    // Cập nhật cách tạo script
                    string script = $"showToast('{title}', '{message}', '{type}');";

                    // Đảm bảo bạn đang sử dụng "this" cho trang hiện tại
                    ClientScript.RegisterStartupScript(this.GetType(), "ShowToast", script, true);

                    Session.Remove(ToastSessionKey);
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