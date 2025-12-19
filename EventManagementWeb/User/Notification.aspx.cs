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

namespace EventManagementWeb.User
{
    public partial class Notification : System.Web.UI.Page
    {
        public DataTable NotificationList;
        public int UnreadCount = 0;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["UserId"] == null)
            {
                Response.Redirect("~/Account/Login.aspx");
                return;
            }

            int userId = Convert.ToInt32(Session["UserId"]);

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
                        MarkAllAsRead(userId);
                    }
                }
            }

            LoadNotifications(userId);
        }

        private void LoadNotifications(int userId)
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
                    cmd.Parameters.AddWithValue("@userId", userId);
                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        NotificationList = new DataTable();
                        adapter.Fill(NotificationList);
                    }
                }
            }
        }

        private void MarkAllAsRead(int userId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "UPDATE Notifications SET IsRead = 1 WHERE UserId = @userId AND IsRead = 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
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
            return createdAt.ToString("dd/MM/yyyy HH:mm");
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