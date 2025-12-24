using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace EventManagementWeb.Admin
{
    public partial class Notification : System.Web.UI.Page
    {
        public DataTable NotificationList;
        public int UnreadCount = 0;
        public DataTable HeaderNotificationList;
        public string FullName = "";
        public string Email = "";
        public string Phone = "";
        public string UserAvatar = "../Assets/images/avatar.jpg";
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["UserId"] == null || Session["Role"]?.ToString() != "Admin")
            {
                Response.Redirect("~/Account/Login.aspx");
                return;
            }

            int adminId = Convert.ToInt32(Session["UserId"]);

            if (Request.HttpMethod == "POST")
            {
                string action = Request.Form["btnAction"];
                if (!string.IsNullOrEmpty(action))
                {
                    if (action == "logout")
                    {
                        HandleLogout();
                        return;
                    }
                    else if (action == "markAllRead")
                    {
                        MarkAllAsRead(adminId);
                    }
                }
            }

            LoadHeaderNotifications(adminId);
            LoadAdminInfo(adminId);
            LoadNotifications(adminId);
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

        private void LoadNotifications(int adminId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
                SELECT Id, Type, Title, Message, IsRead, CreatedAt, RelatedEventId
                FROM Notifications
                WHERE UserId = @userId
                ORDER BY CreatedAt DESC";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", adminId);
                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        NotificationList = new DataTable();
                        adapter.Fill(NotificationList);
                    }
                }
            }
        }

        private void MarkAllAsRead(int adminId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "UPDATE Notifications SET IsRead = 1 WHERE UserId = @userId AND IsRead = 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", adminId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
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
            return createdAt.ToString("dd-MM-yyyy");
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