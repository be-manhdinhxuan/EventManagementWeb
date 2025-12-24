using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web;
using System.Web.Security;
using System.Web.UI;

namespace EventManagementWeb.User
{
    public partial class MyEvent : UserBasePage
    {
        public string CurrentTab = "upcoming"; // Tab hiện tại
        public string CountUpcoming = "0";
        public string CountAttended = "0";
        public string CountCancelled = "0";
        public DataTable EventList;
        public List<string> Locations = new List<string>();
        public string SearchTerm = "";
        public string SelectedTime = "ALL";
        public string SelectedLocation = "ALL";
        public string SelectedStatus = "ALL";
        public int CurrentPage = 1;
        public int TotalPages = 1;
        private const int PageSize = 8;
        public DataTable HeaderNotificationList;
        public string FullName = "";
        public string Email = "";
        public string Phone = "";
        public int UnreadCount = 0;
        public string UserAvatar = "../Assets/images/avatar.jpg";

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
                SearchTerm = Request.Form["txtSearch"]?.Trim() ?? "";
                SelectedTime = Request.Form["ddlTime"] ?? "ALL";
                SelectedLocation = Request.Form["ddlLocation"] ?? "ALL";
                SelectedStatus = Request.Form["ddlStatus"] ?? "ALL";

                if (!string.IsNullOrEmpty(Request.Form["tabAction"]))
                {
                    CurrentTab = Request.Form["tabAction"];
                    CurrentPage = 1;
                }

                if (!string.IsNullOrEmpty(Request.Form["pageAction"]))
                {
                    int.TryParse(Request.Form["pageAction"], out CurrentPage);
                }
            }
            else
            {
                CurrentPage = 1;
            }

            string action = Request.Form["btnAction"];
            if (!string.IsNullOrEmpty(action))
            {
                if (action == "logout")
                {
                    HandleLogout();
                    return;
                }
                else if (action == "reset")
                {
                    SearchTerm = "";
                    SelectedTime = "ALL";
                    SelectedLocation = "ALL";
                    SelectedStatus = "ALL";
                    CurrentPage = 1;
                }
            }

            LoadHeaderNotifications(userId);
            LoadUserAvatar(userId);
            LoadTabCounts(userId);
            LoadLocations();
            LoadEventData(userId);
        }

        private void LoadHeaderNotifications(int userId)
        {
            // Lấy 5 thông báo mới nhất + đếm chưa đọc
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
                SELECT Id, Type, Title, Message, IsRead, CreatedAt, RelatedEventId
                FROM Notifications
                WHERE UserId = @userId
                ORDER BY CreatedAt DESC
                LIMIT 5";

            string countSql = "SELECT COUNT(*) FROM Notifications WHERE UserId = @userId AND IsRead = 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                conn.Open();
                // Đếm chưa đọc
                using (MySqlCommand cmd = new MySqlCommand(countSql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    UnreadCount = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // Lấy 5 thông báo mới nhất
                using (MySqlCommand cmd = new MySqlCommand(sql.Replace("TOP 5", "LIMIT 5"), conn)) // MySQL dùng LIMIT
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                    {
                        HeaderNotificationList = new DataTable();
                        adapter.Fill(HeaderNotificationList);
                    }
                }
            }
        }

        private void LoadUserAvatar(int userId)
        {
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
                string sql = "SELECT FullName, Email, Phone, Avatar FROM Users WHERE Id = @userId";

                using (MySqlConnection conn = new MySqlConnection(connStr))
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        conn.Open();
                        using (MySqlDataReader r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                FullName = r["FullName"].ToString();
                                Email = r["Email"].ToString();
                                Phone = r["Phone"] == DBNull.Value ? "" : r["Phone"].ToString();

                                string avatar = r["Avatar"].ToString();
                                UserAvatar = string.IsNullOrEmpty(avatar)
                                    ? "../Assets/images/avatar.jpg"
                                    : "../Uploads/avatars/" + avatar;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("LoadUserProfile Error: " + ex.Message);
                throw;
            }
        }

        public string GetRelativeTime(DateTime createdAt)
        {
            TimeSpan span = DateTime.Now - createdAt;

            if (span.TotalMinutes < 1)
                return "Vừa xong";

            if (span.TotalMinutes < 60)
                return $"{(int)span.TotalMinutes} phút trước";

            if (span.TotalHours < 24)
                return $"{(int)span.TotalHours} giờ trước";

            if (span.TotalDays < 7)
                return $"{(int)span.TotalDays} ngày trước";

            if (span.TotalDays < 30)
                return $"{(int)(span.TotalDays / 7)} tuần trước";

            if (span.TotalDays < 365)
                return createdAt.ToString("dd/MM");

            return createdAt.ToString("dd/MM/yyyy");
        }

        private void LoadEventData(int userId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;

            // Tab filter (thêm AND er.IsDeleted = 0 cho approved tabs)
            string tabFilter = "";
            if (CurrentTab == "upcoming")
                tabFilter = " AND er.Status = 'Approved' AND er.IsDeleted = 0 AND e.StartTime > NOW()";
            else if (CurrentTab == "attended")
                tabFilter = " AND er.Status = 'Approved' AND er.IsDeleted = 0 AND e.EndTime < NOW()";
            else if (CurrentTab == "cancelled")
                tabFilter = " AND er.Status = 'Cancelled'"; // Không lọc IsDeleted cho cancelled

            string commonFilter = BuildFilterCondition();

            string sql = $@"
                SELECT SQL_CALC_FOUND_ROWS DISTINCT
                    e.Id, e.Title, e.StartTime, e.EndTime, e.Location, e.ImageUrl,
                    e.Status, e.CurrentRegistrations, e.MaxCapacity, e.RegistrationDeadline
                FROM EventRegistrations er
                JOIN Events e ON er.EventId = e.Id
                WHERE er.UserId = @userId
                  AND e.IsDeleted = 0
                  {tabFilter} {commonFilter}
                ORDER BY e.StartTime DESC
                LIMIT @limit OFFSET @offset;
                SELECT FOUND_ROWS() AS TotalRecords;";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@limit", PageSize);
                    cmd.Parameters.AddWithValue("@offset", (CurrentPage - 1) * PageSize);

                    if (!string.IsNullOrEmpty(SearchTerm))
                        cmd.Parameters.AddWithValue("@search", "%" + SearchTerm + "%");
                    if (SelectedLocation != "ALL")
                        cmd.Parameters.AddWithValue("@location", SelectedLocation);

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
            List<string> conditions = new List<string>();

            // Tìm kiếm
            if (!string.IsNullOrEmpty(SearchTerm))
                conditions.Add(" e.Title LIKE @search ");

            // Thời gian
            if (SelectedTime == "TODAY")
                conditions.Add(" DATE(e.StartTime) = CURDATE() ");
            else if (SelectedTime == "PAST")
                conditions.Add(" e.EndTime < NOW() ");
            else if (SelectedTime == "THIS_MONTH")
                conditions.Add(" YEAR(e.StartTime) = YEAR(NOW()) AND MONTH(e.StartTime) = MONTH(NOW()) ");

            // Địa điểm
            if (SelectedLocation != "ALL")
                conditions.Add(" e.Location = @location ");

            // Trạng thái 
            if (SelectedStatus == "OPEN")
            {
                conditions.Add(" e.StartTime > NOW() " +
                               " AND (e.RegistrationDeadline IS NULL OR e.RegistrationDeadline >= NOW()) ");
            }
            else if (SelectedStatus == "UPCOMING")
            {
                conditions.Add(" e.StartTime > NOW() " +
                               " AND e.RegistrationDeadline IS NOT NULL " +
                               " AND e.RegistrationDeadline < NOW() ");
            }
            else if (SelectedStatus == "PAST")
            {
                conditions.Add(" e.EndTime < NOW() ");
            }
            // "ALL" không thêm gì

            return conditions.Count > 0 ? " AND " + string.Join(" AND ", conditions) : "";
        }

        private void LoadTabCounts(int userId)
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = @"
                SELECT
                    COUNT(DISTINCT CASE WHEN er.Status = 'Approved' AND er.IsDeleted = 0 AND e.StartTime > NOW() THEN e.Id END) AS upcoming,
                    COUNT(DISTINCT CASE WHEN er.Status = 'Approved' AND er.IsDeleted = 0 AND e.EndTime < NOW() THEN e.Id END) AS attended,
                    COUNT(DISTINCT CASE WHEN er.Status = 'Cancelled' THEN e.Id END) AS cancelled
                FROM EventRegistrations er
                JOIN Events e ON er.EventId = e.Id
                WHERE er.UserId = @userId AND e.IsDeleted = 0";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    conn.Open();
                    using (MySqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            CountUpcoming = r["upcoming"].ToString();
                            CountAttended = r["attended"].ToString();
                            CountCancelled = r["cancelled"].ToString();
                        }
                        else
                        {
                            CountUpcoming = CountAttended = CountCancelled = "0";
                        }
                    }
                }
            }
        }

        private void LoadLocations()
        {
            string connStr = ConfigurationManager.ConnectionStrings["EventManagementDB"].ConnectionString;
            string sql = "SELECT DISTINCT Location FROM Events WHERE IsDeleted = 0 AND Location IS NOT NULL AND Location != '' ORDER BY Location ASC";

            using (MySqlConnection conn = new MySqlConnection(connStr))
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    conn.Open();
                    using (MySqlDataReader r = cmd.ExecuteReader())
                    {
                        Locations.Clear();
                        while (r.Read())
                        {
                            Locations.Add(r["Location"].ToString());
                        }
                    }
                }
            }
        }

        // Hiển thị ảnh
        public string GetEventImage(object url) =>
            string.IsNullOrEmpty(url?.ToString()) ? "../Assets/images/default-event.jpg" : "../Uploads/" + url;

        // Badge class và text
        public string GetEventStatusClass(DataRow row)
        {
            DateTime start = Convert.ToDateTime(row["StartTime"]);
            DateTime end = Convert.ToDateTime(row["EndTime"]);
            DateTime? deadline = row["RegistrationDeadline"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["RegistrationDeadline"]);
            DateTime now = DateTime.Now;

            if (end < now) return "badge--secondary"; // Đã kết thúc
            if (start > now)
            {
                if (deadline == null || deadline >= now) return "badge--success"; // Đang mở đăng ký
                else return "badge--primary"; // Sắp diễn ra
            }
            if (start <= now && now <= end) return "badge--info"; // Đang diễn ra
            return "badge--success";
        }

        public string GetEventStatusText(DataRow row)
        {
            DateTime start = Convert.ToDateTime(row["StartTime"]);
            DateTime end = Convert.ToDateTime(row["EndTime"]);
            DateTime? deadline = row["RegistrationDeadline"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["RegistrationDeadline"]);
            DateTime now = DateTime.Now;

            if (end < now) return "Đã kết thúc";
            if (start > now)
            {
                if (deadline == null || deadline >= now) return "Đang mở đăng ký";
                else return "Sắp diễn ra";
            }
            if (start <= now && now <= end) return "Đang diễn ra";
            return "Đang mở đăng ký";
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