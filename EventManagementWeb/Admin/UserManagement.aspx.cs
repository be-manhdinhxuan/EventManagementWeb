using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.Security;
using System.Web.UI;

namespace EventManagementWeb.Admin
{
    public partial class UserManagement : AdminBasePage
    {
        public DataTable UserList;
        public int CurrentPage = 1;
        public int TotalPages = 1;
        public int TotalRecords = 0;
        public const int PageSize = 5;

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

            if (Request.HttpMethod == "POST")
            {
                string toggleData = Request.Form["toggleAction"];
                if (!string.IsNullOrEmpty(toggleData))
                {
                    string[] parts = toggleData.Split(':');
                    if (parts.Length == 2)
                    {
                        int userId = int.Parse(parts[0]);
                        bool newStatus = parts[1] == "1";
                        ToggleUserStatus(userId, newStatus);

                        Response.Redirect(Request.RawUrl);
                        return;
                    }
                }

                string action = Request.Form["btnAction"];
                if (action == "deleteUser")
                {
                    if (int.TryParse(Request.Form["deleteUserId"], out int userId))
                    {
                        DeleteUser(userId);
                        Response.Redirect(Request.RawUrl);
                        return;
                    }
                }

                // Xử lý search, filter, phân trang
                SearchTerm = Request.Form["txtSearch"]?.Trim() ?? "";
                SelectedStatus = Request.Form["ddlStatus"] ?? "ALL";

                if (Request.Form["btnAction"] == "reset")
                {
                    SearchTerm = "";
                    SelectedStatus = "ALL";
                    CurrentPage = 1;
                }

                if (!string.IsNullOrEmpty(Request.Form["pageAction"]))
                {
                    if (int.TryParse(Request.Form["pageAction"], out int page))
                    {
                        CurrentPage = page;
                    }
                }
            }

            LoadHeaderNotifications(adminId);
            LoadAdminInfo(adminId);
            LoadUsers();
        }

        private void LoadUsers()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            var parameters = new List<MySqlParameter>();
            string whereClause = BuildWhereClause(ref parameters);

            string sql = $@"
                SELECT SQL_CALC_FOUND_ROWS
                    Id, FullName, Email, Phone, Role, Avatar, IsActive, CreatedAt
                FROM Users
                WHERE IsDeleted = 0
                    {whereClause}
                ORDER BY CreatedAt DESC
                LIMIT @PageSize OFFSET @Offset;

                SELECT FOUND_ROWS() AS TotalRecords;";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@PageSize", PageSize);
                    cmd.Parameters.AddWithValue("@Offset", (CurrentPage - 1) * PageSize);

                    foreach (var param in parameters)
                    {
                        cmd.Parameters.Add(param);
                    }

                    conn.Open();
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        DataSet ds = new DataSet();
                        adapter.Fill(ds);

                        UserList = ds.Tables[0];

                        if (ds.Tables[1].Rows.Count > 0)
                        {
                            TotalRecords = Convert.ToInt32(ds.Tables[1].Rows[0]["TotalRecords"]);
                        }

                        TotalPages = TotalRecords > 0
                            ? (int)Math.Ceiling((double)TotalRecords / PageSize)
                            : 1;
                    }
                }
            }
        }

        private string BuildWhereClause(ref List<MySqlParameter> parameters)
        {
            var conditions = new List<string>();

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                conditions.Add("(FullName LIKE @search OR Email LIKE @search OR Phone LIKE @search)");
                parameters.Add(new MySqlParameter("@search", "%" + SearchTerm + "%"));
            }

            if (SelectedStatus != "ALL")
            {
                conditions.Add("IsActive = @status");
                int statusValue = int.Parse(SelectedStatus);
                parameters.Add(new MySqlParameter("@status", statusValue));
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

        public string GetUserAvatar(object avatarObj)
        {
            string avatar = avatarObj?.ToString();
            return string.IsNullOrEmpty(avatar)
                ? "../Assets/images/avatar.jpg"
                : "../Uploads/avatars/" + avatar;
        }

        private void ToggleUserStatus(int userId, bool newStatus)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "UPDATE Users SET IsActive = @isActive, UpdatedAt = NOW() WHERE Id = @id AND IsDeleted = 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@isActive", newStatus ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id", userId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void DeleteUser(int userId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "UPDATE Users SET IsDeleted = 1, UpdatedAt = NOW() WHERE Id = @id";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
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